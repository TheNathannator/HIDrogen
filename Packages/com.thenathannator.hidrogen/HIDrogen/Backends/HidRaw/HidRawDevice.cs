#if UNITY_STANDALONE_LINUX
using System;
using System.Text;
using System.Threading;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend
{
    using static HidRaw;

    internal class HidRawDevice : IDisposable
    {
        private HidRawBackend m_Backend;
        private InputDevice m_Device;
        private fd m_Fd;

        private Thread m_ReadThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        private HID.HIDDeviceDescriptor m_Descriptor;
        private int m_InputPrepend;

        public string devPath { get; set; }

        public HidRawDevice(
            HidRawBackend backend,
            InputDevice device,
            fd fd,
            HID.HIDDeviceDescriptor descriptor,
            int inputPrependCount
        )
        {
            m_Backend = backend;
            m_Device = device;
            m_Fd = fd;
            m_Descriptor = descriptor;
            m_InputPrepend = inputPrependCount;

            m_ReadThread = new Thread(ReadThread) { IsBackground = true };
            m_ReadThread.Start();
        }

        public void Dispose()
        {
            m_ThreadStop?.Set();

            if (m_ReadThread.IsAlive)
            {
                m_ReadThread?.Join();
                m_ReadThread = null;
            }

            m_ThreadStop?.Dispose();
            m_ThreadStop = null;

            m_Fd?.Dispose();
            m_Fd = null;

            m_Backend = null;
            m_Device = null;
        }

        private unsafe void ReadThread()
        {
            int readBufferSize = m_Descriptor.inputReportSize;
            byte* readBuffer = stackalloc byte[readBufferSize];
            // Stack-allocated data is not initialized
            UnsafeUtility.MemClear(readBuffer, readBufferSize);

            int readSize = readBufferSize - m_InputPrepend;
            int readOffset = m_InputPrepend;

            // Max allowed number of consecutive errors
            const int retryThreshold = 3;
            int errorCount = 0;

            while (!m_ThreadStop.WaitOne(1))
            {
                // Get current state
                int result = Fcntl.read(m_Fd, readBuffer + readOffset, readSize);
                if (result < 0)
                {
                    if (Posix.errno == Errno.ENOENT) // Device has been disconnected
                    {
                        break;
                    }

                    errorCount++;
                    if (errorCount >= retryThreshold)
                    {
                        Logging.LastError($"Failed to read hidraw report");
                        break;
                    }
#if HIDROGEN_VERBOSE_LOGGING
                    else
                    {
                        Logging.LastError($"hid_read error (attempt {errorCount})");
                    }
#endif
                    continue;
                }
                errorCount = 0;

                if (result > 0)
                {
                    m_Backend.QueueStateEvent(m_Device, HidRawBackend.InputFormat, readBuffer, readSize);
                }
            }

            m_Backend.QueueDeviceRemove(m_Device);
        }

        public unsafe long SendOutput(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
            {
                return InputDeviceCommand.GenericFailure;
            }

            int result = Fcntl.write(m_Fd, buffer, bufferSize);
            return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
        }

        public unsafe long GetFeature(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
            {
                return InputDeviceCommand.GenericFailure;
            }

            int result = Ioctl.ioctl(m_Fd, HIDIOCGFEATURE(bufferSize), buffer, bufferSize);
            return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
        }

        public unsafe long SetFeature(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
            {
                return InputDeviceCommand.GenericFailure;
            }

            int result = Ioctl.ioctl(m_Fd, HIDIOCSFEATURE(bufferSize), buffer, bufferSize);
            return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
        }

        // Not implemented; not necessary for layout building, and handling split top-level
        // collections properly would require manual report descriptor parsing

        // public long GetReportDescriptorSize()
        // {
        //     int size = 0;
        //     if (Ioctl.ioctl(m_Fd, HIDIOCGRDESCSIZE, ref size) < 0)
        //     {
        //         return InputDeviceCommand.GenericFailure;
        //     }

        //     // Expected return is the size of the descriptor
        //     return size;
        // }

        // public unsafe long GetReportDescriptor(void* buffer, int bufferSize)
        // {
        //     hidraw_report_descriptor descBuffer = default;
        //     if (Ioctl.ioctl(m_Fd, HIDIOCGRDESCSIZE, ref descBuffer.size) < 0 || bufferSize < descBuffer.size)
        //     {
        //         return InputDeviceCommand.GenericFailure;
        //     }

        //     // paranoia clamp
        //     descBuffer.size = Math.Min(descBuffer.size, HID_MAX_DESCRIPTOR_SIZE);

        //     if (Ioctl.ioctl(m_Fd, HIDIOCGRDESC, ref descBuffer) < 0)
        //     {
        //         return InputDeviceCommand.GenericFailure;
        //     }

        //     UnsafeUtility.MemCpy(buffer, descBuffer.value, descBuffer.size);

        //     // Expected return is the size of the descriptor
        //     return descBuffer.size;
        // }

        public unsafe long GetParsedReportDescriptor(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
            {
                return InputDeviceCommand.GenericFailure;
            }

            // Get string descriptor as a UTF-8 encoded buffer
            string descriptor = JsonUtility.ToJson(m_Descriptor);
            var bytes = Encoding.UTF8.GetBytes(descriptor);
            if (bufferSize < bytes.Length)
            {
                return InputDeviceCommand.GenericFailure;
            }

            fixed (byte* ptr = bytes)
            {
                UnsafeUtility.MemCpy(buffer, ptr, bytes.Length);
            }

            // Expected return is the size of the string buffer
            return bytes.Length;
        }
    }
}
#endif
