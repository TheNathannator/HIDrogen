#if UNITY_STANDALONE_LINUX
using System;
using System.Threading;
using HIDrogen.Devices.LowLevel;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend.Linux
{
    using static Evdev;

    internal class EvdevBackendDevice : IDisposable
    {
        private const int kMaxReadErrors = 3;
        private const int kMaxReadRetries = 3;

        private EvdevBackend m_Backend;
        private InputDevice m_Device;
        private EvdevCapabilities m_Capabilities;

        private readonly string m_Syspath;
        private readonly string m_EvdevPath;
        private fd m_Fd;

        private EvdevStateHandler m_StateHandlerLocked;
        private NativeArray<byte> m_StateLocked;

        private Thread m_ReadThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        private bool m_EventDropped;

        public string syspath => m_Syspath;
        public string evdevPath => m_EvdevPath;

        public InputDevice device => m_Device;

        public EvdevBackendDevice(
            EvdevBackend backend,
            EvdevBackend.AddContext addContext,
            InputDevice device
        )
        {
            m_Backend = backend;
            m_Device = device;

            m_Capabilities = addContext.capabilities;
            m_Syspath = addContext.syspath;
            m_EvdevPath = addContext.evdevPath;

            m_Fd = Fcntl.open(addContext.evdevPath, Fcntl.O_RDONLY | Fcntl.O_CLOEXEC);
            if (m_Fd == null || m_Fd.IsInvalid)
            {
                throw new Exception(Logging.MakeLastErrorMessage("Failed to open evdev device"));
            }

            m_StateHandlerLocked = new EvdevStateHandler(addContext.capabilities);

            // State events will be accumulated and sent on SYN_REPORT
            // Using delta-state events is incompatible with PlasticBand-Unity's state translation
            m_StateLocked = new NativeArray<byte>(m_StateHandlerLocked.byteLength, Allocator.Persistent);

            // Clear deadzones
            // No need to lock yet since the read thread hasn't been started yet
            RemoveDeadzonesLocked();

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

            if (m_Fd != null)
            {
                // why is this global data, exactly...?
                RestoreDeadzonesLocked();

                m_Fd?.Dispose();
                m_Fd = null;
            }

            m_StateHandlerLocked = null;

            if (m_StateLocked.IsCreated)
            {
                m_StateLocked.Dispose();
                m_StateLocked = default;
            }

            m_Backend = null;
            m_Device = null;
        }

        private unsafe void ReadThread()
        {
            lock (m_Fd)
            {
                SyncStateLocked();
            }

            const int eventBufferCount = 16;
            input_event* eventBuffer = stackalloc input_event[eventBufferCount];
            // Stack-allocated data is not initialized
            UnsafeUtility.MemClear(eventBuffer, eventBufferCount * sizeof(input_event));

            int errorCount = 0;
            while (!m_ThreadStop.WaitOne(1) && errorCount < kMaxReadErrors)
            {
                lock (m_Fd)
                {
                    // Wait for new events to come in
                    if (!IsReadAvailable(ref errorCount))
                    {
                        continue;
                    }

                    // Read new events
                    int eventsRead = ReadEvents(eventBuffer, eventBufferCount, ref errorCount);
                    if (eventsRead < 0)
                    {
                        continue;
                    }

                    // Keep lock on fd while processing events, to prevent racing with a concurrent SyncState command
                    for (int i = 0; i < eventsRead; i++)
                    {
                        ref readonly var ev = ref eventBuffer[i];
                        ProcessEventLocked(ev);
                    }

                    errorCount = 0;
                }
            }

            m_Backend.QueueDeviceRemove(m_Device);
        }

        private bool IsReadAvailable(ref int errorCount)
        {
            var poll = new pollfd(m_Fd, Fcntl.POLLIN);
            int result = Fcntl.poll(ref poll, 0);
            if (result < 0 && Posix.errno != Errno.EINTR)
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
                if (errno.IsDeviceDisconnect())
                {
                    // Device has been disconnected
                    errorCount = kMaxReadErrors;
                    return -1;
                }

                if (errno.IsReadInterrupt())
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

        private void ProcessEventLocked(in input_event ev)
        {
            // Evdev clients are required to ignore events until the next SYN_REPORT when an overrun occurs
            if (m_EventDropped)
            {
                if (ev.type == input_event_type.EV_SYN && ev.code == (int)input_sync_code.SYN_REPORT)
                {
                    m_EventDropped = false;
                    SyncStateLocked();
                }
                return;
            }

            switch (ev.type)
            {
                case input_event_type.EV_SYN:
                {
                    HandleSynLocked((input_sync_code)ev.code);
                    break;
                }
                case input_event_type.EV_KEY:
                {
                    m_StateHandlerLocked.WriteKey(m_StateLocked, (input_key_code)ev.code, ev.value);
                    break;
                }
                case input_event_type.EV_ABS:
                {
                    m_StateHandlerLocked.WriteAbs(m_StateLocked, (input_abs_code)ev.code, ev.value);
                    break;
                }
                case input_event_type.EV_REL:
                {
                    m_StateHandlerLocked.WriteRel(m_StateLocked, (input_rel_code)ev.code, ev.value);
                    break;
                }
            }
        }

        private void HandleSynLocked(input_sync_code code)
        {
            switch (code)
            {
                case input_sync_code.SYN_DROPPED:
                {
                    // Ignore events until next SYN_REPORT
                    m_EventDropped = true;
                    break;
                }
                case input_sync_code.SYN_REPORT:
                {
                    SendStateLocked();
                    break;
                }
            }
        }

        private void SendStateLocked()
        {
            m_Backend.QueueStateEvent(m_Device, EvdevDefinitions.InputFormat, m_StateLocked);

            // Clear rels, they don't have persistent state
            m_StateHandlerLocked.ClearRel(m_StateLocked);
        }

        private unsafe void SyncStateLocked()
        {
            UnsafeUtility.MemClear(m_StateLocked.GetUnsafePtr(), m_StateLocked.Length);

            // The entire keymask must be grabbed all at once
            var keyMaskPtr = stackalloc UIntPtr[KEY_MAX_LONGS];
            var keyMask = new EvdevBits<input_key_code>(keyMaskPtr, KEY_MAX_LONGS);
            if (keyMask.Populate(m_Fd, EVIOCGKEY(keyMask.count)) < 0)
            {
                Logging.LastError("Failed to sync evdev key mask");
            }
            else for (int i = 0; i < m_Capabilities.supportedKeys.Length; i++)
            {
                var keyCode = m_Capabilities.supportedKeys[i];
                m_StateHandlerLocked.WriteKey(m_StateLocked, keyCode, keyMask.TestBit(keyCode) ? 1 : 0);
            }

            for (int i = 0; i < m_Capabilities.supportedAbs.Length; i++)
            {
                var absCode = m_Capabilities.supportedAbs[i];

                var absInfo = new input_absinfo();
                if (Ioctl.ioctl(m_Fd, EVIOCGABS(absCode), ref absInfo) < 0)
                {
                    if (Posix.errno.IsDeviceDisconnect())
                    {
                        return;
                    }

                    Logging.LastError("Failed to sync evdev abs value");
                    absInfo.value = 0;
                }

                m_StateHandlerLocked.WriteAbs(m_StateLocked, absCode, absInfo.value);
            }

            // Relative axes should not be synced; attempting to do so will just make them jitter briefly
            // for (int i = 0; i < m_Capabilities.supportedRel.Length; i++)
            // {
            // }

            SendStateLocked();
        }

        private void RemoveDeadzonesLocked()
        {
            for (int i = 0; i < m_Capabilities.supportedAbs.Length; i++)
            {
                var absCode = m_Capabilities.supportedAbs[i];

                // Retrieve current state to avoid overwriting value/min/max
                var absInfo = new input_absinfo();
                if (Ioctl.ioctl(m_Fd, EVIOCGABS(absCode), ref absInfo) < 0)
                {
                    if (Posix.errno.IsDeviceDisconnect())
                    {
                        return;
                    }

                    Logging.LastError("Failed to get evdev absinfo");
                    continue;
                }

                absInfo.fuzz = 0;
                absInfo.flat = 0;

                if (Ioctl.ioctl(m_Fd, EVIOCSABS(absCode), ref absInfo) < 0)
                {
                    if (Posix.errno.IsDeviceDisconnect())
                    {
                        return;
                    }

                    Logging.LastError("Failed to set evdev absinfo");
                    continue;
                }
            }
        }

        private void RestoreDeadzonesLocked()
        {
            for (int i = 0; i < m_Capabilities.supportedAbs.Length; i++)
            {
                var absCode = m_Capabilities.supportedAbs[i];
                var originalAbsInfo = m_Capabilities.absInfo[i];

                // Retrieve current state to avoid overwriting value/min/max
                var newAbsInfo = new input_absinfo();
                if (Ioctl.ioctl(m_Fd, EVIOCGABS(absCode), ref newAbsInfo) < 0)
                {
                    if (Posix.errno.IsDeviceDisconnect())
                    {
                        return;
                    }

                    Logging.LastError("Failed to get evdev absinfo");
                    continue;
                }

                newAbsInfo.fuzz = originalAbsInfo.fuzz;
                newAbsInfo.flat = originalAbsInfo.flat;

                if (Ioctl.ioctl(m_Fd, EVIOCSABS(absCode), ref newAbsInfo) < 0)
                {
                    if (Posix.errno.IsDeviceDisconnect())
                    {
                        return;
                    }

                    Logging.LastError("Failed to set evdev absinfo");
                    continue;
                }
            }
        }

        public unsafe long SyncState(InputDeviceCommand* _command)
        {
            lock (m_Fd)
            {
                SyncStateLocked();
            }

            return InputDeviceCommand.GenericSuccess;
        }
    }
}
#endif
