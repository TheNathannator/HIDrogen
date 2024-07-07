#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && UNITY_2022_2_OR_NEWER
using System;
using System.Threading;
using HIDrogen.Imports;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend
{
    using static Win32Error;

    internal class XInputBackendDevice : IDisposable
    {
        private readonly XInputBackend m_Backend;

        public uint userIndex { get; }
        public InputDevice device { get; }

        private Thread m_ReadThread;
        private EventWaitHandle m_ThreadStop = new(false, EventResetMode.ManualReset);

        public XInputBackendDevice(XInputBackend backend, uint userIndex, InputDevice device)
        {
            m_Backend = backend;
            this.userIndex = userIndex;
            this.device = device;

            m_ReadThread = new Thread(ReadThread) { IsBackground = true };
            m_ReadThread.Start();
        }

        public void Dispose()
        {
            m_ThreadStop?.Set();
            m_ReadThread?.Join();
            m_ReadThread = null;

            m_ThreadStop?.Dispose();
            m_ThreadStop = null;
        }

        private unsafe void ReadThread()
        {
            uint lastPacketNumber = 0;
            while (!m_ThreadStop.WaitOne(1))
            {
                var result = m_Backend.xinput.GetState(userIndex, out var state);
                if (result != ERROR_SUCCESS)
                {
                    if (result != ERROR_DEVICE_NOT_CONNECTED)
                        Logging.Error($"Failed to get state for XInput user index {userIndex}: 0x{(int)result:X8}");
                    break;
                }

                if (state.packetNumber == lastPacketNumber)
                    continue;

                lastPacketNumber = state.packetNumber;
                m_Backend.QueueStateEvent(device, ref state.gamepad);
            }

            m_Backend.QueueDeviceRemove(device);
        }

        public unsafe long SetState(DualMotorRumbleCommand* rumble)
        {
            var vibration = new XInputVibration()
            {
                leftMotor = (ushort)(rumble->lowFrequencyMotorSpeed * ushort.MaxValue),
                rightMotor = (ushort)(rumble->highFrequencyMotorSpeed * ushort.MaxValue),
            };

            var result = m_Backend.xinput.SetState(userIndex, vibration);
            if (result != 0)
            {
                if (result != ERROR_DEVICE_NOT_CONNECTED)
                    Logging.Error($"Failed to set state for XInput user index {userIndex}: 0x{(int)result:X8}");
                return InputDeviceCommand.GenericFailure;
            }

            return InputDeviceCommand.GenericSuccess;
        }
    }
}
#endif