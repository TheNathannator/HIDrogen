using System;

namespace HIDrogen.Utilities
{
    internal static class BufferUtils
    {
        public static void ValidateBufferArguments(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if ((uint)offset > (uint)buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if ((uint)(offset + count) > (uint)buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }
    }
}