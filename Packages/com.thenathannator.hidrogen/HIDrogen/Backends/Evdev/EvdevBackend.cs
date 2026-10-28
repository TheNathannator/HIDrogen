#if UNITY_STANDALONE_LINUX
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using HIDrogen.Imports;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend
{
    [Serializable]
    internal class EvdevCapabilities
    {
        public int bus;
        public int vendorId;
        public int productId;
        public int version;
        public string driver;
        public int subType;
        public int santrollerProtocol;
        public int santrollerFlags;
        public string keys;
        public string axes;
        public EvdevAxis[] axisInfo;
    }

    [Serializable]
    internal struct EvdevAxis
    {
        public int code;
        public int minimum;
        public int maximum;
        public int fuzz;
        public int flat;
        public int resolution;
    }

    internal sealed class EvdevBackend : CustomInputBackend<EvdevDevice>
    {
        private sealed class DeviceContext : IDisposable
        {
            public fd handle;
            public string path;
            public string claim;
            public byte[] axes;
            public bool transferred;
            public EvdevBackend backend;

            public void Dispose()
            {
                if (transferred)
                    return;
                handle?.Dispose();
                LinuxInputOwnership.Release(claim);
                backend.m_Pending.Remove(path);
            }
        }

        private readonly udev_context m_Udev = Udev.Instance.context_new();
        private readonly Stopwatch m_ScanTimer = Stopwatch.StartNew();
        private readonly HashSet<string> m_Pending = new HashSet<string>();
        private readonly HashSet<string> m_Warned = new HashSet<string>();
        public HidApiBackend hidBackend;

        public EvdevBackend()
        {
            if (m_Udev == null || m_Udev.IsInvalid)
                throw new IOException("Failed to initialize udev for evdev.");
        }

        protected override void OnDispose() => m_Udev.Dispose();

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (hidBackend != null && !hidBackend.discoveryComplete)
                return;
            if (m_ScanTimer.ElapsedMilliseconds < 1000)
                return;
            m_ScanTimer.Restart();
            EnumerateDevices();
        }

        private void EnumerateDevices()
        {
            using (var scan = m_Udev.enumerate_new())
            {
                if (scan.IsInvalid || scan.add_match_subsystem("input") < 0 || scan.scan_devices() < 0)
                {
                    Logging.InteropError("Failed to enumerate evdev devices");
                    return;
                }

                var present = new HashSet<string>();
                for (var entry = scan.get_list_entry(); !entry.IsInvalid; entry = entry.get_next())
                {
                    string sysPath = entry.get_name();
                    string name = Path.GetFileName(sysPath);
                    if (!name.StartsWith("event", StringComparison.Ordinal))
                        continue;
                    string path = "/dev/input/" + name;
                    present.Add(path);
                    if (m_Pending.Contains(path))
                        continue;
                    using (var node = m_Udev.device_new_from_syspath(sysPath))
                    {
                        if (node == null || node.IsInvalid)
                            continue;
                        ProbeDevice(node, path);
                    }
                }
                m_Warned.RemoveWhere(path => !present.Contains(path));
            }
        }

        private void ProbeDevice(udev_device node, string path)
        {
            using (var hid = node.get_parent_with_subsystem_devtype("hid", null))
            using (var usb = node.get_parent_with_subsystem_devtype("usb", "usb_interface"))
            using (var input = node.get_parent_with_subsystem_devtype("input", null))
            {
                string driver = hid?.get_driver() ?? usb?.get_driver();
                bool santroller = driver == "hid-generic" &&
                    input?.get_sysattr_value("id/vendor") == "1209" &&
                    (input.get_sysattr_value("id/product") == "2882" ||
                        input.get_sysattr_value("id/product") == "2883");
                if (driver != "sony" && driver != "xpad" && !santroller)
                    return;

                DeviceContext context = null;
                try
                {
                    string hidraw = FindHidraw(hid);
                    if (hidraw != null)
                        hidBackend?.ProbePath(hidraw);
                    string claim = hidraw ?? path;
                    if (!LinuxInputOwnership.TryClaim(claim))
                        return;
                    context = new DeviceContext() { backend = this, path = path, claim = claim };
                    context.handle = Fcntl.open(path, Fcntl.O_RDONLY | Fcntl.O_NONBLOCK | 0x80000);
                    if (context.handle == null || context.handle.IsInvalid)
                    {
                        WarnOnce(path, $"Cannot open evdev fallback '{path}' (errno {(int)Posix.errno}). Check input-device permissions.");
                        return;
                    }

                    Evdev.InputId id = default;
                    var keys = new byte[96];
                    context.axes = new byte[8];
                    if (Evdev.GetId(context.handle, ref id) < 0 ||
                        Evdev.GetBits(context.handle, Evdev.EV_KEY, keys) < 0 ||
                        Evdev.GetBits(context.handle, Evdev.EV_ABS, context.axes) < 0)
                    {
                        WarnOnce(path, $"Failed to query evdev capabilities for '{path}' (errno {(int)Posix.errno}).");
                        return;
                    }

                    var axisInfo = new List<EvdevAxis>();
                    for (int code = 0; code < 64; code++)
                    {
                        if (!Evdev.HasBit(context.axes, code))
                            continue;
                        Evdev.AbsInfo info = default;
                        if (Evdev.GetAbs(context.handle, code, ref info) < 0)
                        {
                            WarnOnce(path, $"Failed to query axis {code} for '{path}' (errno {(int)Posix.errno}).");
                            return;
                        }
                        axisInfo.Add(new EvdevAxis()
                        {
                            code = code, minimum = info.minimum, maximum = info.maximum,
                            flat = info.flat, fuzz = info.fuzz, resolution = info.resolution,
                        });
                    }

                    int protocol = 0, flags = 0;
                    int subType = driver == "xpad" ? ReadXInputSubType(usb, path) : 0;
                    if (santroller)
                    {
                        subType = ReadUsbInstrumentIdentity(usb, path, true, out flags);
                        if (subType != 0)
                            protocol = 2;
                        else if (id.product == 0x2882 && (id.version >> 8) >= 3 && (id.version >> 8) <= 8)
                        {
                            protocol = 1;
                            subType = id.version >> 8;
                        }
                        else
                        {
                            WarnOnce(path, $"Santroller evdev fallback '{path}' has no supported instrument identity. Current firmware must expose the Santroller USB identity descriptor.");
                            return;
                        }
                    }

                    string productName = null;
                    if (santroller && protocol == 2)
                        productName = usb?.get_sysattr_value("interface")?.Trim();
                    if (string.IsNullOrEmpty(productName))
                        productName = input?.get_sysattr_value("name");

                    var description = new InputDeviceDescription()
                    {
                        interfaceName = "Evdev",
                        product = productName,
                        capabilities = JsonUtility.ToJson(new EvdevCapabilities()
                        {
                            bus = id.bus, vendorId = id.vendor, productId = id.product,
                            version = id.version, driver = driver,
                            subType = subType, santrollerProtocol = protocol, santrollerFlags = flags,
                            keys = Convert.ToBase64String(keys),
                            axes = Convert.ToBase64String(context.axes),
                            axisInfo = axisInfo.ToArray(),
                        }),
                    };
                    if (string.IsNullOrEmpty(InputSystem.TryFindMatchingLayout(description)))
                        return;
                    m_Pending.Add(path);
                    QueueDeviceAdd(description, context);
                    context = null;
                }
                catch (IOException ex)
                {
                    WarnOnce(path, $"Failed to inspect evdev fallback '{path}': {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    WarnOnce(path, $"Cannot inspect evdev fallback '{path}': {ex.Message}");
                }
                finally
                {
                    context?.Dispose();
                }
            }
        }

        private int ReadXInputSubType(udev_device usbInterface, string path)
            => ReadUsbInstrumentIdentity(usbInterface, path, false, out _);

        private int ReadUsbInstrumentIdentity(udev_device usbInterface, string path, bool santroller, out int flags)
        {
            flags = 0;
            try
            {
                return ReadUsbInstrumentIdentity(usbInterface, santroller, out flags);
            }
            catch (IOException ex)
            {
                WarnOnce(path, $"Cannot read USB instrument identity for '{path}'; continuing without it: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                WarnOnce(path, $"Cannot read USB instrument identity for '{path}'; continuing without it: {ex.Message}");
            }
            return 0;
        }

        internal static int ReadUsbInstrumentIdentity(udev_device usbInterface, bool santroller, out int flags)
        {
            flags = 0;
            if (usbInterface == null)
                return 0;

            string interfaceNumber = usbInterface.get_sysattr_value("bInterfaceNumber");
            if (!int.TryParse(interfaceNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int target))
                throw new IOException("Missing USB interface number.");
            using (var usb = usbInterface.get_parent_with_subsystem_devtype("usb", "usb_device"))
            {
                if (usb == null)
                    throw new IOException("Missing USB device parent.");
                using (var stream = File.OpenRead(Path.Combine(usb.get_syspath(), "descriptors")))
                using (var buffer = new MemoryStream())
                {
                    // Sysfs reports a maximum size, not the actual descriptor length.
                    stream.CopyTo(buffer);
                    return ParseUsbInstrumentIdentity(buffer.ToArray(), target, santroller, out flags);
                }
            }
        }

        internal static int ParseXInputSubType(byte[] descriptors, int targetInterface)
            => ParseUsbInstrumentIdentity(descriptors, targetInterface, false, out _);

        internal static int ParseUsbInstrumentIdentity(byte[] descriptors, int targetInterface, bool santroller, out int flags)
        {
            const byte xusbDescriptorType = 0x21;
            const byte xusbDescriptorLength = 17;
            const ushort xusbVersion = 0x0110;
            const byte xusbSubclass = 0x5D;
            const byte wiredProtocol = 0x01;
            flags = 0;

            using (var stream = new MemoryStream(descriptors, false))
            using (var reader = new BinaryReader(stream))
            {
                bool selected = false;
                while (stream.Position < stream.Length)
                {
                    if (stream.Length - stream.Position < 2)
                        throw new IOException("Incomplete USB descriptor header.");
                    byte length = reader.ReadByte();
                    var type = (libusb_descriptor_type)reader.ReadByte();
                    long end = stream.Position + length - 2;
                    if (length < 2 || end > stream.Length)
                        throw new IOException("Invalid USB descriptor length.");

                    if (type == libusb_descriptor_type.CONFIG)
                        selected = false;
                    else if (type == libusb_descriptor_type.INTERFACE)
                    {
                        if (length < 9)
                            throw new IOException("Incomplete USB interface descriptor.");
                        byte number = reader.ReadByte();
                        byte alternate = reader.ReadByte();
                        reader.ReadByte(); // bNumEndpoints
                        var deviceClass = (libusb_class_code)reader.ReadByte();
                        byte subclass = reader.ReadByte();
                        byte protocol = reader.ReadByte();
                        selected = number == targetInterface && alternate == 0 &&
                            (santroller
                                ? (byte)deviceClass == 3 && subclass == 0 && protocol == 0
                                : deviceClass == libusb_class_code.VENDOR_SPEC &&
                                    subclass == xusbSubclass && protocol == wiredProtocol);
                    }
                    else if (selected && santroller && (byte)type == 0xFF && length == 9)
                    {
                        if (reader.ReadUInt32() == 0x544E4153 && reader.ReadByte() == 2)
                        {
                            byte subType = reader.ReadByte();
                            if (subType == 0)
                                throw new IOException("Invalid Santroller instrument subtype.");
                            flags = reader.ReadByte();
                            return subType;
                        }
                    }
                    else if (selected && !santroller && (byte)type == xusbDescriptorType && length == xusbDescriptorLength)
                    {
                        ushort version = reader.ReadUInt16();
                        if (version == xusbVersion)
                            return reader.ReadByte();
                    }
                    stream.Position = end;
                }
            }
            return 0;
        }

        private string FindHidraw(udev_device hid)
        {
            if (hid == null)
                return null;
            using (var scan = m_Udev.enumerate_new())
            {
                if (scan.IsInvalid || scan.add_match_parent(hid) < 0 ||
                    scan.add_match_subsystem("hidraw") < 0 || scan.scan_devices() < 0)
                    throw new IOException("Failed to correlate evdev and hidraw nodes.");
                var entry = scan.get_list_entry();
                return entry.IsInvalid ? null : "/dev/" + Path.GetFileName(entry.get_name());
            }
        }

        private void WarnOnce(string path, string message)
        {
            if (m_Warned.Add(path))
                Logging.Warning(message);
        }

        protected override EvdevDevice OnDeviceAdded(InputDevice device, IDisposable pending)
        {
            var context = (DeviceContext)pending;
            var result = new EvdevDevice(this, device, context.handle, context.path, context.claim, context.axes);
            context.transferred = true;
            Logging.Verbose($"Using evdev fallback for {device.description.product} at {context.path}");
            return result;
        }

        protected override void OnDeviceRemoved(EvdevDevice device)
        {
            device.Dispose();
            m_Pending.Remove(device.path);
        }

        protected override unsafe long? OnDeviceCommand(EvdevDevice device, InputDeviceCommand* command)
        {
            if (command->type == RequestSyncCommand.Type)
            {
                device.RequestSync();
                return InputDeviceCommand.GenericSuccess;
            }
            return InputDeviceCommand.GenericFailure;
        }
    }
}
#endif
