using System;
using HIDrogen.LowLevel;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen
{
    internal sealed class CustomInputBuffer : IDisposable
    {
        public struct SwapScope : IDisposable
        {
            private readonly SlimEventBuffer m_Buffer;
            private SlimEventBuffer.Enumerator m_Enumerator;

            public InputEventPtr Current => m_Enumerator.Current;

            public SwapScope(SlimEventBuffer buffer)
            {
                m_Buffer = buffer;
                m_Enumerator = buffer.GetEnumerator();
            }

            public SwapScope GetEnumerator() => this;

            public bool MoveNext() => m_Enumerator.MoveNext();

            public void Dispose()
            {
                m_Buffer.Reset();
            }
        }

        // Safety limit, to avoid allocating too much on the stack
        // (InputSystem.StateEventBuffer.kMaxSize)
        public const int MaxStateSize = 512;

        // We use a custom buffering implementation because the built-in implementation is
        // not friendly to managed threads, despite what the docs for InputSystem.QueueEvent/QueueStateEvent
        // may claim, so we need to flush events on the main thread.
        private readonly SlimEventBuffer[] m_EventBuffers = new SlimEventBuffer[2];
        private volatile int m_CurrentBuffer = 0;

        public CustomInputBuffer()
        {
            for (int i = 0; i < m_EventBuffers.Length; i++)
            {
                m_EventBuffers[i] = new SlimEventBuffer();
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < m_EventBuffers.Length; i++)
            {
                m_EventBuffers[i].Dispose();
                m_EventBuffers[i] = null;
            }
        }

        public SwapScope SwapBuffer()
        {
            SlimEventBuffer buffer;
            lock (m_EventBuffers)
            {
                buffer = m_EventBuffers[m_CurrentBuffer];
                m_CurrentBuffer = (m_CurrentBuffer + 1) % m_EventBuffers.Length;
            }

            return new SwapScope(buffer);
        }

        public unsafe void QueueEvent(InputEventPtr eventPtr)
        {
            lock (m_EventBuffers)
            {
                m_EventBuffers[m_CurrentBuffer].AppendEvent(eventPtr);
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
            if (stateBuffer == null || stateLength < 1 || stateLength > MaxStateSize)
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

        public unsafe void QueueDeltaStateEvent<TValue>(InputControl control, ref TValue value)
            where TValue : unmanaged
        {
            QueueDeltaStateEvent(control, UnsafeUtility.AddressOf(ref value), sizeof(TValue));
        }

        public unsafe void QueueDeltaStateEvent<TValue>(InputDevice device, uint offset, ref TValue value)
            where TValue : unmanaged
        {
            QueueDeltaStateEvent(device, offset, UnsafeUtility.AddressOf(ref value), sizeof(TValue));
        }

        // Based on InputSystem.QueueDeltaStateEvent<T>
        public unsafe void QueueDeltaStateEvent(InputControl control, void* stateBuffer, int stateLength)
        {
            var stateBlock = control.stateBlock;
            
            if (stateBlock.bitOffset != 0)
                throw new InvalidOperationException($"Cannot send delta state events against bitfield controls: {control}");

            uint alignedSizeInBytes = (stateBlock.sizeInBits + 7) >> 3;
            if (stateLength != alignedSizeInBytes)
                throw new ArgumentException($"Size {stateLength} of delta state provided for control '{control}' does not match size {alignedSizeInBytes} of control");

            var device = control.device;
            uint offset = stateBlock.byteOffset - device.stateBlock.byteOffset;
            QueueDeltaStateEvent(device, offset, stateBuffer, stateLength);
        }

        // Based on InputSystem.QueueDeltaStateEvent<T>
        public unsafe void QueueDeltaStateEvent(InputDevice device, uint offset, void* stateBuffer, int stateLength)
        {
            if (stateBuffer == null || stateLength < 1 || stateLength > MaxStateSize)
            {
                return;
            }

            // Create state buffer
            int eventSize = stateLength + (sizeof(DeltaStateEvent) - 1); // DeltaStateEvent already includes 1 byte at the end
            byte* _deltaEvent = stackalloc byte[eventSize];
            var deltaEvent = (DeltaStateEvent*)_deltaEvent;

            *deltaEvent = new DeltaStateEvent()
            {
                baseEvent = new InputEvent(DeltaStateEvent.Type, eventSize, device.deviceId),
                stateFormat = device.stateBlock.format,
                stateOffset = offset
            };

            // Copy state data
            UnsafeUtility.MemCpy(deltaEvent->deltaState, stateBuffer, stateLength);

            QueueEvent(&deltaEvent->baseEvent);
        }
    }
}
