#if UNITY_STANDALONE_LINUX
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using HIDrogen.LowLevel;
using HIDrogen.Services.Linux;
using HIDrogen.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

// TODO:
// - Try to correlate sibling evdev devices together;
//   e.g. pair the accelerometer of a PS controller to its main gamepad inputs

namespace HIDrogen.Backend.Linux
{
    using static Evdev;

    internal partial class EvdevBackend : CustomInputBackend<EvdevBackendDevice, EvdevBackend.AddContext>,
        IUdevBackend
    {
        private struct DeviceInfo
        {
            public string name;
            public string serial;
            public string driver;

            public input_bus_type busType;
            public ushort vendorId;
            public ushort productId;
            public ushort revision;

            public ushort santrollerSubtype;
            public ushort xpadSubtype;
        }

        internal struct AddContext : IDisposable
        {
            public string path;
            public EvdevCapabilities descriptor;

            public void Dispose()
            {
            }
        }

        private readonly Dictionary<string, EvdevBackendDevice> m_DevicesByPath = new Dictionary<string, EvdevBackendDevice>();

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

        protected override EvdevBackendDevice OnDeviceAdded(InputDevice device, AddContext context)
        {
            var fd = Fcntl.open(context.path, Fcntl.O_RDONLY | Fcntl.O_CLOEXEC);
            if (fd == null || fd.IsInvalid)
            {
                throw new Exception(Logging.MakeLastErrorMessage("Failed to open evdev device"));
            }

            var backendDevice = new EvdevBackendDevice(this, device, context.descriptor, context.path, fd);
            m_DevicesByPath.Add(context.path, backendDevice);
            return backendDevice;
        }

        protected override void OnDeviceRemoved(EvdevBackendDevice device)
        {
            device.Dispose();
        }

        protected override unsafe long? OnDeviceCommand(EvdevBackendDevice device, InputDeviceCommand* command)
        {
            // System commands
            // if (command->type == EnableDeviceCommand.Type)
            //     return Enable(command);
            // if (command->type == DisableDeviceCommand.Type)
            //     return Disable(command);
            // if (command->type == QueryEnabledStateCommand.Type)
            //     return IsEnabled(command);
            if (command->type == RequestSyncCommand.Type)
                return device.SyncState(command);
            // if (command->type == RequestResetCommand.Type)
            //     return ResetState(command);
            // if (command->type == QueryCanRunInBackground.Type)
            //     return CanRunInBackground(command);

            return null;
        }

        public bool ProbeDeviceAdd(udev_context udev, udev_device device)
        {
            if (device.get_subsystem().ToString() != Udev.SubsystemInput)
            {
                return false;
            }

            string evdevPath = device.get_devnode().ToString();
            if (evdevPath == null)
            {
                return false;
            }

            if (m_DevicesByPath.ContainsKey(evdevPath))
            {
                // Return true so that this device is not considered by other backends
                return true;
            }

            // We only want evdev devices here, not joydev or any other kinds of input devices
            string sysname = device.get_sysname().ToString();
            if (sysname == null || !sysname.StartsWith("event"))
            {
                return false;
            }

            DeviceInfo deviceInfo;
            EvdevCapabilities descriptor;
            using (var fd = Fcntl.open(evdevPath, Fcntl.O_RDONLY | Fcntl.O_CLOEXEC))
            {
                if (fd == null || fd.IsInvalid)
                {
                    // Ignore permission errors in logs
                    if (Posix.errno != Errno.EACCES && Posix.errno != Errno.EPERM)
                    {
                        Logging.LastError("Failed to open evdev device");
                    }
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
                interfaceName = EvdevDefinitions.InterfaceName,
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
            return true;
        }

        public void ProbeDeviceRemove(udev_context udev, udev_device device)
        {
            if (device.get_subsystem().ToString() != Udev.SubsystemInput)
            {
                return;
            }

            string evdevPath = device.get_devnode().ToString();
            if (evdevPath != null && m_DevicesByPath.TryGetValue(evdevPath, out var backendDevice))
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

            // Retrieve info from evdev first
            deviceInfo.busType = inputIds.bustype;
            deviceInfo.vendorId = inputIds.vendor;
            deviceInfo.productId = inputIds.product;
            deviceInfo.revision = inputIds.version;

            deviceInfo.name = GetDeviceString(fd, EVIOCGNAME(0));
            deviceInfo.serial = GetDeviceString(fd, EVIOCGUNIQ(0));
            bool xpad = evdev.get_property_value("ID_USB_DRIVER").ToString() == "xpad";
            bool santroller2 = deviceInfo.vendorId == 0x1209 && deviceInfo.productId == 0x2883;

            // If the device is neither xpad nor santroller2, we don't need to collect further info.
            if (!xpad && !santroller2)
                return true;

            if (santroller2)
            {
                if (evdev.get_property_value("ID_BUS").ToString() == "bluetooth") {
                    deviceInfo.santrollerSubtype = (byte)(deviceInfo.revision >> 8);
                    return true;
                }
            }

            // usb_interface: identifies which interface this evdev node belongs to
            var usbInterface = evdev.get_parent_with_subsystem_devtype(Udev.SubsystemUsb, "usb_interface");
            if (usbInterface == null || usbInterface.IsInvalid)
                return true;

            // bInterfaceNumber is formatted as hex, bAlternateSetting as space-padded decimal
            if (!int.TryParse(usbInterface.get_sysattr_value("bInterfaceNumber").ToString()?.Trim(),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int interfaceNumber) ||
                !int.TryParse(usbInterface.get_sysattr_value("bAlternateSetting").ToString()?.Trim(),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out int altSetting))
            {
                return true;
            }
            if (santroller2)
            {
                deviceInfo.name = usbInterface.get_sysattr_value("interface").ToString();
            }

            var usbDevice = evdev.get_parent_with_subsystem_devtype(Udev.SubsystemUsb, "usb_device");
            if (usbDevice == null || usbDevice.IsInvalid)
                return true;

            string devicePath = usbDevice.get_syspath().ToString();
            if (devicePath == null)
                return true;
            using (var fddesc = Fcntl.open(Path.Combine(devicePath, "descriptors"), Fcntl.O_RDONLY | Fcntl.O_CLOEXEC))
            {
                if (fddesc == null || fddesc.IsInvalid)
                {
                    Logging.LastError($"Failed to open USB descriptors for {devicePath}");
                    return true;
                }

                const byte USB_DT_INTERFACE = 0x04;
                bool foundInterface = false;

                // bLength is a single byte, so no descriptor can be larger than this
                var buffer = new byte[byte.MaxValue];

                // Read one descriptor at a time: the 2-byte header (bLength, bDescriptorType), then the body.
                while (Fcntl.read(fddesc, buffer, 0, 2) == 2)
                {
                    int length = buffer[0];
                    byte type = buffer[1];

                    // Malformed descriptor; bail instead of looping forever
                    if (length < 2)
                        break;

                    int bodyLength = length - 2;
                    if (bodyLength > 0 && Fcntl.read(fddesc, buffer, 2, bodyLength) != bodyLength)
                        break;

                    if (type == USB_DT_INTERFACE)
                    {
                        // bInterfaceNumber at offset 2, bAlternateSetting at offset 3
                        foundInterface = length >= 4 &&
                            buffer[2] == interfaceNumber &&
                            buffer[3] == altSetting;
                    }
                    else if (foundInterface)
                    {
                        // XInput vendor descriptor: subtype at offset 4
                        if (xpad && type == 0x21 && length >= 5)
                        {
                            deviceInfo.xpadSubtype = buffer[4];
                            break;
                        }

                        // Santroller vendor descriptor: "SANT" at offset 2, subtype at offset 7
                        if (santroller2 && type == 0xFF && length >= 8 &&
                            buffer[2] == 'S' && buffer[3] == 'A' &&
                            buffer[4] == 'N' && buffer[5] == 'T')
                        {
                            deviceInfo.santrollerSubtype = buffer[7];
                            break;
                        }
                    }
                }
            }

            // Then query udev for any fixups or missing properties

            // The kernel doesn't report the revision correctly for all device types for whatever reason
            // USB HID devices for example have bcdHID reported instead of bcdDevice
            // udev seems to smooth this over, so trust its judgement over evdev's
            var id_revision = evdev.get_property_value("ID_REVISION");
            if (!id_revision.IsInvalid && NumberUtils.TryParseHexUInt32(id_revision.ToString(), out uint realRevision))
            {
                deviceInfo.revision = (ushort)realRevision;
            }

            // Retrieve device driver from parents
            for (var parent = evdev.get_parent(); parent != null && !parent.IsInvalid; parent = parent.get_parent())
            {
                if (parent.get_subsystem().ToString() != Udev.SubsystemInput)
                {
                    deviceInfo.driver = parent.get_driver().ToString();
                    if (!string.IsNullOrEmpty(deviceInfo.driver))
                    {
                        break;
                    }
                }
            }

            return true;
        }

        private static unsafe string GetDeviceString(fd fd, IoCode ioCode)
        {
            const int maxLength = 256;

            var str = UTF8String.WithByteLength(maxLength);
            try
            {
                ioCode.size = maxLength;
                if (Ioctl.ioctl_unsafe(fd, ioCode, (void*)str.ToPtr()) < 0)
                {
                    if (Posix.errno != Errno.ENOENT)
                    {
                        Logging.LastError("Could not get evdev device string");
                    }
                    return null;
                }

                return str.ToString();
            }
            finally
            {
                str.Dispose();
            }
        }

        private static unsafe bool CreateEvdevDescriptor(
            udev_context udev,
            udev_device evdev,
            fd fd,
            DeviceInfo deviceInfo,
            out EvdevCapabilities descriptor
        )
        {
            descriptor = new EvdevCapabilities()
            {
                driver = deviceInfo.driver,

                busType = deviceInfo.busType,
                vendorId = deviceInfo.vendorId,
                productId = deviceInfo.productId,
                xpadSubtype = deviceInfo.xpadSubtype,
                santrollerSubtype = deviceInfo.santrollerSubtype,

                supportedKeys = Array.Empty<input_key_code>(),
                supportedAbs = Array.Empty<input_abs_code>(),
                supportedRel = Array.Empty<input_rel_code>(),
            };

            // note: C# 7.3 does not allow directly placing a `stackalloc` expression as an argument
            var keyMaskPtr = stackalloc UIntPtr[KEY_MAX_LONGS];
            var absMaskPtr = stackalloc UIntPtr[ABS_MAX_LONGS];
            var relMaskPtr = stackalloc UIntPtr[REL_MAX_LONGS];

            var keyMask = new EvdevBits<input_key_code>(keyMaskPtr, KEY_MAX_LONGS);
            var absMask = new EvdevBits<input_abs_code>(absMaskPtr, ABS_MAX_LONGS);
            var relMask = new EvdevBits<input_rel_code>(relMaskPtr, REL_MAX_LONGS);

            if (keyMask.Populate(fd, EVIOCGBIT(input_event_type.EV_KEY, keyMask.count)) < 0 ||
                absMask.Populate(fd, EVIOCGBIT(input_event_type.EV_ABS, absMask.count)) < 0 ||
                relMask.Populate(fd, EVIOCGBIT(input_event_type.EV_REL, relMask.count)) < 0)
            {
                Logging.LastError("Failed to get evdev masks");
                return false;
            }

            // Collect into lists for easier use
            var supportedKeys = new List<input_key_code>();
            var supportedAbs = new List<input_abs_code>();
            var supportedRel = new List<input_rel_code>();

            for (input_key_code key = 0; key < input_key_code.KEY_MAX; key++)
            {
                if (keyMask.TestBit(key))
                {
                    supportedKeys.Add(key);
                }
            }

            for (input_abs_code abs = 0; abs < input_abs_code.ABS_MAX; abs++)
            {
                if (absMask.TestBit(abs))
                {
                    supportedAbs.Add(abs);
                }
            }

            for (input_rel_code rel = 0; rel < input_rel_code.REL_MAX; rel++)
            {
                if (relMask.TestBit(rel))
                {
                    supportedRel.Add(rel);
                }
            }

            descriptor.supportedKeys = supportedKeys.ToArray();
            descriptor.supportedAbs = supportedAbs.ToArray();
            descriptor.supportedRel = supportedRel.ToArray();

            // Collect abs calibration info
            descriptor.absInfo = new input_absinfo[descriptor.supportedAbs.Length];

            for (int i = 0; i < descriptor.supportedAbs.Length; i++)
            {
                var abs = descriptor.supportedAbs[i];
                var absInfo = new input_absinfo();
                if (Ioctl.ioctl(fd, EVIOCGABS(abs), ref absInfo) < 0)
                {
                    Logging.LastError("Failed to get evdev absinfo");
                    continue;
                }

                absInfo.value = 0; // we don't care about the retrieved state here
                descriptor.absInfo[i] = absInfo;
            }

            return true;
        }
    }
}
#endif
