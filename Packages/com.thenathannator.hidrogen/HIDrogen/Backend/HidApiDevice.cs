using System;
using System.Reflection;
using HIDrogen.Imports;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend
{
    using static HidApi;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    using static Libc;
    using static HidRaw;
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

        private readonly hid_device_info m_Info;
        private hid_device m_Handle;
        private InputDevice m_Device;
        private HID.HIDDeviceDescriptor m_Descriptor;

        private readonly byte[] m_ReadBuffer;

        public string path => m_Info.path;
        public InputDevice device => m_Device;

        private HidApiDevice(hid_device_info info, hid_device handle, InputDevice device, HID.HIDDeviceDescriptor descriptor)
        {
            m_Info = info;
            m_Handle = handle;
            m_Device = device;
            m_Descriptor = descriptor;

            m_ReadBuffer = new byte[descriptor.inputReportSize];
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
                Debug.LogError($"Error when opening HID device path: {hid_error()}");
                return null;
            }

            // Get descriptor
            if (!GetReportDescriptor(info, out var descriptor))
                return null;

            // Create input device description and add it to the system
            var version = info.releaseVersion;
            var description = new InputDeviceDescription()
            {
                interfaceName = "HID",
                manufacturer = info.manufacturerName,
                product = info.productName,
                serial = info.serialNumber,
                version = $"{version.major}.{version.minor}",
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
                Debug.LogError($"Failed to add device to the system!\n{ex}");
                return null;
            }

            return new HidApiDevice(info, handle, device, descriptor);
        }

        private static unsafe bool GetReportDescriptor(hid_device_info info, out HID.HIDDeviceDescriptor descriptor)
        {
            byte* data = null;
            int dataLength = 0;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            var fd = open(info.path, O_RDONLY);
            if (fd == null || fd.IsInvalid)
            {
                descriptor = default;
                return false;
            }

            var buffer = new hidraw_report_descriptor();
            using (fd)
            {
                if ((ioctl(fd, HIDIOCGRDESCSIZE, &buffer.size) < 0) ||
                    (ioctl(fd, HIDIOCGRDESC, &buffer) < 0))
                {
                    Debug.Log($"Error getting descriptor: {errno}");
                    descriptor = default;
                    return false;
                }
            }

            data = buffer.value;
            dataLength = buffer.size;
#endif

            return ParseReportDescriptor(info, data, dataLength, out descriptor);
        }

        internal static unsafe bool ParseReportDescriptor(hid_device_info info, byte* data, int length,
            out HID.HIDDeviceDescriptor descriptor)
        {
            descriptor = new HID.HIDDeviceDescriptor()
            {
                vendorId = info.vendorId,
                productId = info.productId,
                usagePage = (HID.UsagePage)info.usagePage,
                usage = info.usage
            };

            if (data == null || length < 1 || !s_ParseReportDescriptor(data, length, ref descriptor))
                return false;

            // The parser doesn't actually set the report lengths unfortunately, we need to fix them up ourselves
            int inputSizeBits = 0;
            int outputSizeBits = 0;
            int featureSizeBits = 0;
            foreach (var element in descriptor.elements)
            {
                int sizeBits = element.reportOffsetInBits + element.reportSizeInBits;
                switch (element.reportType)
                {
                    case HID.HIDReportType.Input:
                        if (inputSizeBits < sizeBits)
                            inputSizeBits = sizeBits;
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

            // Ensure sizes are normalized to byte boundaries
            inputSizeBits += -(inputSizeBits % 8) + 8;
            outputSizeBits += -(outputSizeBits % 8) + 8;
            featureSizeBits += -(featureSizeBits % 8) + 8;

            // Turn bit size into byte size
            descriptor.inputReportSize = inputSizeBits / 8;
            descriptor.outputReportSize = outputSizeBits / 8;
            descriptor.featureReportSize = featureSizeBits / 8;

            return descriptor.inputReportSize > 0; // Output and feature reports aren't required for normal operation
        }

        // Returns true on success, false if the device should be removed.
        public bool UpdateState()
        {
            // Ensure read buffer is valid
            if (m_ReadBuffer == null || m_ReadBuffer.Length < 1)
            {
                Debug.Assert(false, "without a read buffer!");
                return false;
            }

            // Get current state
            int result = hid_read_timeout(m_Handle, m_ReadBuffer, 0);
            if (result <= 0) // Error or no reports available
            {
                if (result < 0) // Error
                {
                    Debug.LogError($"hid_read: {hid_error(m_Handle)}");
                    return false;
                }
                return true;
            }

            // Queue state
            QueueState();
            return true;
        }

        // Based on InputSystem.QueueStateEvent<T>
        private unsafe void QueueState()
        {
            // Get event size
            const int kMaxStateSize = 512; // TODO: Is this actually necessary? (InputSystem.StateEventBuffer.kMaxSize)
            var stateSize = m_ReadBuffer.Length;
            if (stateSize > kMaxStateSize)
            {
                Debug.LogError($"Size of the buffer ({stateSize}) exceeds maximum supported state size ({kMaxStateSize})");
                return;
            }
            int eventSize = UnsafeUtility.SizeOf<StateEvent>() - 1 + stateSize; // StateEvent already includes 1 byte at the end

            // Create state buffer
            byte* buffer = stackalloc byte[eventSize];
            StateEvent* stateBuffer = (StateEvent*)buffer;
            *stateBuffer = new StateEvent
            {
                baseEvent = new InputEvent(StateEvent.Type, eventSize, m_Device.deviceId),
                stateFormat = HidApiBackend.InputFormat
            };

            // Copy state into buffer
            fixed (byte* statePtr = m_ReadBuffer)
            {
                byte* bufferPtr = (byte*)stateBuffer->state;
                UnsafeUtility.MemCpy(bufferPtr, statePtr, stateSize);
            }

            // Queue state event
            var eventPtr = new InputEventPtr((InputEvent*)buffer);
            InputSystem.QueueEvent(eventPtr);
        }

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

                // Remove device from input system
                if (m_Device != null)
                {
                    InputSystem.RemoveDevice(m_Device);
                    m_Device = null;
                }
            }
        }
    }
}