using System;
using HIDrogen.Imports;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend
{
    using static HidApi;

    /// <summary>
    /// An hidapi device.
    /// </summary>
    internal class HidApiDevice : IDisposable
    {
        /// <summary>
        /// Information to be serialized to InputDeviceDescription.capabilities on device creation.
        /// </summary>
        private struct Capabilities
        {
            public int vendorId;
            public int productId;
            public int usagePage;
            public int usage;
        }

        private readonly hid_device_info m_Info;
        private hid_device m_Handle;
        private InputDevice m_Device;

        private readonly byte[] m_ReadBuffer = new byte[100]; // TODO: Remove default size once report length calculation is implemented

        public string path => m_Info.path;
        public InputDevice device => m_Device;

        private HidApiDevice(hid_device_info info, hid_device handle, InputDevice device)
        {
            m_Info = info;
            m_Handle = handle;
            m_Device = device;
            // m_ReadBuffer = new byte[descriptor.inputLength], // TODO: Use this when report length calculation is implemented
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

            // Create input device description and add it to the system
            // TODO: Get device descriptor
            var capabilities = new Capabilities()
            {
                vendorId = info.vendorId,
                productId = info.productId,
                usagePage = info.usagePage,
                usage = info.usage
            };
            var version = info.releaseVersion;
            var description = new InputDeviceDescription()
            {
                // TODO: Set to HID once descriptor retrieval has been implemented
                interfaceName = "LinuxHID",
                manufacturer = info.manufacturerName,
                product = info.productName,
                serial = info.serialNumber,
                version = $"{version.major}.{version.minor}",
                capabilities = JsonUtility.ToJson(capabilities)
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

            return new HidApiDevice(info, handle, device);
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