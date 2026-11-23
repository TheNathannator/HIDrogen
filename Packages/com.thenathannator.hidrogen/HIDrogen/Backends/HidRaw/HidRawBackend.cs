#if UNITY_STANDALONE_LINUX
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using HIDrogen.LowLevel;
using HIDrogen.Services.Linux;
using HIDrogen.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend.Linux
{
    internal class HidRawBackend : CustomInputBackend<HidRawDevice, HidRawBackend.AddContext>,
        IUdevBackend
    {
        private struct DeviceInfo
        {
            public string manufacturer;
            public string product;
            public string serial;

            public ushort vendorId;
            public ushort productId;
            public ushort revision;
        }

        internal struct AddContext : IDisposable
        {
            public string syspath;
            public string hidrawPath;
            public HID.HIDDeviceDescriptor descriptor;

            public void Dispose()
            {
            }
        }

        public const string InterfaceName = "HID";
        public static readonly FourCC InputFormat = new FourCC('H', 'I', 'D');
        public static readonly FourCC OutputFormat = new FourCC('H', 'I', 'D', 'O');

        // Get built-in HID descriptor parsing so we don't have to implement our own
        private unsafe delegate bool HIDParser_ParseReportDescriptor(
            byte* bufferPtr,
            int bufferLength,
            ref HID.HIDDeviceDescriptor deviceDescriptor
        );

        private static readonly HIDParser_ParseReportDescriptor s_ParseReportDescriptor =
            (HIDParser_ParseReportDescriptor)
            Assembly.GetAssembly(typeof(HID))
            .GetType("UnityEngine.InputSystem.HID.HIDParser")
            .GetMethod(
                "ParseReportDescriptor",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new Type[]
                {
                    typeof(byte).MakePointerType(),
                    typeof(int),
                    typeof(HID.HIDDeviceDescriptor).MakeByRefType()
                },
                null
            )
            .CreateDelegate(typeof(HIDParser_ParseReportDescriptor));

        private readonly Dictionary<string, List<HidRawDevice>> m_DevicesBySyspath
            = new Dictionary<string, List<HidRawDevice>>();

        protected override void OnDispose()
        {
        }

        protected override void OnStop()
        {
            base.OnStop();

            foreach (var p in m_DevicesBySyspath)
            {
                foreach (var device in p.Value)
                {
                    device?.Dispose();
                }
            }
            m_DevicesBySyspath.Clear();
        }

        protected override HidRawDevice OnDeviceAdded(InputDevice device, AddContext context)
        {
            var backendDevice = new HidRawDevice(this, context, device);

            if (!m_DevicesBySyspath.TryGetValue(context.syspath, out var devices))
            {
                devices = new List<HidRawDevice>();
                m_DevicesBySyspath.Add(context.syspath, devices);
            }
            devices.Add(backendDevice);

            return backendDevice;
        }

        protected override void OnDeviceRemoved(HidRawDevice device)
        {
            if (m_DevicesBySyspath.TryGetValue(device.syspath, out var devices))
            {
                devices.Remove(device);
                if (devices.Count < 1)
                {
                    m_DevicesBySyspath.Remove(device.syspath);
                }
            }

            device.Dispose();
        }

        protected override unsafe long? OnDeviceCommand(HidRawDevice device, InputDeviceCommand* command)
        {
            // TODO
            // System commands
            // if (command->type == EnableDeviceCommand.Type)
            //     return Enable(command);
            // if (command->type == DisableDeviceCommand.Type)
            //     return Disable(command);
            // if (command->type == QueryEnabledStateCommand.Type)
            //     return IsEnabled(command);
            // if (command->type == RequestSyncCommand.Type)
            //     return SyncState(command);
            // if (command->type == RequestResetCommand.Type)
            //     return ResetState(command);
            // if (command->type == QueryCanRunInBackground.Type)
            //     return CanRunInBackground(command);

            // Reports
            if (command->type == OutputFormat)
                return device.SendOutput(command->payloadPtr, command->payloadSizeInBytes);
            // These don't have any (documented, at least) format codes
            // if (command->type == HidDefinitions.GetFeatureFormat)
            //     return GetFeature(command->payloadPtr, command->payloadSizeInBytes);
            // if (command->type == HidDefinitions.SetFeatureFormat)
            //     return SetFeature(command->payloadPtr, command->payloadSizeInBytes);

            // Descriptors
            // if (command->type == HID.QueryHIDReportDescriptorSizeDeviceCommandType)
            //     return device.GetReportDescriptorSize();
            // if (command->type == HID.QueryHIDReportDescriptorDeviceCommandType)
            //     return device.GetReportDescriptor(command->payloadPtr, command->payloadSizeInBytes);
            if (command->type == HID.QueryHIDParsedReportDescriptorDeviceCommandType)
                return device.GetParsedReportDescriptor(command->payloadPtr, command->payloadSizeInBytes);

            return null;
        }

        public bool ProbeDeviceAdd(udev_context udev, udev_device device)
        {
            using (var hidraw = FindSibling(udev, device, Udev.SubsystemHidRaw))
            {
                if (hidraw == null || hidraw.IsInvalid)
                {
                    return false;
                }

                string hidrawPath = hidraw.get_devnode().ToString();
                if (hidrawPath == null)
                {
                    return false;
                }

                string syspath = hidraw.get_syspath().ToString();
                if (m_DevicesBySyspath.ContainsKey(syspath))
                {
                    // Signal claimage so that this device is not considered by other backends
                    return true;
                }

                // Test whether the device can even be opened, as hidraw requires either special permissions
                // or for udev rules to be set to allow unpriveleged access
                using (var fd = Fcntl.open(hidrawPath, Fcntl.O_RDWR | Fcntl.O_CLOEXEC))
                {
                    if (fd == null || fd.IsInvalid)
                    {
                        // Ignore permission errors in logs
                        if (Posix.errno != Errno.EACCES && Posix.errno != Errno.EPERM)
                        {
                            Logging.LastError("Failed to open hidraw device");
                        }
                        return false;
                    }
                }

                if (!CollectDeviceInfo(udev, hidraw, out var deviceInfo) ||
                    !ParseReportDescriptor(hidraw, out var descriptor))
                {
                    return false;
                }

                // Each top-level collection should logically be treated as its own device
                for (int i = 0; i < descriptor.collections.Length; i++)
                {
                    if (!SplitCollection(deviceInfo, descriptor, i, out var collectionDescriptor))
                    {
                        continue;
                    }

                    var description = new InputDeviceDescription()
                    {
                        interfaceName = InterfaceName,
                        manufacturer = deviceInfo.manufacturer,
                        product = deviceInfo.product,
                        serial = deviceInfo.serial,
                        version = deviceInfo.revision.ToString(CultureInfo.InvariantCulture),
                        // capabilities = collectionDescriptor.ToJson(), // this will pretty-print, wasting memory
                        capabilities = JsonUtility.ToJson(collectionDescriptor),
                    };
                    var context = new AddContext()
                    {
                        syspath = syspath,
                        hidrawPath = hidrawPath,
                        descriptor = collectionDescriptor,
                    };

                    AddDeviceNow(description, context);
                }

                if (!m_DevicesBySyspath.ContainsKey(syspath))
                {
                    // Device addition failed
                    return false;
                }

                Logging.Verbose($"hidraw device {hidrawPath} added");
                return true;
            }
        }

        public void ProbeDeviceRemove(udev_context udev, udev_device device)
        {
            using (var hidraw = FindSibling(udev, device, Udev.SubsystemHidRaw))
            {
                if (hidraw == null || hidraw.IsInvalid)
                {
                    return;
                }

                string syspath = device.get_syspath().ToString();
                string hidrawPath = hidraw.get_devnode().ToString();
                if (syspath != null &&
                    hidrawPath != null &&
                    m_DevicesBySyspath.TryGetValue(syspath, out var backendDevices))
                {
                    foreach (var dev in backendDevices)
                    {
                        if (dev != null)
                        {
                            QueueDeviceRemove(dev.device);
                            dev.Dispose();
                        }
                    }

                    backendDevices.Clear();
                    m_DevicesBySyspath.Remove(syspath);
                    Logging.Verbose($"hidraw device {hidrawPath} removed");
                }
            }
        }

        private static udev_device FindSibling(udev_context udev, udev_device device, string siblingSubsystem)
        {
            if (device.get_subsystem().ToString() == siblingSubsystem)
            {
                // Clone to simplify disposal handling
                return device.add_ref();
            }

            using (var hidRoot = device.get_parent_with_subsystem_devtype(Udev.SubsystemHid, null))
            {
                if (hidRoot == null || hidRoot.IsInvalid)
                {
                    return null;
                }

                return Udev.FindChildBySubsystem(udev, hidRoot, siblingSubsystem);
            }
        }

        private static bool CollectDeviceInfo(udev_context udev, udev_device hidraw, out DeviceInfo deviceInfo)
        {
            // Heavily referenced from hidapi, as it is a time-tested implementation
            // and I don't know enough about Linux device properties to derive all of this myself

            deviceInfo = new DeviceInfo();

            UTF8String hid_id;
            using (var hidRoot = hidraw.get_parent_with_subsystem_devtype(Udev.SubsystemHid, null))
            {
                if (hidRoot == null || hidRoot.IsInvalid)
                {
                    return false;
                }

                hid_id = hidRoot.get_property_value("HID_ID");
                var hid_name = hidRoot.get_property_value("HID_NAME");
                var hid_uniq = hidRoot.get_property_value("HID_UNIQ");
                var hid_firmware_version = hidRoot.get_property_value("HID_FIRMWARE_VERSION");
                if (hid_id.IsInvalid)
                {
                    // IDs are vital for identification
                    return false;
                }

                deviceInfo.product = hid_name.ToString();
                deviceInfo.serial = hid_uniq.ToString();
                if (!hid_firmware_version.IsInvalid)
                {
                    deviceInfo.revision = (ushort)NumberUtils.ParseHexU32(hid_firmware_version.ToString(), 0);
                }
            }

            //  bus  vendor   product
            // "0003:000005AC:00008242"
            var splits = hid_id.ToString().Split(':');
            if (splits == null ||
                splits.Length < 3 ||
                !NumberUtils.TryParseHexU32(splits[0], out uint _busType) ||
                !NumberUtils.TryParseHexU32(splits[1], out uint vendorId) ||
                !NumberUtils.TryParseHexU32(splits[2], out uint productId)
            )
            {
                Logging.Verbose($"Encountered malformed HID_ID {hid_id.ToString()}");
                return false;
            }

            var busType = (input_bus_type)_busType;
            deviceInfo.vendorId = (ushort)vendorId;
            deviceInfo.productId = (ushort)productId;

            // Bus-specific information fixups
            switch (busType)
            {
                case input_bus_type.USB:
                {
                    using (var usb_device = hidraw.get_parent_with_subsystem_devtype(Udev.SubsystemUsb, Udev.DevtypeUsbDevice))
                    {
                        if (usb_device == null || usb_device.IsInvalid)
                        {
                            Logging.Verbose("Could not find usb_device node for USB HID device");
                            // Continue onwards with the info we already have
                            break;
                        }

                        deviceInfo.manufacturer = usb_device.get_sysattr_value("manufacturer").ToString();
                        deviceInfo.product = usb_device.get_sysattr_value("product").ToString();

                        string bcdDevice = usb_device.get_sysattr_value("bcdDevice").ToString();
                        if (NumberUtils.TryParseHexU32(bcdDevice, out uint revision))
                        {
                            deviceInfo.revision = (ushort)revision;
                        }
                        if (deviceInfo.vendorId == 0x1209 && deviceInfo.productId == 0x2883)
                        {
                            using (var usb_interface = hidraw.get_parent_with_subsystem_devtype(Udev.SubsystemUsb, Udev.DevtypeUsbInterface))
                            {
                                if (usb_interface == null || usb_interface.IsInvalid)
                                {
                                    Logging.Verbose("Could not find usb_interface node for USB HID device");
                                    break;
                                }
                                // Santroller 2 stores a user-configurable profile name here
                                var interface_attr = usb_interface.get_sysattr_value("interface");
                                if (!interface_attr.IsInvalid)
                                {
                                    deviceInfo.product = interface_attr.ToString();
                                }
                            }
                        }
                    }
                    break;
                }
                case input_bus_type.BLUETOOTH:
                {
                    using (var input = FindSibling(udev, hidraw, Udev.SubsystemInput))
                    {
                        if (input == null || input.IsInvalid)
                        {
                            Logging.Verbose("Could not find input node for Bluetooth HID device");
                            // Continue onwards with the info we already have
                            break;
                        }

                        string id_version = input.get_sysattr_value("id/version").ToString();
                        if (NumberUtils.TryParseHexU32(id_version, out uint revision))
                        {
                            deviceInfo.revision = (ushort)revision;
                        }
                    }
                    break;
                }
            }

            return true;
        }

        private static unsafe bool ParseReportDescriptor(
            udev_device hidraw,
            out HID.HIDDeviceDescriptor descriptor
        )
        {
            descriptor = new HID.HIDDeviceDescriptor();

            try
            {
                // Read from sysfs to avoid the need to open the hidraw device
                string descriptorPath = Path.Combine(hidraw.get_syspath().ToString(), "device", "report_descriptor");

                using (var fd = Fcntl.open(descriptorPath, Fcntl.O_RDONLY | Fcntl.O_CLOEXEC))
                {
                    if (fd == null || fd.IsInvalid)
                    {
                        Logging.LastError("Failed to open report descriptor file");
                        return false;
                    }

                    var descriptorBytes = new hidraw_report_descriptor();
                    int bytesRead = Fcntl.read(fd, descriptorBytes.value, hidraw_report_descriptor.MAX_DESCRIPTOR_SIZE);
                    if (bytesRead < 0)
                    {
                        Logging.LastError("Failed to read report descriptor file");
                        return false;
                    }

                    return s_ParseReportDescriptor(descriptorBytes.value, bytesRead, ref descriptor);
                }
            }
            catch (Exception ex)
            {
                Logging.Exception("Failed to parse report descriptor", ex);
                return false;
            }
        }

        private static bool SplitCollection(
            in DeviceInfo deviceInfo,
            in HID.HIDDeviceDescriptor rootDescriptor,
            int collectionIndex,
            out HID.HIDDeviceDescriptor collectionDescriptor
        )
        {
            collectionDescriptor = default;

            if ((uint)collectionIndex >= (uint)rootDescriptor.collections.Length)
            {
                return false;
            }

            var topCollection = rootDescriptor.collections[collectionIndex];

            // Child collections are not separate devices
            if (topCollection.parent >= 0)
            {
                return false;
            }

            // Ignore unsupported usages
            bool supported = false;
            foreach (var u in HIDSupport.supportedHIDUsages)
            {
                if (u.page == topCollection.usagePage && u.usage == topCollection.usage)
                {
                    supported = true;
                    break;
                }
            }
            if (!supported)
            {
                return false;
            }

            // Copy just the top-level collection and its children to a new descriptor
            var elements = new List<HID.HIDElementDescriptor>();
            var collections = new List<HID.HIDCollectionDescriptor>();
            CopyCollection(rootDescriptor, topCollection, collectionIndex, -1);
            void CopyCollection(
                in HID.HIDDeviceDescriptor root,
                HID.HIDCollectionDescriptor collection,
                int originalIndex,
                int newParent
            )
            {
                int newIndex = collections.Count;

                int newFirstChild = elements.Count;
                for (int i = collection.firstChild; i < collection.firstChild + collection.childCount; i++)
                {
                    // safety check
                    if (i >= root.elements.Length)
                    {
                        break;
                    }

                    var element = root.elements[i];
                    if (element.collectionIndex == originalIndex)
                    {
                        element.collectionIndex = newIndex;
                        elements.Add(element);
                    }
                }

                collection.parent = newParent;
                collection.firstChild = newFirstChild;
                collection.childCount = elements.Count - newFirstChild;

                collections.Add(collection);

                for (int i = 0; i < root.collections.Length; i++)
                {
                    ref readonly var other = ref root.collections[i];
                    if (other.parent == originalIndex)
                    {
                        CopyCollection(root, other, i, newIndex);
                    }
                }
            }

            collectionDescriptor = new HID.HIDDeviceDescriptor
            {
                vendorId = deviceInfo.vendorId,
                productId = deviceInfo.productId,
                usagePage = topCollection.usagePage,
                usage = topCollection.usage,

                collections = collections.ToArray(),
                elements = elements.ToArray()
            };

            // Descriptor parsing does not set report sizes, so we must set them ourselves
            int inputSizeBits = 0;
            int outputSizeBits = 0;
            int featureSizeBits = 0;
            foreach (var element in collectionDescriptor.elements)
            {
                int sizeBits = element.reportOffsetInBits + element.reportSizeInBits;
                switch (element.reportType)
                {
                    case HID.HIDReportType.Input:
                    {
                        inputSizeBits = Math.Max(inputSizeBits, sizeBits);
                        break;
                    }
                    case HID.HIDReportType.Output:
                    {
                        outputSizeBits = Math.Max(outputSizeBits, sizeBits);
                        break;
                    }
                    case HID.HIDReportType.Feature:
                    {
                        featureSizeBits = Math.Max(featureSizeBits, sizeBits);
                        break;
                    }
                }
            }

            // Turn bit size into byte size, ensuring sizes are normalized to byte boundaries
            collectionDescriptor.inputReportSize = NumberUtils.AlignToMultiple(inputSizeBits, 8) / 8;
            collectionDescriptor.outputReportSize = NumberUtils.AlignToMultiple(outputSizeBits, 8) / 8;
            collectionDescriptor.featureReportSize = NumberUtils.AlignToMultiple(featureSizeBits, 8) / 8;

            // Reject non-input devices
            if (collectionDescriptor.inputReportSize < 1)
            {
                return false;
            }

            return true;
        }
    }
}
#endif
