using System;
using System.Text;
using System.Threading;
using HIDrogen.Imports;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend
{
    using static HidApi;

    /// <summary>
    /// An hidapi device.
    /// </summary>
    internal class HidApiDevice : IDisposable
    {
        private readonly HidApiBackend m_Backend;

        private hid_device m_Handle;
        private readonly InputDevice m_Device;
        private readonly HID.HIDDeviceDescriptor m_Descriptor;

        private readonly int m_PrependCount; // Number of bytes to prepend to the queued input buffer

        private Thread m_ReadThread;
        private readonly EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        public string path { get; }

        public bool isAlive => m_ReadThread.IsAlive;

        public HidApiDevice(HidApiBackend backend, string path, InputDevice device, HID.HIDDeviceDescriptor descriptor,
            int inputPrependCount)
        {
            m_Handle = hid_open_path(path);
            if (m_Handle == null || m_Handle.IsInvalid)
                throw new Exception(Logging.MakeInteropErrorMessage($"Error when opening HID device '{path}': {hid_error()}"));

            m_Backend = backend;
            this.path = path;
            m_Device = device;
            m_Descriptor = descriptor;
            m_PrependCount = inputPrependCount;

            m_ReadThread = new Thread(ReadThread) { IsBackground = true };
            m_ReadThread.Start();
        }

        ~HidApiDevice()
        {
            Dispose(false);
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
                m_ThreadStop.Set();
                m_ReadThread?.Join();
                m_ReadThread = null;

                m_Handle?.Dispose();
                m_Handle = null;
            }
        }

        private unsafe void ReadThread()
        {
            int readSize = m_Descriptor.inputReportSize - m_PrependCount;
            int readOffset = m_PrependCount;
            byte* readBuffer = stackalloc byte[m_Descriptor.inputReportSize];

            // Max allowed number of consecutive errors
            const int retryThreshold = 3;
            int errorCount = 0;

            while (!m_ThreadStop.WaitOne(1))
            {
                // Get current state
                int result = hid_read_timeout(m_Handle, readBuffer + readOffset, readSize, 0);
                if (result < 0)
                {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                    if (Libc.errno == Errno.ENOENT) // Device has been disconnected
                        break;
#endif

                    errorCount++;
                    if (errorCount >= retryThreshold)
                    {
                        Logging.InteropError($"hid_read error: {hid_error(m_Handle)}");
                        break;
                    }
#if HIDROGEN_VERBOSE_LOGGING
                    else
                    {
                        Logging.InteropError($"hid_read error (attempt {errorCount}): {hid_error(m_Handle)}");
                    }
#endif
                    continue;
                }
                errorCount = 0;

                if (result > 0)
                    m_Backend.QueueStateEvent(m_Device, HidApiBackend.InputFormat, readBuffer, readSize);
            }

            m_Backend.QueueDeviceRemove(m_Device);
        }

        public unsafe long SendOutput(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
                return InputDeviceCommand.GenericFailure;

            int result = hid_write(m_Handle, buffer, bufferSize);
            return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
        }

        public unsafe long GetFeature(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
                return InputDeviceCommand.GenericFailure;

            int result = hid_get_feature_report(m_Handle, buffer, bufferSize);
            return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
        }

        public unsafe long SetFeature(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
                return InputDeviceCommand.GenericFailure;

            int result = hid_send_feature_report(m_Handle, buffer, bufferSize);
            return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
        }

        public unsafe long GetReportDescriptorSize()
        {
            if (!m_Backend.PlatformGetDescriptorSize(path, out int size))
                return InputDeviceCommand.GenericFailure;

            // Expected return is the size of the descriptor
            return size;
        }

        public unsafe long GetReportDescriptor(void* buffer, int bufferSize)
        {
            if (!m_Backend.PlatformGetDescriptor(path, buffer, bufferSize, out int bytesWritten))
                return InputDeviceCommand.GenericFailure;

            // Expected return is the size of the descriptor
            return bytesWritten;
        }

        public unsafe long GetParsedReportDescriptor(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
                return InputDeviceCommand.GenericFailure;

            // Get string descriptor as a UTF-8 encoded buffer
            string descriptor = JsonUtility.ToJson(m_Descriptor);
            var bytes = Encoding.UTF8.GetBytes(descriptor);
            if (bufferSize < bytes.Length)
                return InputDeviceCommand.GenericFailure;

            fixed (byte* ptr = bytes)
            {
                UnsafeUtility.MemCpy(buffer, ptr, bytes.Length);
            }

            // Expected return is the size of the string buffer
            return bytes.Length; 
        }

        public override string ToString()
        {
            return m_Device.ToString();
        }
    }
}
