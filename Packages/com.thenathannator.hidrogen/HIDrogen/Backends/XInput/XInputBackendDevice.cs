#if UNITY_STANDALONE_WIN
using System;
using System.Threading;
using HIDrogen.Imports.Windows;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend
{
    internal class XInputBackendDevice : IDisposable
    {
        private readonly XInputBackend m_Backend;

        public uint userIndex { get; }
        public InputDevice device { get; }

        private Thread m_ReadThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

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
                var result = XInput.Instance.GetState(userIndex, out var state);
                if (result != Win32Error.ERROR_SUCCESS)
                {
                    if (result != Win32Error.ERROR_DEVICE_NOT_CONNECTED)
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

            var result = XInput.Instance.SetState(userIndex, vibration);
            if (result != 0)
            {
                if (result != Win32Error.ERROR_DEVICE_NOT_CONNECTED)
                    Logging.Error($"Failed to set state for XInput user index {userIndex}: 0x{(int)result:X8}");
                return InputDeviceCommand.GenericFailure;
            }

            return InputDeviceCommand.GenericSuccess;
        }
    }
}
#endif