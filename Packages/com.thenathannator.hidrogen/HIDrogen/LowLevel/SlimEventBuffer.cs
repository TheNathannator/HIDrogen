using System;
using System.Collections;
using System.Collections.Generic;
using HIDrogen.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.LowLevel
{
    /// <summary>
    /// A buffer of memory holding a sequence of <see cref="InputEvent">input events</see>.
    /// Heavily modified and slimmed down from <see cref="InputEventBuffer"/>.
    /// </summary>
    internal unsafe class SlimEventBuffer : IEnumerable<InputEventPtr>, IDisposable
    {
        private static readonly unsafe int s_BaseEventSize = sizeof(InputEvent); // InputEvent.kBaseEventSize
        private const int kEventAlignment = 4; // InputEvent.kAlignment

        private SlimNativeBuffer<byte> m_Buffer = new SlimNativeBuffer<byte>(2048);
        private InputEventPtr m_FirstEvent => m_Buffer != null ? (InputEvent*)m_Buffer.bufferPtr : null;

        private long m_UsedLength;
        private int m_EventCount;

        public void AppendEvent(InputEvent* eventPtr, int capacityIncrementInBytes = 2048)
        {
            if (eventPtr == null)
                throw new ArgumentNullException(nameof(eventPtr));

            // Allocate space
            var eventSizeInBytes = eventPtr->sizeInBytes;
            var destinationPtr = AllocateEvent((int)eventSizeInBytes, capacityIncrementInBytes);

            // Copy event
            UnsafeUtility.MemCpy(destinationPtr, eventPtr, eventSizeInBytes);
        }

        public InputEvent* AllocateEvent(int sizeInBytes, int capacityIncrementInBytes = 2048)
        {
            if (sizeInBytes < s_BaseEventSize)
                throw new ArgumentException(
                    $"sizeInBytes must be >= sizeof(InputEvent) == {s_BaseEventSize} (was {sizeInBytes})", nameof(sizeInBytes));

            var alignedSizeInBytes = sizeInBytes.AlignToMultipleOf(kEventAlignment);

            // Re-allocate the buffer if necessary
            var necessaryCapacity = m_UsedLength + alignedSizeInBytes;
            var currentCapacity = m_Buffer?.length ?? 0;
            if (currentCapacity < necessaryCapacity)
            {
                var newCapacity = necessaryCapacity.AlignToMultipleOf(capacityIncrementInBytes);
                if (newCapacity > int.MaxValue)
                    throw new NotImplementedException("RawBuffer long support");

                var newBuffer = new SlimNativeBuffer<byte>((int)newCapacity);
                if (m_Buffer != null)
                {
                    UnsafeUtility.MemCpy(newBuffer.bufferPtr, m_Buffer.bufferPtr, m_UsedLength);
                    m_Buffer.Dispose();
                }

                m_Buffer = newBuffer;
            }

            // Retrieve pointer to next available unallocated spot
            var eventPtr = (InputEvent*)(m_Buffer.bufferPtr + m_UsedLength);
            eventPtr->sizeInBytes = (uint)sizeInBytes;
            m_UsedLength += alignedSizeInBytes;
            ++m_EventCount;

            return eventPtr;
        }

        public void Reset()
        {
            m_EventCount = 0;
            m_UsedLength = 0;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<InputEventPtr> IEnumerable<InputEventPtr>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_Buffer?.Dispose();
                m_Buffer = null;
            }

            m_UsedLength = 0;
            m_EventCount = 0;
        }

        public struct Enumerator : IEnumerator<InputEventPtr>
        {
            private readonly InputEvent* m_Buffer;
            private readonly int m_EventCount;
            private InputEvent* m_CurrentEvent;
            private int m_CurrentIndex;

            public Enumerator(SlimEventBuffer buffer)
            {
                m_Buffer = buffer.m_FirstEvent;
                m_EventCount = buffer.m_EventCount;
                m_CurrentEvent = null;
                m_CurrentIndex = 0;
            }

            public bool MoveNext()
            {
                if (m_CurrentIndex == m_EventCount)
                    return false;

                if (m_CurrentEvent == null)
                {
                    m_CurrentEvent = m_Buffer;
                    return m_CurrentEvent != null;
                }

                Debug.Assert(m_CurrentEvent != null, "Current event must not be null");

                ++m_CurrentIndex;
                if (m_CurrentIndex == m_EventCount)
                    return false;

                m_CurrentEvent = GetNextInMemory(m_CurrentEvent);
                return true;
            }

            // Copied from InputEvent.GetNextInMemory
            private static InputEventPtr GetNextInMemory(InputEventPtr currentPtr)
            {
                Debug.Assert(currentPtr != null, "Event pointer must not be null!");
                var alignedSizeInBytes = currentPtr.sizeInBytes.AlignToMultipleOf(kEventAlignment);
                return (InputEvent*)((byte*)currentPtr.data + alignedSizeInBytes);
            }

            public void Reset()
            {
                m_CurrentEvent = null;
                m_CurrentIndex = 0;
            }

            public void Dispose()
            {
            }

            public InputEventPtr Current => m_CurrentEvent;

            object IEnumerator.Current => Current;
        }
    }
}
