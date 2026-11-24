#if UNITY_STANDALONE_LINUX
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using HIDrogen.Devices.LowLevel;
using HIDrogen.Imports;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using HIDrogen.LowLevel;
using HIDrogen.Services.Linux;
using HIDrogen.Utilities;
using Unity.Collections.LowLevel.Unsafe;
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
            public string manufacturer;
            public string product;
            public string serial;
            public string driver;

            public input_bus_type busType;
            public ushort vendorId;
            public ushort productId;
            public ushort revision;

            public byte xusbSubtype;
            public byte santrollerSubtype;
            public byte santrollerCapabilities;
        }

        internal struct AddContext : IDisposable
        {
            public string syspath;
            public string evdevPath;
            public EvdevCapabilities capabilities;

            public void Dispose()
            {
            }
        }

        private readonly Dictionary<string, EvdevBackendDevice> m_DevicesBySyspath
            = new Dictionary<string, EvdevBackendDevice>();

        protected override void OnDispose()
        {
        }

        protected override void OnStop()
        {
            base.OnStop();

            foreach (var p in m_DevicesBySyspath)
            {
                p.Value?.Dispose();
            }
            m_DevicesBySyspath.Clear();
        }

        protected override EvdevBackendDevice OnDeviceAdded(InputDevice device, AddContext context)
        {
            var backendDevice = new EvdevBackendDevice(this, context, device);
            m_DevicesBySyspath.Add(context.syspath, backendDevice);
            return backendDevice;
        }

        protected override void OnDeviceRemoved(EvdevBackendDevice device)
        {
            m_DevicesBySyspath.Remove(device.syspath);
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

            string syspath = device.get_syspath().ToString();
            string evdevPath = device.get_devnode().ToString();
            if (syspath == null || evdevPath == null)
            {
                return false;
            }

            if (m_DevicesBySyspath.ContainsKey(syspath))
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
            EvdevCapabilities capabilities;
            using (var evdevFd = Fcntl.open(evdevPath, Fcntl.O_RDONLY | Fcntl.O_CLOEXEC))
            {
                if (evdevFd == null || evdevFd.IsInvalid)
                {
                    // Ignore permission errors in logs
                    if (Posix.errno != Errno.EACCES && Posix.errno != Errno.EPERM)
                    {
                        Logging.LastError("Failed to open evdev device");
                    }
                    return false;
                }

                if (!IsDeviceAllowed(udev, device, evdevFd) ||
                    !CollectDeviceInfo(udev, device, evdevFd, out deviceInfo) ||
                    !CreateEvdevCapabilities(udev, device, evdevFd, deviceInfo, out capabilities))
                {
                    return false;
                }
            }

            var description = new InputDeviceDescription()
            {
                interfaceName = EvdevDefinitions.InterfaceName,
                manufacturer = deviceInfo.manufacturer,
                product = deviceInfo.product,
                serial = deviceInfo.serial,
                version = deviceInfo.revision.ToString(CultureInfo.InvariantCulture),
                capabilities = JsonUtility.ToJson(capabilities),
            };
            var context = new AddContext()
            {
                syspath = syspath,
                evdevPath = evdevPath,
                capabilities = capabilities,
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

            string syspath = device.get_syspath().ToString();
            string evdevPath = device.get_devnode().ToString();
            if (syspath != null &&
                evdevPath != null &&
                m_DevicesBySyspath.TryGetValue(syspath, out var backendDevice))
            {
                if (backendDevice != null)
                {
                    QueueDeviceRemove(backendDevice.device);
                    backendDevice.Dispose();
                }

                m_DevicesBySyspath.Remove(syspath);
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
            fd evdevFd,
            out DeviceInfo deviceInfo
        )
        {
            deviceInfo = new DeviceInfo();

            var inputIds = new input_id();
            if (Ioctl.ioctl(evdevFd, EVIOCGID, ref inputIds) < 0)
            {
                Logging.LastError("Failed to get input IDs");
                return false;
            }

            // Retrieve info from evdev first
            deviceInfo.busType = inputIds.bustype;
            deviceInfo.vendorId = inputIds.vendor;
            deviceInfo.productId = inputIds.product;
            deviceInfo.revision = inputIds.version;

            deviceInfo.product = GetDeviceString(evdevFd, EVIOCGNAME(0));
            deviceInfo.serial = GetDeviceString(evdevFd, EVIOCGUNIQ(0));

            // Then query udev for any fixups or missing properties

            // The kernel doesn't report the revision correctly for all device types for whatever reason
            // USB HID devices for example have bcdHID reported instead of bcdDevice
            // udev seems to smooth this over, so trust its judgement over evdev's
            var id_revision = evdev.get_property_value("ID_REVISION");
            if (!id_revision.IsInvalid && NumberUtils.TryParseHexU32(id_revision.ToString(), out uint realRevision))
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

            switch (deviceInfo.busType)
            {
                case input_bus_type.USB:
                {
                    FixupUsbInfo(udev, evdev, evdevFd, ref deviceInfo);
                    break;
                }
                case input_bus_type.BLUETOOTH:
                {
                    FixupBluetoothInfo(udev, evdev, evdevFd, ref deviceInfo);
                    break;
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct XUSBVendorDescriptorHeader
        {
            public usb_descriptor_header header;
            public ushort bcdXUSB;
            public byte bDeviceSubtype;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal unsafe struct Santroller2VendorDescriptor
        {
            public usb_descriptor_header header;
            public fixed byte signature[4]; // "SANT"
            public byte bVersion; // 2
            public byte bSubtype;
            public byte bCapabilities;

            public bool IsValid =>
                signature[0] == 'S' &&
                signature[1] == 'A' &&
                signature[2] == 'N' &&
                signature[3] == 'T' &&
                bVersion == 2;
        }

        private static unsafe void FixupUsbInfo(
            udev_context udev,
            udev_device evdev,
            fd evdevFd,
            ref DeviceInfo deviceInfo
        )
        {
            using (var usb_device = evdev.get_parent_with_subsystem_devtype(Udev.SubsystemUsb, Udev.DevtypeUsbDevice))
            using (var usb_interface = evdev.get_parent_with_subsystem_devtype(Udev.SubsystemUsb, Udev.DevtypeUsbInterface))
            {
                if (usb_device == null || usb_device.IsInvalid ||
                    usb_interface == null || usb_interface.IsInvalid)
                {
                    return;
                }

                deviceInfo.manufacturer = usb_device.get_sysattr_value("manufacturer").ToString();
                deviceInfo.product = usb_device.get_sysattr_value("product").ToString();

                // Device-specific fixups

                // XUSB via xpad
                if (deviceInfo.driver == Udev.DriverXpad)
                {
                    using (var xusbDescriptorBuffer = ReadUsbDescriptor(usb_device, usb_interface, 0x21))
                    {
                        if (xusbDescriptorBuffer.length >= sizeof(XUSBVendorDescriptorHeader))
                        {
                            var sanscriptor = (XUSBVendorDescriptorHeader*)xusbDescriptorBuffer.bufferPtr;
                            deviceInfo.xusbSubtype = sanscriptor->bDeviceSubtype;
                        }
                    }
                }

                // Santroller 2
                if (deviceInfo.vendorId == 0x1209 && deviceInfo.productId == 0x2883)
                {
                    // Santroller 2 stores a user-configurable profile name here
                    var interface_attr = usb_interface.get_sysattr_value("interface");
                    if (!interface_attr.IsInvalid)
                    {
                        deviceInfo.product = interface_attr.ToString();
                    }

                    // XInput mode does not include the Santroller descriptor
                    if (deviceInfo.driver != Udev.DriverXpad)
                    {
                        using (var sanscriptorBuffer = ReadUsbDescriptor(usb_device, usb_interface, 0xFF))
                        {
                            if (sanscriptorBuffer.length >= sizeof(Santroller2VendorDescriptor))
                            {
                                var sanscriptor = (Santroller2VendorDescriptor*)sanscriptorBuffer.bufferPtr;
                                if (sanscriptor->IsValid)
                                {
                                    deviceInfo.santrollerSubtype = sanscriptor->bSubtype;
                                    deviceInfo.santrollerCapabilities = sanscriptor->bCapabilities;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool GetUsbInterfaceNumber(
            udev_device usb_interface,
            out uint bInterfaceNumber,
            out uint bAlternateSetting
        )
        {
            bInterfaceNumber = 0;
            bAlternateSetting = 0;

            var bInterfaceNumber_attr = usb_interface.get_sysattr_value("bInterfaceNumber").ToString();
            var bAlternateSetting_attr = usb_interface.get_sysattr_value("bAlternateSetting").ToString();

            // bInterfaceNumber is formatted as hex, bAlternateSetting as space-padded decimal
            return NumberUtils.TryParseHexU32(bInterfaceNumber_attr, out bInterfaceNumber) &&
                NumberUtils.TryParseIntegerU32(bAlternateSetting_attr, out bAlternateSetting);
        }

        private static unsafe TempNativeBuffer<byte> ReadUsbDescriptor(
            udev_device usb_device,
            udev_device usb_interface,
            byte bDescriptorType
        )
        {
            string syspath = usb_device.get_syspath().ToString();
            if (syspath == null)
            {
                return default;
            }

            if (!GetUsbInterfaceNumber(usb_interface, out uint bInterfaceNumber, out uint bAlternateSetting))
            {
                return default;
            }

            using (var fddesc = Fcntl.open(Path.Combine(syspath, "descriptors"), Fcntl.O_RDONLY | Fcntl.O_CLOEXEC))
            {
                if (fddesc == null || fddesc.IsInvalid)
                {
                    Logging.LastError("Failed to open USB descriptors file");
                    return default;
                }

                // bLength is a single byte, so no descriptor can be larger than this
                byte* buffer = stackalloc byte[byte.MaxValue];
                var header = (usb_descriptor_header*)buffer;

                bool foundInterface = false;
                while (Fcntl.read(fddesc, buffer, sizeof(usb_descriptor_header)) == sizeof(usb_descriptor_header))
                {
                    if (header->bLength < sizeof(usb_descriptor_header))
                    {
                        // Malformed descriptor
                        return default;
                    }

                    int bodyLength = header->bLength - sizeof(usb_descriptor_header);
                    if (bodyLength > 0)
                    {
                        int bytesRead = Fcntl.read(fddesc, buffer + sizeof(usb_descriptor_header), bodyLength);
                        if (bytesRead != bodyLength)
                        {
                            // Bad read
                            Logging.LastError("Failed to read USB descriptor body");
                            return default;
                        }
                    }

                    if (header->bDescriptorType == (byte)libusb_descriptor_type.INTERFACE)
                    {
                        if (header->bLength < sizeof(usb_interface_descriptor))
                        {
                            // Malformed descriptor
                            return default;
                        }

                        var iface = (usb_interface_descriptor*)buffer;
                        foundInterface =
                            iface->bInterfaceNumber == bInterfaceNumber &&
                            iface->bAlternateSetting == bAlternateSetting;
                    }
                    else if (foundInterface && header->bDescriptorType == bDescriptorType)
                    {
                        var ownedBuffer = new TempNativeBuffer<byte>(header->bLength);
                        UnsafeUtility.MemCpy(ownedBuffer.bufferPtr, buffer, header->bLength);
                        return ownedBuffer;
                    }
                }
            }

            return default;
        }

        private static void FixupBluetoothInfo(
            udev_context udev,
            udev_device evdev,
            fd evdevFd,
            ref DeviceInfo deviceInfo
        )
        {
            // No general fixups to perform, so we go straight to device-specific ones

            // Santroller 2
            if (deviceInfo.vendorId == 0x1209 && deviceInfo.productId == 0x2883)
            {
                deviceInfo.santrollerSubtype = (byte)(deviceInfo.revision >> 8);
            }
        }

        private static unsafe bool CreateEvdevCapabilities(
            udev_context udev,
            udev_device evdev,
            fd fd,
            DeviceInfo deviceInfo,
            out EvdevCapabilities capabilities
        )
        {
            capabilities = new EvdevCapabilities()
            {
                driver = deviceInfo.driver,

                busType = deviceInfo.busType,
                vendorId = deviceInfo.vendorId,
                productId = deviceInfo.productId,

                xusbSubtype = deviceInfo.xusbSubtype,
                santrollerSubtype = deviceInfo.santrollerSubtype,
                santrollerCapabilities = deviceInfo.santrollerCapabilities,

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

            capabilities.supportedKeys = supportedKeys.ToArray();
            capabilities.supportedAbs = supportedAbs.ToArray();
            capabilities.supportedRel = supportedRel.ToArray();

            // Collect abs calibration info
            capabilities.absInfo = new input_absinfo[capabilities.supportedAbs.Length];

            for (int i = 0; i < capabilities.supportedAbs.Length; i++)
            {
                var abs = capabilities.supportedAbs[i];
                var absInfo = new input_absinfo();
                if (Ioctl.ioctl(fd, EVIOCGABS(abs), ref absInfo) < 0)
                {
                    Logging.LastError("Failed to get evdev absinfo");
                    continue;
                }

                absInfo.value = 0; // we don't care about the retrieved state here
                capabilities.absInfo[i] = absInfo;
            }

            return true;
        }
    }
}
#endif
