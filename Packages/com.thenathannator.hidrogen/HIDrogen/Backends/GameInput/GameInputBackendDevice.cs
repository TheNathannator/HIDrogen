#if UNITY_STANDALONE_WIN
using System;
using System.Threading;
using HIDrogen.Imports.Windows;
using SharpGameInput.v0;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
        private readonly GameInputBackend m_Backend;
        private IGameInput m_GameInput;
        private IGameInputDevice m_GipDevice;

        public IGameInputDevice gipDevice => m_GipDevice;
        public InputDevice device { get; }

        private Thread m_ReadThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        private NativeArray<byte> m_LastReport = new NativeArray<byte>(1, Allocator.Persistent);
        private int m_LastReportLength = 0;

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

            m_LastReport.Dispose();

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
                if (result != (int)HResult.E_NOTIMPL)
                    Logging.Verbose($"Couldn't register GameInput reading callback, falling back to manual polling. Error result: 0x{result:X4}");
                ReadThreaded();
            }

            m_Backend.QueueDeviceRemove(device);
        }

        private unsafe void ReadThreaded()
        {
            // We unfortunately can't rely on timestamp to determine state change,
            // as guitar axis changes do not change the timestamp
            // ulong lastTimestamp = 0;
            while (!m_ThreadStop.WaitOne(1))
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
                    // // Ignore unchanged reports
                    // ulong timestamp = reading.GetTimestamp();
                    // if (lastTimestamp == timestamp)
                    //     continue;
                    // lastTimestamp = timestamp;

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
                Logging.Error("Couldn't get GameInput raw report!");
                return false;
            }

            // Unfolded `using (rawReport)` statement due to C# 7.3 limitations
            try
            {
                const int minReportSize = 10;
                const int maxReportSize = 64 + 1; // + 1 to accomodate report ID

                byte* buffer = stackalloc byte[maxReportSize];
                buffer[0] = (byte)rawReport.GetReportInfo().id;

                int readSize = (int)rawReport.GetRawData((UIntPtr)maxReportSize - 1, buffer + 1);
                readSize += 1; // + 1 to accomodate report ID

                // hack: ensure a minimum report size, for PlasticBand state translation purposes
                // excess bytes will be invisible to any device layouts not expecting them
                if (readSize < minReportSize)
                {
                    // stack-allocated buffers are not initialized, make sure the excess bytes are zeroed out
                    UnsafeUtility.MemSet(buffer + readSize, 0, minReportSize - readSize);
                    readSize = minReportSize;
                }

                if (readSize == m_LastReportLength &&
                    UnsafeUtility.MemCmp(buffer, m_LastReport.GetUnsafeReadOnlyPtr(), readSize) == 0)
                {
                    return true;
                }

                if (m_LastReport.Length < readSize)
                {
                    m_LastReport.Dispose();
                    m_LastReport = new NativeArray<byte>(readSize, Allocator.Persistent);
                }

                UnsafeUtility.MemCpy(m_LastReport.GetUnsafePtr(), buffer, readSize);
                m_LastReportLength = readSize;

                m_Backend.QueueStateEvent(device, GameInputBackend.InputFormat, buffer, readSize);
            }
            finally
            {
                rawReport.Dispose();
            }

            return true;
        }

        public unsafe long SendMessage(void* _buffer, int bufferLength)
        {
            if (_buffer == null || bufferLength < 1)
                return InputDeviceCommand.GenericFailure;

            byte* buffer = (byte*)_buffer;
            byte reportId = *buffer++;
            bufferLength--;

            int hResult = m_GipDevice.CreateRawDeviceReport(reportId, GameInputRawDeviceReportKind.Output, out var report);
            if (hResult < 0)
            {
                if (hResult == (int)GameInputResult.DeviceDisconnected)
                    return InputDeviceCommand.GenericFailure;

                Logging.Error($"Failed to create raw report: 0x{hResult:X8}");
                return InputDeviceCommand.GenericFailure;
            }

            // Unfolded `using (report)` statement due to C# 7.3 limitations
            try
            {
                if (!report.SetRawData((UIntPtr)bufferLength, buffer))
                {
                    Logging.Error("Failed to set raw report data!");
                    return InputDeviceCommand.GenericFailure;
                }

                hResult = m_GipDevice.SendRawDeviceOutput(report);
                if (hResult < 0)
                {
                    // This call is not implemented as of the time of writing,
                    // ignore and treat as success
                    if (hResult == (int)HResult.E_NOTIMPL)
                        return InputDeviceCommand.GenericSuccess;

                    if (hResult == (int)GameInputResult.DeviceDisconnected)
                        return InputDeviceCommand.GenericFailure;

                    Logging.Error($"Failed to send raw report: 0x{hResult:X8}");
                    return InputDeviceCommand.GenericFailure;
                }

                return InputDeviceCommand.GenericSuccess;
            }
            finally
            {
                report.Dispose();
            }
        }
    }
}
#endif