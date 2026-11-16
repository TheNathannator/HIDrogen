#if UNITY_STANDALONE_LINUX
using System;
using System.Threading;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    using static Evdev;

    internal class EvdevDevice : IDisposable
    {
        private EvdevBackend m_Backend;
        private InputDevice m_Device;
        private EvdevDescriptor m_Descriptor;
        private string m_Path;
        private fd m_Fd;

        private Thread m_ReadThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

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
        }
    }
}
#endif
