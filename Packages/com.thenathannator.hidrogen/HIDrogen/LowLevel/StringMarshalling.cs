#if UNITY_STANDALONE
    #if UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
        #define GNU_WCHAR_32
    #else
        #define GNU_WCHAR_16
    #endif
#endif

using System;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace HIDrogen.LowLevel
{
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

        public bool IsInvalid => m_Str == null && m_Ptr == IntPtr.Zero;

        public static UnicodeString<TChar> WithCharLength(int length)
        {
            return new UnicodeString<TChar>()
            {
                m_Str = null,
                m_Ptr = AllocCharLength(length),
                m_Owned = true,
            };
        }

        public static UnicodeString<TChar> WithByteLength(int length)
        {
            return new UnicodeString<TChar>()
            {
                m_Str = null,
                m_Ptr = AllocByteLength(length),
                m_Owned = true,
            };
        }

        public static UnicodeString<TChar> FromStr(string str)
        {
            return new UnicodeString<TChar>()
            {
                m_Str = str,
                m_Ptr = IntPtr.Zero,
                m_Owned = false,
            };
        }

        public static UnicodeString<TChar> FromPtr(IntPtr ptr, bool owned)
        {
            return new UnicodeString<TChar>()
            {
                m_Str = null,
                m_Ptr = ptr,
                m_Owned = owned,
            };
        }

        public void Dispose()
        {
            if (m_Ptr != IntPtr.Zero && m_Owned)
            {
                Free(m_Ptr);
            }

            m_Ptr = IntPtr.Zero;
        }

        public UnicodeString<TChar> Copy()
        {
            var copied = Copy(m_Ptr);
            return FromPtr(copied, owned: true);
        }

        public IntPtr ToPtr()
        {
            if (m_Ptr == IntPtr.Zero && m_Str != null)
            {
                m_Ptr = Marshal(m_Str);
                m_Owned = true;
            }

            return m_Ptr;
        }

        public override string ToString()
        {
            if (m_Str == null && m_Ptr != IntPtr.Zero)
            {
                m_Str = Unmarshal(m_Ptr);
            }

            return m_Str;
        }

        public static IntPtr AllocCharLength(int count)
        {
            return StringMarshal.AllocCharLength<TChar>(count);
        }

        public static IntPtr AllocByteLength(int count)
        {
            return StringMarshal.AllocByteLength<TChar>(count);
        }

        public static void Free(IntPtr ptr)
        {
            StringMarshal.Free(ptr);
        }

        public static IntPtr Marshal(string str)
        {
            return StringMarshal.Marshal<TChar>(s_Encoding, str);
        }

        public static string Unmarshal(IntPtr ptr)
        {
            return StringMarshal.Unmarshal<TChar>(s_Encoding, ptr);
        }

        public static IntPtr Copy(IntPtr ptr)
        {
            return StringMarshal.Copy<TChar>(ptr);
        }
    }

    /// <summary>
    /// An unmanaged string with UTF8 encoding.
    /// </summary>
    internal ref struct UTF8String
    {
        private UnicodeString<byte> m_Base;

        public bool IsInvalid => m_Base.IsInvalid;

        public static UTF8String WithCharLength(int length)
        {
            return new UTF8String()
            {
                m_Base = UnicodeString<byte>.WithCharLength(length),
            };
        }

        public static UTF8String WithByteLength(int length)
        {
            return new UTF8String()
            {
                m_Base = UnicodeString<byte>.WithByteLength(length),
            };
        }

        public static UTF8String FromStr(string str)
        {
            return new UTF8String()
            {
                m_Base = UnicodeString<byte>.FromStr(str),
            };
        }

        public static UTF8String FromPtr(IntPtr ptr, bool owned)
        {
            return new UTF8String()
            {
                m_Base = UnicodeString<byte>.FromPtr(ptr, owned),
            };
        }

        public void Dispose() => m_Base.Dispose();

        public UTF8String Copy() => new UTF8String() { m_Base = m_Base.Copy() };

        public IntPtr ToPtr() => m_Base.ToPtr();
        public override string ToString() => m_Base.ToString();

        public static IntPtr AllocCharLength(int count) => UnicodeString<byte>.AllocCharLength(count);
        public static IntPtr AllocByteLength(int count) => UnicodeString<byte>.AllocByteLength(count);
        public static void Free(IntPtr ptr) => UnicodeString<byte>.Free(ptr);
        public static IntPtr Marshal(string str) => UnicodeString<byte>.Marshal(str);
        public static string Unmarshal(IntPtr ptr) => UnicodeString<byte>.Unmarshal(ptr);
        public static IntPtr Copy(IntPtr ptr) => UnicodeString<byte>.Copy(ptr);
    }

    /// <summary>
    /// An unmanaged string with UTF16 encoding.
    /// </summary>
    internal ref struct UTF16String
    {
        private UnicodeString<short> m_Base;

        public bool IsInvalid => m_Base.IsInvalid;

        public static UTF16String WithCharLength(int length)
        {
            return new UTF16String()
            {
                m_Base = UnicodeString<short>.WithCharLength(length),
            };
        }

        public static UTF16String WithByteLength(int length)
        {
            return new UTF16String()
            {
                m_Base = UnicodeString<short>.WithByteLength(length),
            };
        }

        public static UTF16String FromStr(string str)
        {
            return new UTF16String()
            {
                m_Base = UnicodeString<short>.FromStr(str),
            };
        }

        public static UTF16String FromPtr(IntPtr ptr, bool owned)
        {
            return new UTF16String()
            {
                m_Base = UnicodeString<short>.FromPtr(ptr, owned),
            };
        }

        public void Dispose() => m_Base.Dispose();

        public UTF16String Copy() => new UTF16String() { m_Base = m_Base.Copy() };

        public IntPtr ToPtr() => m_Base.ToPtr();
        public override string ToString() => m_Base.ToString();

        public static IntPtr AllocCharLength(int count) => UnicodeString<short>.AllocCharLength(count);
        public static IntPtr AllocByteLength(int count) => UnicodeString<short>.AllocByteLength(count);
        public static void Free(IntPtr ptr) => UnicodeString<short>.Free(ptr);
        public static IntPtr Marshal(string str) => UnicodeString<short>.Marshal(str);
        public static string Unmarshal(IntPtr ptr) => UnicodeString<short>.Unmarshal(ptr);
        public static IntPtr Copy(IntPtr ptr) => UnicodeString<short>.Copy(ptr);
    }

    /// <summary>
    /// An unmanaged string with UTF32 encoding.
    /// </summary>
    internal ref struct UTF32String
    {
        private UnicodeString<int> m_Base;

        public bool IsInvalid => m_Base.IsInvalid;

        public static UTF32String WithCharLength(int length)
        {
            return new UTF32String()
            {
                m_Base = UnicodeString<int>.WithCharLength(length),
            };
        }

        public static UTF32String WithByteLength(int length)
        {
            return new UTF32String()
            {
                m_Base = UnicodeString<int>.WithByteLength(length),
            };
        }

        public static UTF32String FromStr(string str)
        {
            return new UTF32String()
            {
                m_Base = UnicodeString<int>.FromStr(str),
            };
        }

        public static UTF32String FromPtr(IntPtr ptr, bool owned)
        {
            return new UTF32String()
            {
                m_Base = UnicodeString<int>.FromPtr(ptr, owned),
            };
        }

        public void Dispose() => m_Base.Dispose();

        public UTF32String Copy() => new UTF32String() { m_Base = m_Base.Copy() };

        public IntPtr ToPtr() => m_Base.ToPtr();
        public override string ToString() => m_Base.ToString();

        public static IntPtr AllocCharLength(int count) => UnicodeString<int>.AllocCharLength(count);
        public static IntPtr AllocByteLength(int count) => UnicodeString<int>.AllocByteLength(count);
        public static void Free(IntPtr ptr) => UnicodeString<int>.Free(ptr);
        public static IntPtr Marshal(string str) => UnicodeString<int>.Marshal(str);
        public static string Unmarshal(IntPtr ptr) => UnicodeString<int>.Unmarshal(ptr);
        public static IntPtr Copy(IntPtr ptr) => UnicodeString<int>.Copy(ptr);
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

        public static GnuWideString WithCharLength(int length)
        {
            return new GnuWideString()
            {
#if GNU_WCHAR_32
                m_Base = UTF32String.WithCharLength(length),
#else
                m_Base = UTF16String.WithCharLength(length),
#endif
            };
        }

        public static GnuWideString WithByteLength(int length)
        {
            return new GnuWideString()
            {
#if GNU_WCHAR_32
                m_Base = UTF32String.WithByteLength(length),
#else
                m_Base = UTF16String.WithByteLength(length),
#endif
            };
        }

        public static GnuWideString FromStr(string str)
        {
            return new GnuWideString()
            {
#if GNU_WCHAR_32
                m_Base = UTF32String.FromStr(str),
#else
                m_Base = UTF16String.FromStr(str),
#endif
            };
        }

        public static GnuWideString FromPtr(IntPtr ptr, bool owned)
        {
            return new GnuWideString()
            {
#if GNU_WCHAR_32
                m_Base = UTF32String.FromPtr(ptr, owned),
#else
                m_Base = UTF16String.FromPtr(ptr, owned),
#endif
            };
        }

        public void Dispose() => m_Base.Dispose();

        public GnuWideString Copy() => new GnuWideString() { m_Base = m_Base.Copy() };

        public IntPtr ToPtr() => m_Base.ToPtr();
        public override string ToString() => m_Base.ToString();

#if GNU_WCHAR_32
        public static IntPtr AllocCharLength(int count) => UTF32String.AllocCharLength(count);
        public static IntPtr AllocByteLength(int count) => UTF32String.AllocByteLength(count);
        public static void Free(IntPtr ptr) => UTF32String.Free(ptr);
        public static IntPtr Marshal(string str) => UTF32String.Marshal(str);
        public static string Unmarshal(IntPtr ptr) => UTF32String.Unmarshal(ptr);
        public static IntPtr Copy(IntPtr ptr) => UTF32String.Copy(ptr);
#else
        public static IntPtr AllocCharLength(int count) => UTF16String.AllocCharLength(count);
        public static IntPtr AllocByteLength(int count) => UTF16String.AllocByteLength(count);
        public static void Free(IntPtr ptr) => UTF16String.Free(ptr);
        public static IntPtr Marshal(string str) => UTF16String.Marshal(str);
        public static string Unmarshal(IntPtr ptr) => UTF16String.Unmarshal(ptr);
        public static IntPtr Copy(IntPtr ptr) => UTF16String.Copy(ptr);
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

        public static int StrLen<TChar>(IntPtr ptr)
            where TChar : unmanaged
        {
            switch (sizeof(TChar))
            {
                case 1: return StrLenByte(ptr);
                case 2: return StrLenInt16(ptr);
                case 4: return StrLenInt32(ptr);
                default: throw new ArgumentOutOfRangeException(typeof(TChar).Name);
            }
        }

        public static IntPtr AllocCharLength<TChar>(int length)
            where TChar : unmanaged
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return AllocByteLength<TChar>(length * sizeof(TChar));
        }

        public static IntPtr AllocByteLength<TChar>(int length)
            where TChar : unmanaged
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (length % sizeof(TChar) != 0)
            {
                throw new ArgumentException("Length is not aligned to charcter size.", nameof(length));
            }

            length += sizeof(TChar); // null-terminated
            var ptr = UnsafeUtility.Malloc(length, 8, Allocator.Persistent);
            if (ptr == null)
            {
                throw new OutOfMemoryException();
            }

            UnsafeUtility.MemClear(ptr, length);
            return (IntPtr)ptr;
        }

        public static void Free(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                UnsafeUtility.Free((void*)ptr, Allocator.Persistent);
            }
        }

        public static IntPtr Marshal<TChar>(Encoding encoding, string str)
            where TChar : unmanaged
        {
            if (encoding == null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }

            if (str == null)
            {
                return IntPtr.Zero;
            }

            int byteLength = encoding.GetByteCount(str) + sizeof(TChar); // null-terminated
            var ptr = UnsafeUtility.Malloc(byteLength, 8, Allocator.Persistent);
            if (ptr == null)
            {
                throw new OutOfMemoryException();
            }

            byte* bytePtr = (byte*)ptr;
            fixed (char* strPtr = str)
            {
                encoding.GetBytes(strPtr, str.Length, bytePtr, byteLength);
            }

            // Apply null terminator
            for (int i = sizeof(TChar); i > 0; i--)
            {
                bytePtr[byteLength - i] = 0;
            }

            return (IntPtr)ptr;
        }

        public static string Unmarshal<TChar>(Encoding encoding, IntPtr ptr)
            where TChar : unmanaged
        {
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            int charLength = StrLen<TChar>(ptr);
            if (charLength < 1)
            {
                return string.Empty;
            }

            int byteLength = charLength * sizeof(TChar);
            return encoding.GetString((byte*)ptr, byteLength);
        }

        public static IntPtr Copy<TChar>(IntPtr ptr)
            where TChar : unmanaged
        {
            if (ptr == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            int charLength = StrLen<TChar>(ptr) + 1; // null-terminated
            int byteLength = charLength * sizeof(TChar);

            var newPtr = UnsafeUtility.Malloc(byteLength, 8, Allocator.Persistent);
            UnsafeUtility.MemCpy(newPtr, (void*)ptr, byteLength);

            return (IntPtr)newPtr;
        }
    }
}