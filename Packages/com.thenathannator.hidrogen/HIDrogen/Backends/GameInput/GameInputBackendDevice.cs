#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Threading;
using SharpGameInput;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    [Serializable]
    internal struct GameInputDeviceCapabilities
    {
        public int vendorId;
        public int productId;
    }

    internal class GameInputBackendDevice
    {
        private const int E_NOTIMPL = unchecked((int)0x80004001);

        private readonly GameInputBackend m_Backend;
        private IGameInput m_GameInput;
        private IGameInputDevice m_GipDevice;

        public IGameInputDevice gipDevice => m_GipDevice;
        public InputDevice device { get; }

        private Thread m_ReadThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        public GameInputBackendDevice(GameInputBackend backend, IGameInput gameInput, IGameInputDevice gipDevice, InputDevice device)
        {
            m_Backend = backend;
            m_GameInput = gameInput.Duplicate();
            m_GipDevice = gipDevice.Duplicate();
            this.device = device;

            m_ReadThread = new Thread(ReadThread) { IsBackground = true };
            m_ReadThread.Start();
        }

        // No finalizer, all resources are managed

        public void Dispose()
        {
            m_ThreadStop?.Set();
            m_ReadThread?.Join();
            m_ReadThread = null;

            m_ThreadStop?.Dispose();
            m_ThreadStop = null;

            m_GameInput?.Dispose();
            m_GameInput = null;

            m_GipDevice?.Dispose();
            m_GipDevice = null;
        }

        private unsafe void ReadThread()
        {
            if (m_GameInput.RegisterReadingCallback(
                m_GipDevice, GameInputKind.RawDeviceReport, 0, null,
                (token, context, reading, hasOverrunOccurred) =>
                {
                    // Unfolded `using (reading)` statement due to C# 7.3 limitations
                    try
                    {
                        if (!HandleReading(reading))
                            token.Stop();
                    }
                    finally
                    {
                        reading.Dispose();
                    }
                },
                out var readingToken, out int result
            ))
            {
                m_ThreadStop.WaitOne(Timeout.Infinite);
                readingToken.Unregister(1000000);
            }
            else
            {
                // RegisterReadingCallback is not implemented at the time of writing,
                // so we fall back to polling on failure
                if (result != E_NOTIMPL)
                    Logging.Verbose($"Couldn't register GameInput reading callback, falling back to manual polling. Error result: 0x{result:X4}");
                ReadThreaded();
            }

            m_Backend.QueueDeviceRemove(device);
        }

        private unsafe void ReadThreaded()
        {
            ulong lastTimestamp = 0;
            while (!m_ThreadStop.WaitOne(0))
            {
                int hResult = m_GameInput.GetCurrentReading(GameInputKind.RawDeviceReport, m_GipDevice, out var reading);
                if (hResult < 0)
                {
                    if (hResult == (int)GameInputResult.ReadingNotFound)
                        continue;

                    if (hResult != (int)GameInputResult.DeviceDisconnected)
                        Logging.Error($"Failed to get current reading: 0x{hResult:X8}");
                    break;
                }

                // Unfolded `using (reading)` statement due to C# 7.3 limitations
                try
                {
                    // Ignore unchanged reports
                    ulong timestamp = reading.GetTimestamp();
                    if (lastTimestamp == timestamp)
                        continue;
                    lastTimestamp = timestamp;

                    if (!HandleReading(reading))
                        break;
                }
                finally
                {
                    reading.Dispose();
                }
            }
        }

        private unsafe bool HandleReading(LightIGameInputReading reading)
        {
            if (!reading.GetRawReport(out var rawReport))
            {
                Logging.Error("Could not get GameInput raw report!");
                return false;
            }

            // Unfolded `using (rawReport)` statement due to C# 7.3 limitations
            try
            {
                byte reportId = (byte)rawReport.ReportInfo.id;
                UIntPtr reportSize = rawReport.GetRawDataSize();
                UIntPtr bufferSize = reportSize + 1;

                byte* buffer = stackalloc byte[(int)bufferSize];
                buffer[0] = reportId;
                UIntPtr readSize = rawReport.GetRawData(reportSize, buffer + 1);
                Debug.Assert(readSize == reportSize);

                m_Backend.QueueStateEvent(device, GameInputDefinitions.InputFormat, buffer, (int)size);
            }
            finally
            {
                rawReport.Dispose();
            }

            return true;
        }

        public unsafe GameInputCommandResult SendMessage(void* _buffer, int bufferLength)
        {
            if (_buffer == null || bufferLength < 1)
                return GameInputCommandResult.ArgumentError;

            byte* buffer = (byte*)_buffer;
            byte reportId = *buffer++;
            bufferLength--;

            int hResult = m_GipDevice.CreateRawDeviceReport(reportId, GameInputRawDeviceReportKind.Output, out var report);
            if (hResult < 0)
            {
                if (hResult == (int)GameInputResult.DeviceDisconnected)
                    return GameInputCommandResult.Disconnected;

                Logging.Error($"Failed to create raw report: 0x{hResult:X8}");
                return GameInputCommandResult.Failure;
            }

            // Unfolded `using (report)` statement due to C# 7.3 limitations
            try
            {
                if (!report.SetRawData((UIntPtr)bufferLength, buffer))
                {
                    Logging.Error("Failed to set raw report data!");
                    return GameInputCommandResult.Failure;
                }

                hResult = m_GipDevice.SendRawDeviceOutput(report);
                if (hResult < 0)
                {
                    // This call is not implemented as of the time of writing,
                    // ignore and treat as success
                    if (hResult == E_NOTIMPL)
                        return GameInputCommandResult.Success;

                    if (hResult == (int)GameInputResult.DeviceDisconnected)
                        return GameInputCommandResult.Disconnected;

                    Logging.Error($"Failed to send raw report: 0x{hResult:X8}");
                    return GameInputCommandResult.Failure;
                }

                return GameInputCommandResult.Success;
            }
            finally
            {
                report.Dispose();
            }
        }
    }
}
#endif