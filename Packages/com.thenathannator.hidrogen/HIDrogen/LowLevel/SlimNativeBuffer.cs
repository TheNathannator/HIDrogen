using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace HIDrogen.LowLevel
{
    /// <summary>
    /// A slim, no-nonsense implementation for a buffer of native memory.
    /// </summary>
    internal unsafe class SlimNativeBuffer<T> : IDisposable
        where T : unmanaged
    {
        private const Allocator kAllocator = Allocator.Persistent;

        private T* m_Buffer;
        private long m_Length;
        private int m_Count;

        public T* bufferPtr => m_Buffer;
        public int count => m_Count;
        public long length => m_Length;

        public SlimNativeBuffer(int count, int alignment = 1)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Must allocate at least one element!");
            if (alignment < 1)
                throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Must align to at least a byte boundary!");

            m_Count = count;
            m_Length = count * sizeof(T);
            m_Buffer = (T*)UnsafeUtility.Malloc(m_Length, alignment, kAllocator);
            if (m_Buffer == null)
                throw new OutOfMemoryException("Failed to allocate memory for the buffer!");
            UnsafeUtility.MemClear(m_Buffer, m_Length);
        }

        ~SlimNativeBuffer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (m_Buffer != null)
            {
                UnsafeUtility.Free(m_Buffer, kAllocator);
                m_Buffer = null;
            }

            m_Length = 0;
            m_Count = 0;
        }
    }
}