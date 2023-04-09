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

        public IEnumerator<InputEventPtr> GetEnumerator()
        {
            int currentIndex = 0;
            InputEventPtr currentEvent = m_FirstEvent;
            while (currentIndex < m_EventCount)
            {
                if (!currentEvent.valid)
                    break;

                yield return currentEvent;
                currentEvent = getNextInMemory(currentEvent);
                ++currentIndex;
            }
            yield break;

            // Copied from InputEvent.GetNextInMemory
            unsafe InputEventPtr getNextInMemory(InputEventPtr currentPtr)
            {
                Debug.Assert(currentPtr != null, "Event pointer must not be null!");
                var alignedSizeInBytes = currentPtr.sizeInBytes.AlignToMultipleOf(kEventAlignment);
                return (InputEvent*)((byte*)currentPtr.data + alignedSizeInBytes);
            }
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
    }
}
