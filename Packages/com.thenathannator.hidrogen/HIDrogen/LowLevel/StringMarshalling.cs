#if UNITY_STANDALONE
    #if UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
        #define GNU_WCHAR_32
    #else
        #define GNU_WCHAR_16
    #endif
#endif

using System;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;

namespace HIDrogen.LowLevel
{
    /// <summary>
    /// An unmanaged string with ASCII/ANSI C <c>char*</c> encoding.
    /// </summary>
    internal ref struct AnsiString
    {
        private string m_Str;
        private IntPtr m_Ptr;
        private bool m_Owned;

        public bool IsInvalid => m_Str == null && m_Ptr == null;

        public AnsiString(string str)
        {
            m_Str = str;
            m_Ptr = IntPtr.Zero;
            m_Owned = false;
        }

        public AnsiString(IntPtr ptr, bool owned)
        {
            m_Str = null;
            m_Ptr = ptr;
            m_Owned = owned;
        }

        public void Dispose()
        {
            if (m_Ptr != null && m_Owned)
            {
                FreePtr(m_Ptr);
            }

            m_Ptr = IntPtr.Zero;
        }

        public AnsiString Copy()
        {
            var copied = CopyPtr(m_Ptr);
            return new AnsiString(copied, owned: true);
        }

        public IntPtr ToPtr()
        {
            if (m_Ptr == null && m_Str != null)
            {
                m_Ptr = AllocPtr(m_Str);
                m_Owned = true;
            }

            return m_Ptr;
        }

        public override string ToString()
        {
            if (m_Str == null && m_Ptr != null)
            {
                m_Str = UnmarshalPtr(m_Ptr);
            }

            return m_Str;
        }

        public static IntPtr AllocPtr(string str)
        {
            if (str == null)
            {
                return IntPtr.Zero;
            }

            return Marshal.StringToHGlobalAnsi(str);
        }

        public static void FreePtr(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public static string UnmarshalPtr(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringAnsi(ptr);
        }

        public static IntPtr CopyPtr(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            int byteLength = StringMarshal.StrLenByte(ptr) + 1;
            var newPtr = Marshal.AllocHGlobal(byteLength);

            unsafe
            {
                UnsafeUtility.MemCpy((void*)newPtr, (void*)ptr, byteLength);
            }

            return newPtr;
        }
    }

    /// <summary>
    /// An unmanaged string with Unicode encoding, with character size specified via <typeparamref name="TChar"/>.
    /// </summary>
    internal ref struct UnicodeString<TChar>
        where TChar : unmanaged
    {
        private static readonly Encoding s_Encoding;

        static unsafe UnicodeString()
        {
            switch (sizeof(TChar))
            {
                case 1:
                {
                    s_Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
                    break;
                }
                case 2:
                {
                    s_Encoding = new UnicodeEncoding(!BitConverter.IsLittleEndian, byteOrderMark: false, throwOnInvalidBytes: false);
                    break;
                }
                case 4:
                {
                    s_Encoding = new UTF32Encoding(!BitConverter.IsLittleEndian, byteOrderMark: false, throwOnInvalidCharacters: false);
                    break;
                }
                default:
                {
                    throw new ArgumentException(typeof(TChar).Name);
                }
            }
        }

        private string m_Str;
        private IntPtr m_Ptr;
        private bool m_Owned;

        public bool IsInvalid => m_Str == null && m_Ptr == null;

        public UnicodeString(string str)
        {
            m_Str = str;
            m_Ptr = IntPtr.Zero;
            m_Owned = false;
        }

        public UnicodeString(IntPtr ptr, bool owned)
        {
            m_Str = null;
            m_Ptr = ptr;
            m_Owned = owned;
        }

        public void Dispose()
        {
            if (m_Ptr != null && m_Owned)
            {
                FreePtr(m_Ptr);
            }

            m_Ptr = IntPtr.Zero;
        }

        public UnicodeString<TChar> Copy()
        {
            var copied = CopyPtr(m_Ptr);
            return new UnicodeString<TChar>(copied, owned: true);
        }

        public IntPtr ToPtr()
        {
            if (m_Ptr == null && m_Str != null)
            {
                m_Ptr = AllocPtr(m_Str);
                m_Owned = true;
            }

            return m_Ptr;
        }

        public override string ToString()
        {
            if (m_Str == null && m_Ptr != null)
            {
                m_Str = UnmarshalPtr(m_Ptr);
            }

            return m_Str;
        }

        public static unsafe IntPtr AllocPtr(string str)
        {
            if (str == null)
            {
                return IntPtr.Zero;
            }

            // + char size for null terminator
            int byteLength = s_Encoding.GetByteCount(str) + sizeof(TChar);
            var ptr = Marshal.AllocHGlobal(byteLength);

            byte* bytePtr = (byte*)ptr;
            fixed (char* strPtr = str)
            {
                s_Encoding.GetBytes(strPtr, str.Length, bytePtr, byteLength);
            }

            // Apply null terminator
            for (int i = sizeof(TChar); i > 0; i++)
            {
                bytePtr[byteLength - i] = 0;
            }

            return ptr;
        }

        public static void FreePtr(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public static unsafe string UnmarshalPtr(IntPtr ptr)
        {
            if (ptr == null)
            {
                return null;
            }

            int charLength;
            switch (sizeof(TChar))
            {
                case 1: charLength = StringMarshal.StrLenByte(ptr); break;
                case 2: charLength = StringMarshal.StrLenInt16(ptr); break;
                case 4: charLength = StringMarshal.StrLenInt32(ptr); break;
                default: throw new ArgumentOutOfRangeException(typeof(TChar).Name);
            }

            if (charLength < 1)
            {
                return string.Empty;
            }

            return s_Encoding.GetString((byte*)ptr, charLength * sizeof(TChar));
        }

        public static IntPtr CopyPtr(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            int byteLength = StringMarshal.StrLenByte(ptr) + 1;
            var newPtr = Marshal.AllocHGlobal(byteLength);

            unsafe
            {
                UnsafeUtility.MemCpy((void*)newPtr, (void*)ptr, byteLength);
            }

            return newPtr;
        }
    }

    /// <summary>
    /// An unmanaged string with UTF8 encoding.
    /// </summary>
    internal ref struct UTF8String
    {
        private UnicodeString<byte> m_Base;

        public bool IsInvalid => m_Base.IsInvalid;

        public UTF8String(string str)
        {
            m_Base = new UnicodeString<byte>(str);
        }

        public UTF8String(IntPtr ptr, bool owned)
        {
            m_Base = new UnicodeString<byte>(ptr, owned);
        }

        public void Dispose() => m_Base.Dispose();

        public UTF8String Copy() => new UTF8String() { m_Base = m_Base.Copy() };

        public IntPtr ToPtr() => m_Base.ToPtr();
        public override string ToString() => m_Base.ToString();

        public static IntPtr AllocPtr(string str) => UnicodeString<byte>.AllocPtr(str);
        public static void FreePtr(IntPtr ptr) => UnicodeString<byte>.FreePtr(ptr);
        public static string UnmarshalPtr(IntPtr ptr) => UnicodeString<byte>.UnmarshalPtr(ptr);
        public static IntPtr CopyPtr(IntPtr ptr) => UnicodeString<byte>.CopyPtr(ptr);
    }

    /// <summary>
    /// An unmanaged string with UTF16 encoding.
    /// </summary>
    internal ref struct UTF16String
    {
        private UnicodeString<short> m_Base;

        public bool IsInvalid => m_Base.IsInvalid;

        public UTF16String(string str)
        {
            m_Base = new UnicodeString<short>(str);
        }

        public UTF16String(IntPtr ptr, bool owned)
        {
            m_Base = new UnicodeString<short>(ptr, owned);
        }

        public void Dispose() => m_Base.Dispose();

        public UTF16String Copy() => new UTF16String() { m_Base = m_Base.Copy() };

        public IntPtr ToPtr() => m_Base.ToPtr();
        public override string ToString() => m_Base.ToString();

        public static IntPtr AllocPtr(string str) => UnicodeString<short>.AllocPtr(str);
        public static void FreePtr(IntPtr ptr) => UnicodeString<short>.FreePtr(ptr);
        public static string UnmarshalPtr(IntPtr ptr) => UnicodeString<short>.UnmarshalPtr(ptr);
        public static IntPtr CopyPtr(IntPtr ptr) => UnicodeString<short>.CopyPtr(ptr);
    }

    /// <summary>
    /// An unmanaged string with UTF32 encoding.
    /// </summary>
    internal ref struct UTF32String
    {
        private UnicodeString<int> m_Base;

        public bool IsInvalid => m_Base.IsInvalid;

        public UTF32String(string str)
        {
            m_Base = new UnicodeString<int>(str);
        }

        public UTF32String(IntPtr ptr, bool owned)
        {
            m_Base = new UnicodeString<int>(ptr, owned);
        }

        public void Dispose() => m_Base.Dispose();

        public UTF32String Copy() => new UTF32String() { m_Base = m_Base.Copy() };

        public IntPtr ToPtr() => m_Base.ToPtr();
        public override string ToString() => m_Base.ToString();

        public static IntPtr AllocPtr(string str) => UnicodeString<int>.AllocPtr(str);
        public static void FreePtr(IntPtr ptr) => UnicodeString<int>.FreePtr(ptr);
        public static string UnmarshalPtr(IntPtr ptr) => UnicodeString<int>.UnmarshalPtr(ptr);
        public static IntPtr CopyPtr(IntPtr ptr) => UnicodeString<int>.CopyPtr(ptr);
    }

#if GNU_WCHAR_32 || GNU_WCHAR_16
    /// <summary>
    /// An unmanaged string with GNU C <c>wchar_t*</c> Unicode encoding (UTF16 on Windows, UTF32 on Linux or Mac).
    /// </summary>
    internal ref struct GnuWideString
    {
#if GNU_WCHAR_32
        private UTF32String m_Base;
#else
        private UTF16String m_Base;
#endif

        public bool IsInvalid => m_Base.IsInvalid;

        public GnuWideString(string str)
        {
#if GNU_WCHAR_32
            m_Base = new UTF32String(str);
#else
            m_Base = new UTF16String(str);
#endif
        }

        public GnuWideString(IntPtr ptr, bool owned)
        {
#if GNU_WCHAR_32
            m_Base = new UTF32String(ptr, owned);
#else
            m_Base = new UTF16String(ptr, owned);
#endif
        }

        public void Dispose() => m_Base.Dispose();

        public GnuWideString Copy() => new GnuWideString() { m_Base = m_Base.Copy() };

        public IntPtr ToPtr() => m_Base.ToPtr();
        public override string ToString() => m_Base.ToString();

#if GNU_WCHAR_32
        public static IntPtr AllocPtr(string str) => UTF32String.AllocPtr(str);
        public static void FreePtr(IntPtr ptr) => UTF32String.FreePtr(ptr);
        public static string UnmarshalPtr(IntPtr ptr) => UTF32String.UnmarshalPtr(ptr);
        public static IntPtr CopyPtr(IntPtr ptr) => UTF32String.CopyPtr(ptr);
#else
        public static IntPtr AllocPtr(string str) => UTF16String.AllocPtr(str);
        public static void FreePtr(IntPtr ptr) => UTF16String.FreePtr(ptr);
        public static string UnmarshalPtr(IntPtr ptr) => UTF16String.UnmarshalPtr(ptr);
        public static IntPtr CopyPtr(IntPtr ptr) => UTF16String.CopyPtr(ptr);
#endif
    }
#endif

    /// <summary>
    /// Marshals strings between different formats.
    /// </summary>
    internal static unsafe class StringMarshal
    {
        public static int StrLenByte(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return 0;
            }

            byte* start = (byte*)ptr;
            byte* end = (byte*)ptr;
            while (*end != 0)
            {
                checked { end++; }
            }

            return checked((int)(end - start));
        }

        public static int StrLenInt16(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return 0;
            }

            short* start = (short*)ptr;
            short* end = (short*)ptr;
            while (*end != 0)
            {
                checked { end++; }
            }

            return checked((int)(end - start));
        }

        public static int StrLenInt32(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return 0;
            }

            int* start = (int*)ptr;
            int* end = (int*)ptr;
            while (*end != 0)
            {
                checked { end++; }
            }

            return checked((int)(end - start));
        }
    }
}