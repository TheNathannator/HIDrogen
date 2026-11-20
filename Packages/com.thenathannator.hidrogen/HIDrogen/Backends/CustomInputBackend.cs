using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen
{
    internal interface ICustomInputBackend : IDisposable
    {
        void Start();
        void Stop();

        void Update();
        void OnDeviceChange(InputDevice device, InputDeviceChange change);
        unsafe long? OnDeviceCommand(InputDevice device, InputDeviceCommand* command);
    }

    internal abstract class CustomInputBackend<TBackendDevice, TAddContext> : ICustomInputBackend
        where TBackendDevice : class
        where TAddContext : IDisposable
    {
        private readonly CustomInputBuffer m_InputBuffer = new CustomInputBuffer();

        // Map from input system instance to backend instance
        private readonly Dictionary<int, TBackendDevice> m_DeviceLookup
            = new Dictionary<int, TBackendDevice>();

        // Queues for devices; they must be managed on the main thread
        private readonly ConcurrentBag<(InputDeviceDescription description, TAddContext context)> m_AdditionQueue
            = new ConcurrentBag<(InputDeviceDescription, TAddContext)>();
        internal readonly ConcurrentBag<int> m_RemovalQueue = new ConcurrentBag<int>();
        private readonly ConcurrentBag<int> m_PendingRemovals = new ConcurrentBag<int>();
        private int m_MainThreadId;

        private bool m_Started = false;

        ~CustomInputBackend()
        {
            Logging.Error($"Input backend {GetType()} was not disposed correctly! " +
                "Input system resources cannot safely be reclaimed on the finalizer thread.");
        }

        public void Dispose()
        {
            Stop();
            OnDispose();

            m_InputBuffer.Dispose();

            GC.SuppressFinalize(this);
        }

        private void CheckStarted()
        {
            if (!m_Started)
            {
                throw new InvalidOperationException("Backend has not been started yet!");
            }
        }

        public void Start()
        {
            if (!m_Started)
            {
                m_Started = true;
                m_MainThreadId = Thread.CurrentThread.ManagedThreadId;
                OnStart();
            }
        }

        public void Stop()
        {
            if (m_Started)
            {
                m_Started = false;

                while (m_AdditionQueue.TryTake(out var pair))
                {
                    pair.context?.Dispose();
                }

                foreach (var pair in m_DeviceLookup)
                {
                    // need to defer to prevent modifying the lookup while enumerating it
                    m_RemovalQueue.Add(pair.Key);
                }

                while (m_RemovalQueue.TryTake(out var deviceId))
                {
                    RemoveDeviceNow(deviceId);
                }

                m_DeviceLookup.Clear();

                m_MainThreadId = 0;
                OnStop();
            }
        }

        public void Update()
        {
            CheckStarted();

            OnUpdate();
            FlushDeviceQueue();
            FlushEventBuffer();
        }

        public void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            CheckStarted();

            if (change == InputDeviceChange.Removed)
            {
                m_PendingRemovals.Add(device.deviceId);
            }
        }

        public unsafe long? OnDeviceCommand(InputDevice device, InputDeviceCommand* command)
        {
            CheckStarted();

            if (!m_DeviceLookup.TryGetValue(device.deviceId, out var backendDevice))
            {
                return null;
            }

            return OnDeviceCommand(backendDevice, command);
        }

        private void FlushDeviceQueue()
        {
            // Process removals from InputSystem, unless they were recreated
            while (m_PendingRemovals.TryTake(out var deviceId))
            {
                if (InputSystem.GetDeviceById(deviceId) == null)
                {
                    RemoveBackendDevice(deviceId);
                }
            }
            // Process device removals first, for backends that may remove and re-add a device in the same update
            while (m_RemovalQueue.TryTake(out var deviceId))
            {
                RemoveDeviceNow(deviceId);
            }
            while (m_AdditionQueue.TryTake(out var _context))
            {
                var (description, context) = _context;
                AddDeviceNow(description, context);
            }
        }

        private void FlushEventBuffer()
        {
            using (var buffer = m_InputBuffer.SwapBuffer())
            {
                foreach (var eventPtr in buffer)
                {
                    try
                    {
                        InputSystem.QueueEvent(eventPtr);
                    }
                    catch (Exception ex)
                    {
                        Logging.Exception("Error when flushing an event!", ex);
                    }
                }
            }
        }

        protected abstract void OnDispose();

        protected virtual void OnStart() {}
        protected virtual void OnStop() {}
        protected virtual void OnUpdate() {}

        protected abstract TBackendDevice OnDeviceAdded(InputDevice device, TAddContext context);
        protected abstract void OnDeviceRemoved(TBackendDevice device);

        protected virtual unsafe long? OnDeviceCommand(TBackendDevice device, InputDeviceCommand* command) => null;

        public bool AddDeviceNow(InputDeviceDescription description, TAddContext context)
        {
            if (Thread.CurrentThread.ManagedThreadId != m_MainThreadId)
            {
                throw new InvalidOperationException("Devices must be added on the main thread");
            }

            using (context)
            {
                // The input system will throw if a device layout can't be found
                InputDevice device;
                try
                {
                    device = InputSystem.AddDevice(description);
                }
                catch (Exception ex)
                {
                    // Exception filters not used here, as they ended up being more surprising
                    // than helpful with how non-matching ArgumentExceptions were allowed to go
                    // uncaught, and addressing that with a second ArgumentException block with
                    // no filter just resulted in duplication of the log message

                    if (ex is ArgumentException argEx && argEx.ParamName == "description")
                    {
                        // Silence layout-not-found exception
                        Logging.Verbose(ex);
                    }
                    else
                    {
                        Logging.Exception($"Failed to add device to the input system! (Description '{description}')", ex);
                    }

                    return false;
                }

                try
                {
                    var backendDevice = OnDeviceAdded(device, context);
                    m_DeviceLookup.Add(device.deviceId, backendDevice);
                    return true;
                }
                catch (Exception ex)
                {
                    InputSystem.RemoveDevice(device);
                    Logging.Exception("Error in device added callback!", ex);
                    return false;
                }
            }
        }

        public bool RemoveDeviceNow(int deviceId)
        {
            if (Thread.CurrentThread.ManagedThreadId != m_MainThreadId)
            {
                throw new InvalidOperationException("Devices must be removed on the main thread");
            }

            try
            {
                RemoveBackendDevice(deviceId);
                var current = InputSystem.GetDeviceById(deviceId);
                if (current != null)
                {
                    InputSystem.RemoveDevice(current);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logging.Exception("Failed to remove device from backend!", ex);
                return false;
            }
        }

        public bool RemoveDeviceNow(InputDevice device)
        {
            if (device == null)
                return false;

            return RemoveDeviceNow(device.deviceId);
        }
        
        private void RemoveBackendDevice(int deviceId)
        {
            if (m_DeviceLookup.TryGetValue(deviceId, out var backendDevice))
            {
                OnDeviceRemoved(backendDevice);
                m_DeviceLookup.Remove(deviceId);
            }
        }

        public void QueueDeviceAdd(InputDeviceDescription description, TAddContext context)
        {
            m_AdditionQueue.Add((description, context));
        }

        public void QueueDeviceRemove(InputDevice device)
        {
            if (device != null)
                m_RemovalQueue.Add(device.deviceId);
        }

        public void QueueEvent(InputEventPtr eventPtr)
        {
            m_InputBuffer.QueueEvent(eventPtr);
        }

        public void QueueEvent<TEvent>(ref TEvent inputEvent)
            where TEvent : struct, IInputEventTypeInfo
        {
            m_InputBuffer.QueueEvent(ref inputEvent);
        }

        public void QueueStateEvent<TState>(InputDevice device, ref TState state)
            where TState : unmanaged, IInputStateTypeInfo
        {
            m_InputBuffer.QueueStateEvent(device, ref state);
        }

        public void QueueStateEvent(InputDevice device, FourCC format, byte[] stateBuffer)
        {
            m_InputBuffer.QueueStateEvent(device, format, stateBuffer);
        }

        public void QueueStateEvent(InputDevice device, FourCC format, byte[] stateBuffer, int offset, int length)
        {
            m_InputBuffer.QueueStateEvent(device, format, stateBuffer, offset, length);
        }

        public unsafe void QueueStateEvent(InputDevice device, FourCC format, void* stateBuffer, int stateLength)
        {
            m_InputBuffer.QueueStateEvent(device, format, stateBuffer, stateLength);
        }

        public void QueueDeltaStateEvent<TValue>(InputControl control, ref TValue value)
            where TValue : unmanaged
        {
            m_InputBuffer.QueueDeltaStateEvent(control, ref value);
        }

        public void QueueDeltaStateEvent<TValue>(InputDevice device, uint offset, ref TValue value)
            where TValue : unmanaged
        {
            m_InputBuffer.QueueDeltaStateEvent(device, offset, ref value);
        }

        public unsafe void QueueDeltaStateEvent(InputControl control, void* stateBuffer, int stateLength)
        {
            m_InputBuffer.QueueDeltaStateEvent(control, stateBuffer, stateLength);
        }

        public unsafe void QueueDeltaStateEvent(InputDevice device, uint offset, void* stateBuffer, int stateLength)
        {
            m_InputBuffer.QueueDeltaStateEvent(device, offset, stateBuffer, stateLength);
        }
    }
}