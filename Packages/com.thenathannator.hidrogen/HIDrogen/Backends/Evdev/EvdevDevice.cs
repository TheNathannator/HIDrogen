#if UNITY_STANDALONE_LINUX
using System;
using System.Threading;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    using static Evdev;

    internal class EvdevDevice : IDisposable
    {
        private const int kMaxReadErrors = 3;
        private const int kMaxReadRetries = 3;

        private EvdevBackend m_Backend;
        private InputDevice m_Device;
        private EvdevDescriptor m_Descriptor;
        private string m_Path;
        private fd m_Fd;

        private Thread m_ReadThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        private bool m_EventDropped;

        public string path => m_Path;
        public InputDevice device => m_Device;

        public EvdevDevice(
            EvdevBackend backend,
            InputDevice device,
            EvdevDescriptor descriptor,
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
            m_Path = null;
        }

        private unsafe void ReadThread()
        {
            const int eventBufferCount = 16;
            input_event* eventBuffer = stackalloc input_event[eventBufferCount];
            // Stack-allocated data is not initialized
            UnsafeUtility.MemClear(eventBuffer, eventBufferCount * sizeof(input_event));

            int errorCount = 0;
            while (!m_ThreadStop.WaitOne(1) && errorCount < kMaxReadErrors)
            {
                int eventsRead = 0;
                lock (m_Fd)
                {
                    // Wait for new events to come in
                    if (!IsReadAvailable(ref errorCount))
                    {
                        continue;
                    }

                    // Read new events
                    eventsRead = ReadEvents(eventBuffer, eventBufferCount, ref errorCount);
                    if (eventsRead < 0)
                    {
                        continue;
                    }
                }

                if (eventsRead > 0)
                {
                    ProcessEvents(eventBuffer, eventsRead);
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

        private unsafe int ReadEvents(input_event* eventBuffer, int eventBufferCount, ref int errorCount)
        {
            for (int retryCount = 0; retryCount < kMaxReadRetries; retryCount++)
            {
                int bytesRead = Fcntl.read(m_Fd, eventBuffer, eventBufferCount * sizeof(input_event));
                if (bytesRead >= 0)
                {
                    if (bytesRead % sizeof(input_event) != 0)
                    {
                        Logging.Warning($"Incomplete evdev event read!");
                    }

                    // Successful read
                    return bytesRead / sizeof(input_event);
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

            Logging.LastError($"Failed to read evdev events");
            errorCount++;
            return -1;
        }

        private unsafe void ProcessEvents(input_event* events, int eventCount)
        {
            for (int i = 0; i < eventCount; i++)
            {
                ref readonly var ev = ref events[i];

                // Evdev clients are required to ignore events when an overrun occurs
                if (m_EventDropped &&
                    (ev.type != input_event_type.EV_SYN ||
                    ev.code != (int)input_sync_code.SYN_REPORT))
                {
                    continue;
                }

                switch (ev.type)
                {
                    case input_event_type.EV_SYN:
                    {
                        var code = (input_sync_code)ev.code;
                        if (code == input_sync_code.SYN_DROPPED)
                        {
                            // Ignore events until next SYN_REPORT
                            m_EventDropped = true;
                        }
                        else if (code == input_sync_code.SYN_REPORT && m_EventDropped)
                        {
                            // Resync inputs
                            m_EventDropped = false;
                            SyncState();
                        }
                        break;
                    }
                    case input_event_type.EV_KEY:
                    {
                        SendKey((input_key_code)ev.code, ev.value);
                        break;
                    }
                    case input_event_type.EV_ABS:
                    {
                        SendAbs((input_abs_code)ev.code, ev.value);
                        break;
                    }
                    case input_event_type.EV_REL:
                    {
                        SendRel((input_rel_code)ev.code, ev.value);
                        break;
                    }
                }
            }
        }

        private void SendKey(input_key_code code, int value)
        {
            // m_Backend.QueueDeltaStateEvent(m_Device, EvdevBackend.InputFormat, ...);
        }

        private void SendAbs(input_abs_code code, int value)
        {
            // m_Backend.QueueDeltaStateEvent(m_Device, EvdevBackend.InputFormat, ...);
        }

        private void SendRel(input_rel_code code, int value)
        {
            // m_Backend.QueueDeltaStateEvent(m_Device, EvdevBackend.InputFormat, ...);
        }

        private unsafe void SyncState()
        {
            // TODO: create complete state event

            for (input_key_code key = 0; key < input_key_code.KEY_MAX; key++)
            {
                // ...
            }

            for (input_abs_code abs = 0; abs < input_abs_code.ABS_MAX; abs++)
            {
                // ...
            }

            for (input_rel_code rel = 0; rel < input_rel_code.REL_MAX; rel++)
            {
                // ...
            }

            // m_Backend.QueueStateEvent(m_Device, EvdevBackend.InputFormat, ...);
        }
    }
}
#endif
