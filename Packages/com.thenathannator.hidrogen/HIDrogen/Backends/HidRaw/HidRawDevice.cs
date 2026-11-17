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

namespace HIDrogen.Backend.Linux
{
    using static HidRaw;

    internal class HidRawDevice : IDisposable
    {
        private const int kMaxReadErrors = 3;
        private const int kMaxReadRetries = 3;

        private HidRawBackend m_Backend;
        private InputDevice m_Device;
        private HID.HIDDeviceDescriptor m_Descriptor;
        private string m_Path;
        private fd m_Fd;

        private Thread m_ReadThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        public string path => m_Path;
        public InputDevice device => m_Device;

        public HidRawDevice(
            HidRawBackend backend,
            InputDevice device,
            HID.HIDDeviceDescriptor descriptor,
            string path,
            fd fd
        )
        {
            m_Backend = backend;
            m_Device = device;
            m_Descriptor = descriptor;
            m_Path = path;
            m_Fd = fd;

            m_ReadThread = new Thread(ReadThread) { IsBackground = true };
            m_ReadThread.Start();
        }

        public void Dispose()
        {
            m_ThreadStop?.Set();

            if (m_ReadThread != null && m_ReadThread.IsAlive)
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
            m_Path = null;
        }

        private unsafe void ReadThread()
        {
            int readBufferSize = m_Descriptor.inputReportSize;
            byte* readBuffer = stackalloc byte[readBufferSize];
            // Stack-allocated data is not initialized
            UnsafeUtility.MemClear(readBuffer, readBufferSize);

            int errorCount = 0;
            while (!m_ThreadStop.WaitOne(1) && errorCount < kMaxReadErrors)
            {
                int bytesRead = 0;
                lock (m_Fd)
                {
                    // Wait for new reports to come in
                    if (!IsReadAvailable(ref errorCount))
                    {
                        continue;
                    }

                    // Read new report
                    bytesRead = ReadReport(readBuffer, readBufferSize, ref errorCount);
                    if (bytesRead < 0)
                    {
                        continue;
                    }
                }

                if (bytesRead > 0)
                {
                    m_Backend.QueueStateEvent(m_Device, HidRawBackend.InputFormat, readBuffer, bytesRead);
                }

                errorCount = 0;
            }

            m_Backend.QueueDeviceRemove(m_Device);
        }

        private bool IsReadAvailable(ref int errorCount)
        {
            var poll = new pollfd(m_Fd, Fcntl.POLLIN);
            int result = Fcntl.poll(ref poll, 0);
            if (result < 0)
            {
                Logging.LastError($"Error while polling for device monitor events");
                errorCount++;
            }

            return result > 0;
        }

        private unsafe int ReadReport(byte* readBuffer, int readBufferSize, ref int errorCount)
        {
            for (int retryCount = 0; retryCount < kMaxReadRetries; retryCount++)
            {
                int bytesRead = Fcntl.read(m_Fd, readBuffer, readBufferSize);
                if (bytesRead >= 0)
                {
                    // Successful read
                    return bytesRead;
                }

                var errno = Posix.errno;
                if (errno == Errno.ENOENT)
                {
                    // Device has been disconnected
                    errorCount = kMaxReadErrors;
                    return -1;
                }

                if (errno == Errno.EINTR ||
                    errno == Errno.EAGAIN ||
                    errno == Errno.EINPROGRESS)
                {
                    // Read was interrupted, try again
                    continue;
                }

                break;
            }

            Logging.LastError($"Failed to read hidraw report");
            errorCount++;
            return -1;
        }

        public unsafe long SendOutput(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
            {
                return InputDeviceCommand.GenericFailure;
            }

            lock (m_Fd)
            {
                int result = Fcntl.write(m_Fd, buffer, bufferSize);
                return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
            }
        }

        public unsafe long GetFeature(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
            {
                return InputDeviceCommand.GenericFailure;
            }

            lock (m_Fd)
            {
                int result = Ioctl.ioctl(m_Fd, HIDIOCGFEATURE(bufferSize), (byte*)buffer, bufferSize);
                return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
            }
        }

        public unsafe long SetFeature(void* buffer, int bufferSize)
        {
            if (buffer == null || bufferSize < 1)
            {
                return InputDeviceCommand.GenericFailure;
            }

            lock (m_Fd)
            {
                int result = Ioctl.ioctl(m_Fd, HIDIOCSFEATURE(bufferSize), (byte*)buffer, bufferSize);
                return result >= 0 ? InputDeviceCommand.GenericSuccess : InputDeviceCommand.GenericFailure;
            }
        }

        // Not implemented; not necessary for layout building, and handling split top-level
        // collections properly would require manual report descriptor parsing

        // public long GetReportDescriptorSize()
        // {
        //     int size = 0;
        //     lock (m_Fd)
        //     {
        //         if (Ioctl.ioctl(m_Fd, HIDIOCGRDESCSIZE, ref size) < 0)
        //         {
        //             Logging.LastError("Could not get hidraw descriptor size");
        //             return InputDeviceCommand.GenericFailure;
        //         }
        //     }

        //     // Expected return is the size of the descriptor
        //     return size;
        // }

        // public unsafe long GetReportDescriptor(void* buffer, int bufferSize)
        // {
        //     hidraw_report_descriptor descBuffer = default;
        //     lock (m_Fd)
        //     {
        //         if (Ioctl.ioctl(m_Fd, HIDIOCGRDESCSIZE, ref descBuffer.size) < 0 || bufferSize < descBuffer.size)
        //         {
        //             Logging.LastError("Could not get hidraw descriptor size");
        //             return InputDeviceCommand.GenericFailure;
        //         }

        //         // paranoia clamp
        //         descBuffer.size = Math.Min(descBuffer.size, hidraw_report_descriptor.MAX_DESCRIPTOR_SIZE);

        //         if (Ioctl.ioctl(m_Fd, HIDIOCGRDESC, ref descBuffer) < 0)
        //         {
        //             Logging.LastError("Could not get hidraw descriptor");
        //             return InputDeviceCommand.GenericFailure;
        //         }
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
