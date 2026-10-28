#if UNITY_STANDALONE_LINUX
using System;
using System.IO;
using System.Threading;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    internal sealed class EvdevDevice : IDisposable
    {
        private readonly EvdevBackend m_Backend;
        private readonly InputDevice m_Device;
        private readonly fd m_Handle;
        private readonly byte[] m_Axes;
        private readonly string m_Claim;
        private readonly Thread m_Thread;
        private int m_SyncRequested;
        private readonly EventWaitHandle m_Stop = new EventWaitHandle(false, EventResetMode.ManualReset);
        public string path { get; }

        internal EvdevDevice(EvdevBackend backend, InputDevice device, fd handle, string path, string claim, byte[] axes)
        {
            m_Backend = backend;
            m_Device = device;
            m_Handle = handle;
            this.path = path;
            m_Claim = claim;
            m_Axes = axes;
            m_Thread = new Thread(ReadThread) { IsBackground = true };
            m_Thread.Start();
        }

        public void Dispose()
        {
            m_Stop.Set();
            m_Thread.Join();
            m_Stop.Dispose();
            m_Handle.Dispose();
            LinuxInputOwnership.Release(m_Claim);
        }

        internal void RequestSync() => Interlocked.Exchange(ref m_SyncRequested, 1);

        private unsafe void Synchronize(ref EvdevState state)
        {
            var keys = new byte[96];
            if (Evdev.GetKeys(m_Handle, keys) < 0)
                throw new IOException($"Could not synchronize evdev keys for {path}: errno {(int)Posix.errno}");
            for (int code = 0; code < 768; code++)
                state.SetKey(code, Evdev.HasBit(keys, code));
            for (int code = 0; code < 64; code++)
            {
                if (!Evdev.HasBit(m_Axes, code))
                    continue;
                Evdev.AbsInfo info = default;
                if (Evdev.GetAbs(m_Handle, code, ref info) < 0)
                    throw new IOException($"Could not synchronize evdev axis {code} for {path}: errno {(int)Posix.errno}");
                fixed (int* axes = state.axes)
                    axes[code] = info.value;
            }
        }

        private unsafe void ReadThread()
        {
            try
            {
                EvdevState state = default;
                Synchronize(ref state);
                m_Backend.QueueStateEvent(m_Device, ref state);
                bool dropped = false;
                var events = stackalloc Evdev.InputEvent[32];
                while (!m_Stop.WaitOne(1))
                {
                    if (Interlocked.Exchange(ref m_SyncRequested, 0) != 0)
                    {
                        Synchronize(ref state);
                        m_Backend.QueueStateEvent(m_Device, ref state);
                    }
                    int count = Fcntl.read(m_Handle, events, sizeof(Evdev.InputEvent) * 32);
                    if (count < 0)
                    {
                        var error = Posix.errno;
                        if (error == Errno.EAGAIN || error == Errno.EINTR)
                            continue;
                        if (error == Errno.ENODEV || error == Errno.ENOENT)
                            break;
                        throw new IOException($"evdev read failed for {path}: errno {(int)error}");
                    }
                    if (count == 0)
                        break;
                    if (count % sizeof(Evdev.InputEvent) != 0)
                        throw new IOException($"Incomplete evdev event read for {path}.");
                    for (int i = 0; i < count / sizeof(Evdev.InputEvent); i++)
                    {
                        var ev = events[i];
                        if (ev.type == Evdev.EV_SYN && ev.code == Evdev.SYN_DROPPED)
                            dropped = true;
                        else if (ev.type == Evdev.EV_SYN && ev.code == Evdev.SYN_REPORT)
                        {
                            if (dropped)
                                Synchronize(ref state);
                            dropped = false;
                            m_Backend.QueueStateEvent(m_Device, ref state);
                        }
                        else if (!dropped && ev.type == Evdev.EV_KEY && ev.code < 768)
                            state.SetKey(ev.code, ev.value != 0);
                        else if (!dropped && ev.type == Evdev.EV_ABS && ev.code < 64)
                            state.axes[ev.code] = ev.value;
                    }
                }
            }
            catch (IOException ex)
            {
                Logging.Exception($"Evdev input stopped for {path}", ex);
            }
            finally
            {
                m_Backend.QueueDeviceRemove(m_Device);
            }
        }
    }
}
#endif
