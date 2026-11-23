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

        public SlimNativeBuffer(int count)
        {
            Alloc(kAllocator, count, out m_Count, out m_Length, out m_Buffer);
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
            Free(kAllocator, ref m_Count, ref m_Length, ref m_Buffer);
        }

        public static void Alloc(
            Allocator allocator,
            int count,
            out int countField,
            out long lengthField,
            out T* bufferField
        )
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            countField = count;
            lengthField = count * sizeof(T);

            bufferField = (T*)UnsafeUtility.Malloc(lengthField, IntPtr.Size, allocator);
            if (bufferField == null)
            {
                throw new OutOfMemoryException("Failed to allocate memory for the buffer!");
            }

            UnsafeUtility.MemClear(bufferField, lengthField);
        }

        public static void Free(
            Allocator allocator,
            ref int countField,
            ref long lengthField,
            ref T* bufferField
        )
        {
            if (bufferField != null)
            {
                UnsafeUtility.Free(bufferField, allocator);
                bufferField = null;
            }

            lengthField = 0;
            countField = 0;
        }
    }

    internal unsafe struct TempNativeBuffer<T> : IDisposable
        where T : unmanaged
    {
        private const Allocator kAllocator = Allocator.Temp;

        private T* m_Buffer;
        private long m_Length;
        private int m_Count;

        public T* bufferPtr => m_Buffer;
        public int count => m_Count;
        public long length => m_Length;

        public TempNativeBuffer(int count)
        {
            SlimNativeBuffer<T>.Alloc(kAllocator, count, out m_Count, out m_Length, out m_Buffer);
        }

        public void Dispose()
        {
            SlimNativeBuffer<T>.Free(kAllocator, ref m_Count, ref m_Length, ref m_Buffer);
        }
    }
}