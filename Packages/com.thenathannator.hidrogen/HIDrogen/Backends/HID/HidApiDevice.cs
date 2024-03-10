using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using HIDrogen.Imports;
using HIDrogen.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend
{
    using static HidApi;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    using static Libc;
    using static HidRaw;
    using static Udev;
#endif

    /// <summary>
    /// An hidapi device.
    /// </summary>
    internal class HidApiDevice : IDisposable
    {
        // Get built-in HID descriptor parsing so we don't have to implement our own
        private unsafe delegate bool HIDParser_ParseReportDescriptor(byte* bufferPtr, int bufferLength,
            ref HID.HIDDeviceDescriptor deviceDescriptor);
        private static readonly HIDParser_ParseReportDescriptor s_ParseReportDescriptor = (HIDParser_ParseReportDescriptor)
            Assembly.GetAssembly(typeof(HID)).GetType("UnityEngine.InputSystem.HID.HIDParser")
            .GetMethod("ParseReportDescriptor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                new Type[] { typeof(byte).MakePointerType(), typeof(int), typeof(HID.HIDDeviceDescriptor).MakeByRefType() }, null)
            .CreateDelegate(typeof(HIDParser_ParseReportDescriptor));

        private const int kRetryThreshold = 3; // Max allowed number of consecutive errors

        // Format codes
        public const string InterfaceName = "HID";
        public static readonly FourCC InputFormat = new FourCC('H', 'I', 'D');
        public static readonly FourCC OutputFormat = new FourCC('H', 'I', 'D', 'O');

        private readonly hid_device_info m_Info;
        private hid_device m_Handle;
        private InputDevice m_Device;
        private HID.HIDDeviceDescriptor m_Descriptor;

        private readonly byte[] m_ReadBuffer;
        private readonly int m_PrependCount; // Number of bytes to prepend to the queued input buffer

        private int m_ErrorCount; // Number of consecutive errors encountered during reads

        public string path => m_Info.path;
        public int deviceId => m_Device?.deviceId ?? InputDevice.InvalidDeviceId;

        private HidApiDevice(hid_device_info info, hid_device handle, InputDevice device, HID.HIDDeviceDescriptor descriptor,
            int inputPrependCount)
        {
            int realInputSize = descriptor.inputReportSize - inputPrependCount;
            m_Info = info;
            m_Handle = handle;
            m_Device = device;
            m_Descriptor = descriptor;
            m_ReadBuffer = new byte[realInputSize];
            m_PrependCount = inputPrependCount;
            m_PrependCount = 0;

            HidApiBackend.LogVerbose($"Created new device '{device}' with report size of {realInputSize} and prepend count of {inputPrependCount}");
        }

        ~HidApiDevice()
        {
            Dispose(false);
        }

        public static HidApiDevice TryCreate(hid_device_info info)
        {
            // Open device
            var handle = hid_open_path(info.path);
            if (handle == null || handle.IsInvalid)
            {
                HidApiBackend.LogInteropError($"Error when opening HID device path '{info.path}': {hid_error()} ({{0}})");
                return null;
            }

            // Get descriptor
            if (!GetReportDescriptor(info, out var descriptor, out int inputPrependCount))
            {
                HidApiBackend.LogError($"Could not get descriptor for device! VID/PID: {info.vendorId:X4}:{info.productId:X4}, path: {info.path}");
                handle.Dispose();
                return null;
            }

            // hidapi's usage values are only valid in v0.10.1 and greater
            info.usagePage = (ushort)descriptor.usagePage;
            info.usage = (ushort)descriptor.usage;

            // Ignore unsupported usages
            // This check must be done after descriptor parsing since hidapi v0.10.0 and earlier don't parse it
            if (!HIDSupport.supportedHIDUsages.Any((usage) =>
                usage.page == descriptor.usagePage && usage.usage == descriptor.usage))
            {
                HidApiBackend.LogVerbose($"Found device with unsupported usage page {descriptor.usagePage} and usage {descriptor.usage}, ignoring. VID/PID: {info.vendorId:X4}:{info.productId:X4}, path: {info.path}");
                handle.Dispose();
                return null;
            }

            if (info.releaseBcd == 0)
                FixupVersionNumber(ref info);

            // Create input device description and add it to the system
            var description = new InputDeviceDescription()
            {
                interfaceName = InterfaceName,
                manufacturer = info.manufacturerName,
                product = info.productName,
                serial = info.serialNumber,
                version = info.releaseBcd.ToString(),
                capabilities = JsonUtility.ToJson(descriptor)
            };

            // The input system will throw if a device layout can't be found
            InputDevice device;
            try
            {
                device = InputSystem.AddDevice(description);
            }
            catch (Exception ex)
            {
                HidApiBackend.LogError($"Failed to add device to the system! Description:\n{description.ToJson()}");
                Debug.LogException(ex);
                handle.Dispose();
                return null;
            }

            return new HidApiDevice(info, handle, device, descriptor, inputPrependCount);
        }

        private static unsafe bool GetReportDescriptor(hid_device_info info, out HID.HIDDeviceDescriptor descriptor,
            out int inputPrependCount)
        {
            byte* data = null;
            int dataLength = 0;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            var fd = open(info.path, O_RDONLY);
            if (fd == null || fd.IsInvalid)
            {
                descriptor = default;
                inputPrependCount = 0;
                return false;
            }

            hidraw_report_descriptor buffer;
            using (fd)
            {
                if (!GetHidRawReportDescriptor(fd, out buffer))
                {
                    HidApiBackend.LogError($"Error getting descriptor: {errno}");
                    descriptor = default;
                    inputPrependCount = 0;
                    return false;
                }
            }

            data = buffer.value;
            dataLength = buffer.size;
#endif

            return ParseReportDescriptor(info, data, dataLength, out descriptor, out inputPrependCount);
        }

        internal static unsafe bool ParseReportDescriptor(hid_device_info info, byte* data, int length,
            out HID.HIDDeviceDescriptor descriptor, out int inputPrependCount)
        {
            descriptor = new HID.HIDDeviceDescriptor()
            {
                vendorId = info.vendorId,
                productId = info.productId,
                usagePage = (HID.UsagePage)info.usagePage,
                usage = info.usage
            };
            inputPrependCount = 0;

            if (data == null || length < 1 || !s_ParseReportDescriptor(data, length, ref descriptor))
                return false;

            // The parser doesn't actually set the report lengths unfortunately, we need to fix them up ourselves
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

                    default:
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

            return descriptor.inputReportSize > 0; // Output and feature reports aren't required for normal operation
        }

        private static void FixupVersionNumber(ref hid_device_info info)
        {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            // Bluetooth devices won't have this filled in so we need to do it ourselves.
            // Use statx to grab the device type and number, and then construct a udev device
            if (statx(0, info.path, 0, 0, out var pathStats) < 0)
            {
                HidApiBackend.LogError($"Error getting device type info: {errno}");
                return;
            }

            var udev = udev_new();
            if (udev == null || udev.IsInvalid)
            {
                HidApiBackend.LogError($"Failed to initialize udev context: {errno}");
                return;
            }

            using (udev)
            {
                var hidrawUdevDevice = udev_device_new_from_devnum(udev, pathStats.DeviceType, pathStats.DeviceNumber);
                if (hidrawUdevDevice == null || hidrawUdevDevice.IsInvalid)
                {
                    HidApiBackend.LogError($"Failed to get hidraw device instance: {errno}");
                    return;
                }

                // Grab the root parent hid device that both the hidraw and input devices share
                var hidUdevDevice = udev_device_get_parent_with_subsystem_devtype(hidrawUdevDevice, "hid", null);
                if (hidUdevDevice == null || hidUdevDevice.IsInvalid)
                {
                    HidApiBackend.LogError($"Failed to get HID device instance: {errno}");
                    return;
                }

                using (hidUdevDevice)
                {
                    // Find the input device by scanning the parent's children for input devices, and grabbing the first one
                    var enumerate = udev_enumerate_new(udev);
                    if (enumerate == null || enumerate.IsInvalid)
                    {
                        HidApiBackend.LogError($"Failed to make udev enumeration: {errno}");
                        return;
                    }

                    using (enumerate)
                    {
                        // Scan for devices under the 'input' subsystem
                        if (udev_enumerate_add_match_parent(enumerate, hidUdevDevice) < 0 ||
                            udev_enumerate_add_match_subsystem(enumerate, "input") < 0 ||
                            udev_enumerate_scan_devices(enumerate) < 0)
                        {
                            HidApiBackend.LogError($"Failed to scan udev devices: {errno}");
                            return;
                        }

                        // Get the first device found
                        IntPtr entry;
                        string entryPath;
                        udev_device inputUdevDevice;
                        if ((entry = udev_enumerate_get_list_entry(enumerate)) == IntPtr.Zero ||
                            string.IsNullOrEmpty(entryPath = udev_list_entry_get_name(entry)) ||
                            (inputUdevDevice = udev_device_new_from_syspath(udev, entryPath)) == null ||
                            inputUdevDevice.IsInvalid)
                        {
                            HidApiBackend.LogError($"Failed to get input device instance: {errno}");
                            return;
                        }

                        // Grab the version number from the found device
                        using (inputUdevDevice)
                        {
                            string versionStr = udev_device_get_sysattr_value(inputUdevDevice, "id/version");
                            info.releaseBcd = Convert.ToUInt16(versionStr, 16);
                        }
                    }
                }
            }
#endif
        }

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        private static unsafe bool GetHidRawReportDescriptorLength(fd fd, out int length)
        {
            length = 0;
            if (fd == null || fd.IsInvalid)
                return false;

            fixed (int* ptr = &length)
                return ioctl(fd, HIDIOCGRDESCSIZE, ptr) >= 0;
        }

        private static unsafe bool GetHidRawReportDescriptor(fd fd, out hidraw_report_descriptor buffer)
        {
            buffer = default;
            if (fd == null || fd.IsInvalid)
                return false;

            if (!GetHidRawReportDescriptorLength(fd, out int length))
                return false;

            buffer.size = length;
            fixed (hidraw_report_descriptor* ptr = &buffer)
                return ioctl(fd, HIDIOCGRDESC, ptr) >= 0;
        }
#endif

        // Returns true on success, false if the device should be removed.
        public bool UpdateState()
        {
            // Ensure read buffer is valid
            if (m_ReadBuffer == null || m_ReadBuffer.Length < 1)
            {
                Debug.Assert(false, "Device without a read buffer!");
                return false;
            }

            // Get current state
            int result = hid_read_timeout(m_Handle, m_ReadBuffer, 0);
            if (result <= 0) // Error or no reports available
            {
                if (result < 0) // Error
                {
                    #if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                    if (errno == Errno.ENOENT) // Device has been disconnected
                        return false;
                    #endif

                    m_ErrorCount++;
                    HidApiBackend.LogInteropErrorVerbose($"hid_read: {hid_error(m_Handle)} ({{0}}) - Error count: {m_ErrorCount}");
                    return m_ErrorCount < kRetryThreshold;
                }
                m_ErrorCount = 0;
                return true;
            }
            m_ErrorCount = 0;

            // Queue state
            QueueState();
            return true;
        }

        public unsafe long? ExecuteCommand(InputDeviceCommand* command)
        {
            if (command == null)
                return InputDeviceCommand.GenericFailure;

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
                return SendOutput(command);
            // These don't have any (documented, at least) format codes
            // if (command->type == HidDefinitions.GetFeatureFormat)
            //     return GetFeature(command);
            // if (command->type == HidDefinitions.SetFeatureFormat)
            //     return SetFeature(command);

            // Descriptors
            if (command->type == HID.QueryHIDReportDescriptorSizeDeviceCommandType)
                return GetReportDescriptorSize(command);
            if (command->type == HID.QueryHIDReportDescriptorDeviceCommandType)
                return GetReportDescriptor(command);
            if (command->type == HID.QueryHIDParsedReportDescriptorDeviceCommandType)
                return GetParsedReportDescriptor(command);

            return null;
        }

        // Based on InputSystem.QueueStateEvent<T>
        private unsafe void QueueState()
        {
            // Get event size
            const int kMaxStateSize = 512; // TODO: Is this actually necessary? (InputSystem.StateEventBuffer.kMaxSize)
            var stateSize = m_ReadBuffer.Length + m_PrependCount;
            if (stateSize > kMaxStateSize)
            {
                HidApiBackend.LogError($"State buffer size ({stateSize}) exceeds maximum supported state size ({kMaxStateSize})");
                return;
            }
            int eventSize = UnsafeUtility.SizeOf<StateEvent>() - 1 + stateSize; // StateEvent already includes 1 byte at the end

            // Create state buffer
            byte* buffer = stackalloc byte[eventSize];
            StateEvent* stateBuffer = (StateEvent*)buffer;
            *stateBuffer = new StateEvent
            {
                baseEvent = new InputEvent(StateEvent.Type, eventSize, m_Device.deviceId),
                stateFormat = InputFormat
            };

            // Copy state into buffer
            fixed (byte* statePtr = m_ReadBuffer)
            {
                byte* bufferPtr = (byte*)stateBuffer->state;
                UnsafeUtility.MemCpy(bufferPtr + m_PrependCount, statePtr, stateSize);
            }

            // Queue state event
            var eventPtr = new InputEventPtr((InputEvent*)buffer);
            HidApiBackend.QueueEvent(eventPtr);
        }

        private unsafe long SendOutput(InputDeviceCommand* command)
        {
            if (command->payloadPtr == null || command->payloadSizeInBytes < 1)
                return InputDeviceCommand.GenericFailure;

            int result = hid_write(m_Handle, (byte*)command->payloadPtr, command->payloadSizeInBytes);
            return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
        }

        private unsafe long GetFeature(InputDeviceCommand* command)
        {
            if (command->payloadPtr == null || command->payloadSizeInBytes < 1)
                return InputDeviceCommand.GenericFailure;

            int result = hid_get_feature_report(m_Handle, (byte*)command->payloadPtr, command->payloadSizeInBytes);
            return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
        }

        private unsafe long SetFeature(InputDeviceCommand* command)
        {
            if (command->payloadPtr == null || command->payloadSizeInBytes < 1)
                return InputDeviceCommand.GenericFailure;

            int result = hid_send_feature_report(m_Handle, (byte*)command->payloadPtr, command->payloadSizeInBytes);
            return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
        }

        private unsafe long GetReportDescriptorSize(InputDeviceCommand* command)
        {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            var fd = open(m_Info.path, O_RDONLY);
            if (fd == null || fd.IsInvalid || !GetHidRawReportDescriptorLength(fd, out int size))
                return InputDeviceCommand.GenericFailure;

            // Expected return is the size of the descriptor
            return size;
#else
            return InputDeviceCommand.GenericFailure;
#endif
        }

        private unsafe long GetReportDescriptor(InputDeviceCommand* command)
        {
            if (command->payloadPtr == null || command->payloadSizeInBytes < 1)
                return InputDeviceCommand.GenericFailure;

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            var fd = open(m_Info.path, O_RDONLY);
            if (fd == null || fd.IsInvalid || !GetHidRawReportDescriptor(fd, out var buffer) ||
                command->payloadSizeInBytes < buffer.size)
                return InputDeviceCommand.GenericFailure;

            UnsafeUtility.MemCpy(command->payloadPtr, buffer.value, buffer.size);
            return buffer.size; // Expected return is the size of the descriptor
#else
            return InputDeviceCommand.GenericFailure;
#endif
        }

        private unsafe long GetParsedReportDescriptor(InputDeviceCommand* command)
        {
            if (command->payloadPtr == null || command->payloadSizeInBytes < 1)
                return InputDeviceCommand.GenericFailure;

            // Get string descriptor as a UTF-8 encoded buffer
            string descriptor = JsonUtility.ToJson(m_Descriptor);
            var buffer = Encoding.UTF8.GetBytes(descriptor);
            if (command->payloadSizeInBytes < buffer.Length)
                return InputDeviceCommand.GenericFailure;

            fixed (byte* ptr = buffer)
            {
                UnsafeUtility.MemCpy(command->payloadPtr, ptr, buffer.Length);
            }
            return buffer.Length; // Expected return is the size of the string buffer
        }

        public void Remove()
        {
            if (m_Device == null)
                return;

            var removeEvent = DeviceRemoveEvent.Create(m_Device.deviceId);
            InputSystem.QueueEvent(ref removeEvent);
            m_Device = null;
            Dispose();
        }

        // Provided for clarity
        public void RemoveImmediate() => Dispose();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Close hidapi handle
                m_Handle?.Close();
                m_Handle = null;

                // Forcibly remove device from input system if it's still present
                if (m_Device != null)
                {
                    InputSystem.RemoveDevice(m_Device);
                    m_Device = null;
                }
            }
        }

        public override string ToString()
        {
            return m_Device.ToString();
        }
    }
}
