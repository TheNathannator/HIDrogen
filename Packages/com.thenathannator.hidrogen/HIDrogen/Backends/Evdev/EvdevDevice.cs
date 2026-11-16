#if UNITY_STANDALONE_LINUX
using System;
using System.Threading;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using HIDrogen.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.InputSystem;

namespace HIDrogen.Backend.Linux
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

        // Offset notes:
        // keys: sizeof(byte) / 8, KEY_CNT == 0x300, max state size == 0x60
        // abs: sizeof(int), ABS_CNT == 0x40, max state size == 0x100
        // rel: sizeof(int), REL_CNT == 0x10, max state size == 0x40
        // Total maximum offset: 0x1A0
        readonly ushort[] m_KeyBitOffsets;
        readonly ushort[] m_AbsOffsets;
        readonly ushort[] m_RelOffsets;

        private NativeArray<byte> m_State;

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

            // Determine key bit offsets
            ushort keyEndByte = 0;
            var supportedKeys = descriptor.supportedKeys;
            if (supportedKeys.Length > 0)
            {
                m_KeyBitOffsets = new ushort[(int)input_key_code.KEY_CNT];
                for (int i = 0; i < m_KeyBitOffsets.Length; i++)
                {
                    m_KeyBitOffsets[i] = ushort.MaxValue;
                }

                for (int i = 0; i < supportedKeys.Length; i++)
                {
                    var key = supportedKeys[i];
                    m_KeyBitOffsets[(int)key] = (ushort)i;
                }

                int keyBits = NumberUtils.AlignToMultiple(supportedKeys.Length, 8);
                // Align for axis values
                int keyBytes = NumberUtils.AlignToMultiple(keyBits / 8, sizeof(int));
                keyEndByte = (ushort)keyBytes;
            }

            // Determine abs byte offsets
            int absEndByte = keyEndByte;
            var supportedAbs = descriptor.supportedAbs;
            if (supportedAbs.Length > 0)
            {
                m_AbsOffsets = new ushort[(int)input_abs_code.ABS_CNT];
                for (int i = 0; i < m_AbsOffsets.Length; i++)
                {
                    m_AbsOffsets[i] = ushort.MaxValue;
                }

                for (int i = 0; i < supportedAbs.Length; i++)
                {
                    int abs = (int)supportedAbs[i];
                    int byteOffset = i * sizeof(int);
                    m_AbsOffsets[abs] = (ushort)(keyEndByte + byteOffset);
                }

                int absBytes = supportedAbs.Length * sizeof(int);
                absEndByte = keyEndByte + absBytes;
            }

            // Determine rel byte offsets
            // int relEndByte = absEndByte; // not necessary
            var supportedRel = descriptor.supportedRel;
            if (supportedRel.Length > 0)
            {
                m_RelOffsets = new ushort[(int)input_rel_code.REL_CNT];
                for (int i = 0; i < m_RelOffsets.Length; i++)
                {
                    m_RelOffsets[i] = ushort.MaxValue;
                }

                for (int i = 0; i < supportedRel.Length; i++)
                {
                    int rel = (int)supportedRel[i];
                    int byteOffset = i * sizeof(int);
                    m_RelOffsets[rel] = (ushort)(absEndByte + byteOffset);
                }

                // Not necessary, rel axes don't have meaningful state
                // int relBytes = supportedAbs.Length * sizeof(int);
                // relEndByte = absEndByte + relBytes;
            }

            m_State = new NativeArray<byte>(absEndByte, Allocator.Persistent);

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

            if (m_State.IsCreated)
            {
                m_State.Dispose();
                m_State = default;
            }

            m_Backend = null;
            m_Device = null;
            m_Path = null;
        }

        private unsafe void ReadThread()
        {
            SyncState();

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

                    // Keep lock on fd while processing events, to prevent racing with a concurrent call to SyncState
                    ProcessEvents(eventBuffer, eventsRead);
                    errorCount = 0;
                }
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

        private uint ApplyKey(input_key_code code, int value)
        {
            // value == 2 is used to indicate key repeasts
            if (value != 0 && value != 1)
            {
                return uint.MaxValue;
            }

            ushort bitOffset = m_KeyBitOffsets[(int)code];
            uint byteOffset = bitOffset / 8u;
            if (byteOffset < m_State.Length)
            {
                byte state = m_State[(int)byteOffset];

                int bit = bitOffset % 8;
                if (value == 1)
                {
                    state |= (byte)(1 << bit);
                }
                else
                {
                    state &= (byte)~(1 << bit);
                }

                m_State[(int)byteOffset] = state;
            }

            return byteOffset;
        }

        private uint ApplyAbs(input_abs_code code, int value)
        {
            uint byteOffset = m_AbsOffsets[(int)code];
            if (byteOffset < m_State.Length)
            {
                m_State.ReinterpretStore((int)byteOffset, value);
            }

            return byteOffset;
        }

        private unsafe void SendKey(input_key_code code, int value)
        {
            uint byteOffset = ApplyKey(code, value);
            if (byteOffset < m_State.Length)
            {
                // Can't delta-state just a single bit, need to do the whole field
                byte state = m_State[(int)byteOffset];
                m_Backend.QueueDeltaStateEvent(m_Device, byteOffset, &state, sizeof(byte));
            }
        }

        private unsafe void SendAbs(input_abs_code code, int value)
        {
            uint byteOffset = ApplyAbs(code, value);
            if (byteOffset < m_State.Length)
            {
                m_Backend.QueueDeltaStateEvent(m_Device, byteOffset, &value, sizeof(int));
            }
        }

        private unsafe void SendRel(input_rel_code code, int value)
        {
            uint byteOffset = m_RelOffsets[(int)code];
            if (byteOffset < m_State.Length)
            {
                m_Backend.QueueDeltaStateEvent(m_Device, byteOffset, &value, sizeof(int));
            }
        }

        private unsafe void SyncState()
        {
            lock (m_Fd)
            {
                UnsafeUtility.MemClear(m_State.GetUnsafePtr(), m_State.Length);

                // The entire keymask must be grabbed all at once
                var keyMaskPtr = stackalloc UIntPtr[KEY_MAX_LONGS];
                var keyMask = new EvdevBits<input_key_code>(keyMaskPtr, KEY_MAX_LONGS);
                if (keyMask.Populate(m_Fd, EVIOCGKEY(keyMask.count)) < 0)
                {
                    Logging.LastError("Failed to sync evdev key mask");
                }
                else for (int i = 0; i < m_Descriptor.supportedKeys.Length; i++)
                {
                    var keyCode = m_Descriptor.supportedKeys[i];
                    ApplyKey(keyCode, keyMask.TestBit(keyCode) ? 1 : 0);
                }

                for (int i = 0; i < m_Descriptor.supportedAbs.Length; i++)
                {
                    var absCode = m_Descriptor.supportedAbs[i];

                    var abs = new input_absinfo();
                    if (Ioctl.ioctl(m_Fd, EVIOCGABS(absCode), ref abs) < 0)
                    {
                        Logging.LastError("Failed to sync evdev abs value");
                        abs.value = 0;
                    }

                    ApplyAbs(absCode, abs.value);
                }

                // Relative axes should not be synced; attempting to do so will just make them jitter briefly
                // for (int i = 0; i < m_Descriptor.supportedRel.Length; i++)
                // {
                // }
            }

            m_Backend.QueueStateEvent(m_Device, EvdevBackend.InputFormat, m_State.GetUnsafeReadOnlyPtr(), m_State.Length);
        }
    }
}
#endif
