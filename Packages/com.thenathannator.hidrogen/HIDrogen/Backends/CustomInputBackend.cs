using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly Dictionary<InputDevice, TBackendDevice> m_DeviceLookup
            = new Dictionary<InputDevice, TBackendDevice>();

        // Queues for devices; they must be managed on the main thread
        private readonly ConcurrentBag<(InputDeviceDescription description, TAddContext context)> m_AdditionQueue
            = new ConcurrentBag<(InputDeviceDescription, TAddContext)>();
        internal readonly ConcurrentBag<InputDevice> m_RemovalQueue = new ConcurrentBag<InputDevice>();

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
                    OnDeviceRemoved(pair.Value);
                    InputSystem.RemoveDevice(pair.Key);
                }
                m_DeviceLookup.Clear();

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
                if (!m_DeviceLookup.TryGetValue(device, out var backendDevice))
                    return;

                OnDeviceRemoved(backendDevice);
                m_DeviceLookup.Remove(device);
            }
        }

        public unsafe long? OnDeviceCommand(InputDevice device, InputDeviceCommand* command)
        {
            CheckStarted();

            if (!m_DeviceLookup.TryGetValue(device, out var backendDevice))
            {
                return null;
            }

            return OnDeviceCommand(backendDevice, command);
        }

        private void FlushDeviceQueue()
        {
            // Process device removals first, for backends that may remove and re-add a device in the same update
            while (m_RemovalQueue.TryTake(out var device))
            {
                InputSystem.RemoveDevice(device);
            }

            while (m_AdditionQueue.TryTake(out var _context))
            {
                var (description, context) = _context;
                using (context)
                {
                    // The input system will throw if a device layout can't be found
                    InputDevice device;
                    try
                    {
                        device = InputSystem.AddDevice(description);
                    }
                    catch (ArgumentException)
                    {
                        // Ignore layout-not-found exception
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Logging.Exception("Failed to add device to the input system!", ex);
                        continue;
                    }

                    try
                    {
                        var backendDevice = OnDeviceAdded(device, context);
                        m_DeviceLookup.Add(device, backendDevice);
                    }
                    catch (Exception ex)
                    {
                        InputSystem.RemoveDevice(device);
                        Logging.Exception("Error in device added callback!", ex);
                        continue;
                    }
                }
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

        public void QueueDeviceAdd(InputDeviceDescription description, TAddContext context)
        {
            m_AdditionQueue.Add((description, context));
        }

        public void QueueDeviceRemove(InputDevice device)
        {
            m_RemovalQueue.Add(device);
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