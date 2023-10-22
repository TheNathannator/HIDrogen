using System;
using System.Text;

namespace HIDrogen.LowLevel
{
    /// <summary>
    /// Marshals strings between different formats.
    /// </summary>
    internal static unsafe class StringMarshal
    {
        private static readonly UTF32Encoding s_BigEndianUTF32 = new UTF32Encoding(true, false);

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
            if (BitConverter.IsLittleEndian)
                return Encoding.UTF32.GetString(ptr, length);
            else
                return s_BigEndianUTF32.GetString(ptr, length);
        }

        public static byte[] ToNullTerminatedAscii(string str)
        {
            return ToNullTerminatedCore(str, sizeof(byte), Encoding.ASCII);
        }

        public static byte[] ToNullTerminatedUtf16(string str)
        {
            var encoding = BitConverter.IsLittleEndian ? Encoding.Unicode : Encoding.BigEndianUnicode;
            return ToNullTerminatedCore(str, sizeof(short), encoding);
        }

        public static byte[] ToNullTerminatedUtf32(string str)
        {
            var encoding = BitConverter.IsLittleEndian ? Encoding.UTF32 : s_BigEndianUTF32;
            return ToNullTerminatedCore(str, sizeof(int), encoding);
        }

        private static byte[] ToNullTerminatedCore(string str, int charSize, Encoding encoding)
        {
            // Get encoded bytes
            int byteLength = encoding.GetByteCount(str);
            var bytes = new byte[byteLength + charSize];
            encoding.GetBytes(str, 0, str.Length, bytes, 0);

            // Ensure null terminated
            for (int i = 1; i <= charSize; i++)
            {
                bytes[bytes.Length - i] = 0;
            }

            return bytes;
        }
    }
}