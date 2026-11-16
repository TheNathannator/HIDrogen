#if UNITY_STANDALONE_LINUX
using System;
using System.Collections.Generic;
using System.Globalization;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using HIDrogen.LowLevel;
using HIDrogen.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;

// TODO:
// - Try to correlate sibling evdev devices together;
//   e.g. pair the accelerometer of a PS controller to its main gamepad inputs

namespace HIDrogen.Backend
{
    using static Evdev;

    internal partial class EvdevBackend : CustomInputBackend<EvdevDevice, EvdevBackend.AddContext>,
        IUdevBackend
    {
        private struct DeviceInfo
        {
            public string name;
            public string serial;

            public ushort vendorId;
            public ushort productId;
            public ushort revision;
        }

        internal struct AddContext : IDisposable
        {
            public string path;
            public EvdevDescriptor descriptor;

            public void Dispose()
            {
            }
        }

        public const string InterfaceName = "Evdev";
        public static readonly FourCC InputFormat = new FourCC('E', 'V', 'D', 'V');

        private readonly Dictionary<string, EvdevDevice> m_DevicesByPath = new Dictionary<string, EvdevDevice>();

        protected override void OnDispose()
        {
        }

        protected override void OnStop()
        {
            base.OnStop();

            foreach (var p in m_DevicesByPath)
            {
                p.Value?.Dispose();
            }
            m_DevicesByPath.Clear();
        }

        protected override EvdevDevice OnDeviceAdded(InputDevice device, AddContext context)
        {
            var fd = Fcntl.open(context.path, Fcntl.O_RDONLY | Fcntl.O_CLOEXEC);
            if (fd == null || fd.IsInvalid)
            {
                throw new Exception(Logging.MakeLastErrorMessage("Failed to open evdev device"));
            }

            var backendDevice = new EvdevDevice(this, device, context.descriptor, context.path, fd);
            m_DevicesByPath.Add(context.path, backendDevice);
            return backendDevice;
        }

        protected override void OnDeviceRemoved(EvdevDevice device)
        {
            device.Dispose();
        }

        public bool ProbeDeviceAdd(udev_context udev, udev_device device)
        {
            if (device.get_subsystem().ToString() != Udev.SubsystemInput)
            {
                return false;
            }

            string evdevPath = device.get_devnode().ToString();
            if (m_DevicesByPath.ContainsKey(evdevPath))
            {
                // Return true so that this device is not considered by other backends
                return true;
            }

            // We only want evdev devices here, not joydev or any other kinds of input devices
            if (!device.get_sysname().ToString().StartsWith("event"))
            {
                return false;
            }

            DeviceInfo deviceInfo;
            EvdevDescriptor descriptor;
            using (var fd = Fcntl.open(evdevPath, Fcntl.O_RDONLY | Fcntl.O_CLOEXEC))
            {
                if (fd == null || fd.IsInvalid)
                {
                    Logging.LastError("Failed to open evdev device");
                    return false;
                }

                if (!IsDeviceAllowed(udev, device, fd) ||
                    !CollectDeviceInfo(udev, device, fd, out deviceInfo) ||
                    !CreateEvdevDescriptor(udev, device, fd, deviceInfo, out descriptor))
                {
                    return false;
                }
            }

            var description = new InputDeviceDescription()
            {
                interfaceName = InterfaceName,
                product = deviceInfo.name,
                serial = deviceInfo.serial,
                version = deviceInfo.revision.ToString(CultureInfo.InvariantCulture),
                capabilities = JsonUtility.ToJson(descriptor),
            };
            var context = new AddContext()
            {
                path = evdevPath,
                descriptor = descriptor,
            };

            if (!AddDeviceNow(description, context))
            {
                return false;
            }

            Logging.Verbose($"evdev device {evdevPath} added");
            return false;
        }

        public void ProbeDeviceRemove(udev_context udev, udev_device device)
        {
            if (device.get_subsystem().ToString() != Udev.SubsystemInput)
            {
                return;
            }

            string evdevPath = device.get_devnode().ToString();
            if (m_DevicesByPath.TryGetValue(evdevPath, out var backendDevice))
            {
                if (backendDevice != null)
                {
                    RemoveDeviceNow(backendDevice.device);
                    backendDevice.Dispose();
                }
                m_DevicesByPath.Remove(evdevPath);
                Logging.Verbose($"evdev device {evdevPath} removed");
            }
        }

        // Filter out devices that are most definitely not relevant early on,
        // to avoid going through the whole layout matching process just to not match anything.
        private static bool IsDeviceAllowed(
            udev_context udev,
            udev_device evdev,
            fd fd
        )
        {
            // udev determines device classes for us via device proprties.
            // It handles various edge cases which would be very inconvenient to reimplement ourselves.
            // If this property is not set, assume the device is irrelevant and discard it.
            string hasJoystick = evdev.get_property_value("ID_INPUT_JOYSTICK").ToString();
            return uint.TryParse(hasJoystick, out uint value) && value != 0;
        }

        private static bool CollectDeviceInfo(
            udev_context udev,
            udev_device evdev,
            fd fd,
            out DeviceInfo deviceInfo
        )
        {
            deviceInfo = new DeviceInfo();

            var inputIds = new input_id();
            if (Ioctl.ioctl(fd, EVIOCGID, ref inputIds) < 0)
            {
                Logging.LastError("Failed to get input IDs");
                return false;
            }

            deviceInfo.vendorId = inputIds.vendor;
            deviceInfo.productId = inputIds.product;
            deviceInfo.revision = inputIds.version;

            deviceInfo.name = GetDeviceString(fd, EVIOCGNAME(0));
            deviceInfo.serial = GetDeviceString(fd, EVIOCGUNIQ(0));

            return true;
        }

        private static unsafe string GetDeviceString(fd fd, IoCode ioCode)
        {
            ioCode.size = 0;

            int byteLength = Ioctl.ioctl(fd, ioCode, null);
            if (byteLength < 0)
            {
#if HIDROGEN_VERBOSE_LOGGING
                if (Posix.errno != Errno.ENOENT)
                {
                    Logging.LastError("Could not get evdev device string");
                }
#endif
                return null;
            }

            ioCode.size = byteLength;

            var str = UTF8String.WithByteLength(byteLength);
            try
            {
                if (Ioctl.ioctl_unsafe(fd, ioCode, (void*)str.ToPtr()) < 0)
                {
                    return null;
                }

                return str.ToString();
            }
            finally
            {
                str.Dispose();
            }
        }

        private static bool CreateEvdevDescriptor(
            udev_context udev,
            udev_device evdev,
            fd fd,
            DeviceInfo deviceInfo,
            out EvdevDescriptor descriptor
        )
        {
            descriptor = new EvdevDescriptor()
            {
                vendorId = deviceInfo.vendorId,
                productId = deviceInfo.productId,
            };

            return false; // TODO
        }
    }
}
#endif
