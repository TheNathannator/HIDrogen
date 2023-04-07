using System;
using System.Text;

namespace HIDrogen.LowLevel
{
    /// <summary>
    /// Marshals strings between different formats.
    /// </summary>
    internal static unsafe class StringMarshal
    {
        public static string FromNullTerminatedAscii(byte* ptr)
        {
            if (ptr == null)
                return null;

            // Get length of string
            int length = 0;
            byte* lengthPtr = ptr;
            checked
            {
                while (*lengthPtr != 0)
                {
                    lengthPtr++;
                    length += sizeof(byte);
                }
            }
            if (length < 1)
                return string.Empty;

            // Convert the string
            return Encoding.ASCII.GetString(ptr, length);
        }

        public static string FromNullTerminatedUtf16(byte* ptr)
        {
            if (ptr == null)
                return null;

            // Get length of string
            int length = 0;
            short* lengthPtr = (short*)ptr;
            checked
            {
                while (*lengthPtr != 0)
                {
                    lengthPtr++;
                    length += sizeof(short);
                }
            }
            if (length < 1)
                return string.Empty;

            // Convert the string
            if (BitConverter.IsLittleEndian)
                return Encoding.Unicode.GetString(ptr, length);
            else
                return Encoding.BigEndianUnicode.GetString(ptr, length);
        }

        public static string FromNullTerminatedUtf32(byte* ptr)
        {
            if (ptr == null)
                return null;

            // Get length of string
            int length = 0;
            int* lengthPtr = (int*)ptr;
            checked
            {
                while (*lengthPtr != 0)
                {
                    lengthPtr++;
                    length += sizeof(int);
                }
            }
            if (length < 1)
                return string.Empty;

            // Convert the string
            return Encoding.UTF32.GetString(ptr, length);
        }

        public static byte[] ToNullTerminatedAscii(string str)
        {
            // Get encoded bytes
            var bytes = Encoding.ASCII.GetBytes(str);

            // Add a null terminator
            var buffer = new byte[bytes.Length + sizeof(byte)];
            bytes.CopyTo(buffer, 0);
            buffer[buffer.Length - 1] = 0;

            return buffer;
        }

        public static byte[] ToNullTerminatedUtf16(string str)
        {
            // Get encoded bytes
            var bytes = Encoding.Unicode.GetBytes(str);

            // Add a null terminator
            var buffer = new byte[bytes.Length + sizeof(short)];
            bytes.CopyTo(buffer, 0);
            buffer[buffer.Length - 1] = 0;
            buffer[buffer.Length - 2] = 0;

            return buffer;
        }

        public static byte[] ToNullTerminatedUtf32(string str)
        {
            // Get encoded bytes
            var bytes = Encoding.UTF32.GetBytes(str);

            // Add a null terminator
            var buffer = new byte[bytes.Length + sizeof(int)];
            bytes.CopyTo(buffer, 0);
            buffer[buffer.Length - 1] = 0;
            buffer[buffer.Length - 2] = 0;
            buffer[buffer.Length - 3] = 0;
            buffer[buffer.Length - 4] = 0;

            return buffer;
        }
    }
}