using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using HIDrogen.Imports;
using HIDrogen.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend
{
    using static HidApi;

    /// <summary>
    /// Provides input through the hidapi library.
    /// </summary>
    internal partial class HidApiBackend : CustomInputBackend<HidApiDevice>
    {
        private class DeviceAddContext
        {
            public string path;
            public int inputPrependCount;
            public HID.HIDDeviceDescriptor descriptor;
        }

        public const string InterfaceName = "HID";
        public static readonly FourCC InputFormat = new FourCC('H', 'I', 'D');
        public static readonly FourCC OutputFormat = new FourCC('H', 'I', 'D', 'O');

        // Get built-in HID descriptor parsing so we don't have to implement our own
        private unsafe delegate bool HIDParser_ParseReportDescriptor(byte[] buffer, ref HID.HIDDeviceDescriptor deviceDescriptor);
        private static readonly HIDParser_ParseReportDescriptor s_ParseReportDescriptor = (HIDParser_ParseReportDescriptor)
            Assembly.GetAssembly(typeof(HID))
            .GetType("UnityEngine.InputSystem.HID.HIDParser")
            .GetMethod("ParseReportDescriptor",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                new Type[] { typeof(byte).MakeArrayType(), typeof(HID.HIDDeviceDescriptor).MakeByRefType() }, null)
            .CreateDelegate(typeof(HIDParser_ParseReportDescriptor));

        private readonly Thread m_EnumerationThread;
        private readonly EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        // This lookup is used by the enumeration thread in addition to the main thread
        private readonly ConcurrentDictionary<string, HidApiDevice> m_DevicesByPath
            = new ConcurrentDictionary<string, HidApiDevice>();

        public HidApiBackend()
        {
            // Initialize hidapi
            int result = hid_init();
            if (result < 0)
            {
                Logging.InteropError("Failed to initialize hidapi");
                throw new Exception("Failed to initialize hidapi!");
            }

            // Initialize platform-specific resources
            PlatformInitialize();

            // Start threads
            m_EnumerationThread = new Thread(DeviceDiscoveryThread) { IsBackground = true };
            m_EnumerationThread.Start();
        }

        protected override void OnDispose()
        {
            // Stop threads
            m_ThreadStop.Set();
            m_EnumerationThread.Join();

            // Clean up platform-specific resources
            PlatformDispose();

            // Free hidapi
            int result = hid_exit();
            if (result < 0)
                Logging.InteropError("Error when freeing hidapi");
        }

        private void DeviceDiscoveryThread()
        {
            // Initial device enumeration
            EnumerateDevices();

            // Try using platform monitoring first
            int errorCount = 0;
            const int errorThreshold = 3;
            while (errorCount < errorThreshold && !m_ThreadStop.WaitOne(5000))
            {
                if (!PlatformMonitor())
                {
                    errorCount++;
                    Logging.Error($"Device monitoring failed! {(errorCount < errorThreshold ? "Trying again" : "Falling back to periodic re-enumeration of hidapi")}");
                }
            }

            // Fall back to just periodically enumerating hidapi
            while (!m_ThreadStop.WaitOne(2000))
            {
                EnumerateDevices();
            }
        }

        private void EnumerateDevices()
        {
            Logging.Verbose("Enumerating hidapi devices");
            foreach (var info in hid_enumerate())
            {
                if (!m_DevicesByPath.ContainsKey(info.path) &&
                    MakeDeviceDescription(info, out var description, out var descriptor, out int inputPrependCount))
                {
                    Logging.Verbose($"Found new device, VID/PID: {info.vendorId:X4}:{info.productId:X4}, path: {info.path}");
                    QueueDeviceAdd(description, new DeviceAddContext()
                    {
                        path = info.path,
                        inputPrependCount = inputPrependCount,
                        descriptor = descriptor,
                    });
                }
            }
        }

        protected override HidApiDevice OnDeviceAdded(InputDevice device, object _context)
        {
            var context = (DeviceAddContext)_context;

            var hidDevice = new HidApiDevice(this, context.path, device, context.descriptor, context.inputPrependCount);
            m_DevicesByPath.TryAdd(context.path, hidDevice);
            return hidDevice;
        }

        protected override void OnDeviceRemoved(HidApiDevice device)
        {
            m_DevicesByPath.TryRemove(device.path, out _);
            device.Dispose();
        }

        private unsafe bool MakeDeviceDescription(
            in hid_device_info info,
            out InputDeviceDescription description,
            out HID.HIDDeviceDescriptor descriptor,
            out int inputPrependCount
        )
        {
            description = default;
            inputPrependCount = default;
            descriptor = default;

            // Get descriptor
            if (!PlatformGetDescriptor(info.path, out var descriptorBytes))
            {
                Logging.InteropError("Error getting descriptor");
                return false;
            }

            // Parse descriptor
            descriptor = new HID.HIDDeviceDescriptor()
            {
                vendorId = info.vendorId,
                productId = info.productId,
                // These are set by the parsing routine below
                // hidapi's values aren't always guaranteed to be accurate regardless,
                // versions below v0.10.1 don't parse it out
                // usagePage = (HID.UsagePage)info.usagePage,
                // usage = info.usage
            };

            if (!s_ParseReportDescriptor(descriptorBytes, ref descriptor))
            {
                Logging.Error("Could not get descriptor for device!");
                return false;
            }

            // Ignore unsupported usages
            HID.UsagePage usagePage = descriptor.usagePage;
            int usage = descriptor.usage;
            if (!HIDSupport.supportedHIDUsages.Any((u) => usagePage == u.page && usage == u.usage))
            {
                Logging.Verbose($"Device has unsupported usage {(int)descriptor.usagePage}:{descriptor.usage}, ignoring.");
                return false;
            }

            // Fix up report sizes
            // The parser doesn't actually set the report lengths unfortunately, we need to fix them up ourselves
            FixupReportLengths(info, ref descriptor, out inputPrependCount);
            if (descriptor.inputReportSize < 1)
            {
                Logging.Verbose("Device has no input report data, ignoring.");
                return false;
            }

            // Check against safety limit
            if (descriptor.inputReportSize > kMaxStateSize)
            {
                Logging.Warning("Device has an excessive amount of input data, ignoring for safety.");
                return false;
            }

            // Version may need to be fixed up as well
            ushort version = info.releaseBcd;
            if (version == 0 && PlatformGetVersionNumber(info.path, out ushort fixedVersion))
                version = fixedVersion;

            // Create final description
            description = new InputDeviceDescription()
            {
                interfaceName = InterfaceName,
                manufacturer = info.manufacturerName,
                product = info.productName,
                serial = info.serialNumber,
                version = version.ToString(),
                capabilities = JsonUtility.ToJson(descriptor)
            };

            return true;
        }

        private static unsafe void FixupReportLengths(
            in hid_device_info info,
            ref HID.HIDDeviceDescriptor descriptor,
            out int inputPrependCount)
        {
            inputPrependCount = 0;

            int inputSizeBits = 0;
            int outputSizeBits = 0;
            int featureSizeBits = 0;
#if HIDROGEN_FORCE_REPORT_IDS
            // We also need to account for the case where there's no input report ID
            // No elements are provided for the report ID itself, so if any have an offset
            // less than 8 we know there's no report ID
            int inputStartOffsetBits = 8;
#endif
            foreach (var element in descriptor.elements)
            {
                int offsetBits = element.reportOffsetInBits;
                int sizeBits = element.reportOffsetInBits + element.reportSizeInBits;
                switch (element.reportType)
                {
                    case HID.HIDReportType.Input:
                        if (inputSizeBits < sizeBits)
                            inputSizeBits = sizeBits;
#if HIDROGEN_FORCE_REPORT_IDS
                        if (inputStartOffsetBits > offsetBits)
                            inputStartOffsetBits = offsetBits;
#endif
                        break;

                    case HID.HIDReportType.Output:
                        if (outputSizeBits < sizeBits)
                            outputSizeBits = sizeBits;
                        break;

                    case HID.HIDReportType.Feature:
                        if (featureSizeBits < sizeBits)
                            featureSizeBits = sizeBits;
                        break;
                }
            }

            // Turn bit size into byte size, ensuring sizes are normalized to byte boundaries
            descriptor.inputReportSize = inputSizeBits.AlignToMultipleOf(8) / 8;
            descriptor.outputReportSize = outputSizeBits.AlignToMultipleOf(8) / 8;
            descriptor.featureReportSize = featureSizeBits.AlignToMultipleOf(8) / 8;

#if HIDROGEN_FORCE_REPORT_IDS
            // Fix up offsets and set prepend count, such that there always is a report ID byte
            if (inputStartOffsetBits < 8)
            {
                descriptor.inputReportSize += 1;
                inputPrependCount = 1;
                for (int i = 0; i < descriptor.elements.Length; i++)
                {
                    var element = descriptor.elements[i];
                    if (element.reportType != HID.HIDReportType.Input)
                        continue;

                    element.reportOffsetInBits += 8;
                    descriptor.elements[i] = element;
                }
            }
#endif
        }

        public unsafe void QueueStateEvent(InputDevice device, void* stateBuffer, int stateLength)
            => QueueStateEvent(device, InputFormat, stateBuffer, stateLength);

        protected override unsafe long? OnDeviceCommand(HidApiDevice device, InputDeviceCommand* command)
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
            if (command->type == HID.QueryHIDReportDescriptorSizeDeviceCommandType)
                return device.GetReportDescriptorSize();
            if (command->type == HID.QueryHIDReportDescriptorDeviceCommandType)
                return device.GetReportDescriptor(command->payloadPtr, command->payloadSizeInBytes);
            if (command->type == HID.QueryHIDParsedReportDescriptorDeviceCommandType)
                return device.GetParsedReportDescriptor(command->payloadPtr, command->payloadSizeInBytes);

            return null;
        }
    }
}