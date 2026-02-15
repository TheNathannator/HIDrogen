using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HIDrogen.LowLevel;
using Unity.Collections.LowLevel.Unsafe;
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
        // Safety limit, to avoid allocating too much on the stack
        // (InputSystem.StateEventBuffer.kMaxSize)
        protected const int kMaxStateSize = 512;

        // Map from input system instance to backend instance
        private readonly Dictionary<InputDevice, TBackendDevice> m_DeviceLookup
            = new Dictionary<InputDevice, TBackendDevice>();

        // Queues for devices; they must be managed on the main thread
        private readonly ConcurrentBag<(InputDeviceDescription description, TAddContext context)> m_AdditionQueue
            = new ConcurrentBag<(InputDeviceDescription, TAddContext)>();

        // We use a custom buffering implementation because the built-in implementation is
        // not friendly to managed threads, despite what the docs for InputSystem.QueueEvent/QueueStateEvent
        // may claim, so we need to flush events on the main thread.
        private readonly SlimEventBuffer[] m_InputBuffers = new SlimEventBuffer[2];
        private volatile int m_CurrentBuffer = 0;

        private bool m_Started = false;

        protected CustomInputBackend()
        {
            for (int i = 0; i < m_InputBuffers.Length; i++)
            {
                m_InputBuffers[i] = new SlimEventBuffer();
            }
        }

        ~CustomInputBackend()
        {
            Logging.Error($"Input backend {GetType()} was not disposed correctly! " +
                "Input system resources cannot safely be reclaimed on the finalizer thread.");
        }

        public void Dispose()
        {
            Stop();
            OnDispose();

            foreach (var buffer in m_InputBuffers)
            {
                buffer.Dispose();
            }

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
            SlimEventBuffer buffer;
            lock (m_InputBuffers)
            {
                buffer = m_InputBuffers[m_CurrentBuffer];
                m_CurrentBuffer = (m_CurrentBuffer + 1) % m_InputBuffers.Length;
            }

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
            buffer.Reset();
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
            var removeEvent = DeviceRemoveEvent.Create(device.deviceId);
            QueueEvent(ref removeEvent);
        }

        public unsafe void QueueEvent(InputEventPtr eventPtr)
        {
            lock (m_InputBuffers)
            {
                m_InputBuffers[m_CurrentBuffer].AppendEvent(eventPtr);
            }
        }

        public unsafe void QueueEvent<TEvent>(ref TEvent inputEvent)
            where TEvent : struct, IInputEventTypeInfo
        {
            QueueEvent((InputEvent*)UnsafeUtility.AddressOf(ref inputEvent));
        }

        public unsafe void QueueStateEvent<TState>(InputDevice device, ref TState state)
            where TState : unmanaged, IInputStateTypeInfo
        {
            QueueStateEvent(device, state.format, UnsafeUtility.AddressOf(ref state), sizeof(TState));
        }

        public unsafe void QueueStateEvent(InputDevice device, FourCC format, byte[] stateBuffer)
        {
            fixed (byte* ptr = stateBuffer)
            {
                QueueStateEvent(device, format, ptr, stateBuffer.Length);
            }
        }

        public unsafe void QueueStateEvent(InputDevice device, FourCC format, byte[] stateBuffer, int offset, int length)
        {
            if (stateBuffer.Length < offset)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if ((stateBuffer.Length - offset) < length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            fixed (byte* ptr = stateBuffer)
            {
                QueueStateEvent(device, format, ptr + offset, length);
            }
        }

        // Based on InputSystem.QueueStateEvent<T>
        public unsafe void QueueStateEvent(InputDevice device, FourCC format, void* stateBuffer, int stateLength)
        {
            if (stateBuffer == null || stateLength < 1 || stateLength > kMaxStateSize)
            {
                return;
            }

            // Create state buffer
            int eventSize = stateLength + (sizeof(StateEvent) - 1); // StateEvent already includes 1 byte at the end
            byte* _stateEvent = stackalloc byte[eventSize];
            StateEvent* stateEvent = (StateEvent*)_stateEvent;
            *stateEvent = new StateEvent
            {
                baseEvent = new InputEvent(StateEvent.Type, eventSize, device.deviceId),
                stateFormat = format
            };

            // Copy state data
            UnsafeUtility.MemCpy(stateEvent->state, stateBuffer, stateLength);

            // Queue state event
            QueueEvent((InputEvent*)stateEvent);
        }
    }
}