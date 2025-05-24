using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HIDrogen.LowLevel;

// Naming conventions match the original header
#pragma warning disable IDE1006

namespace HIDrogen.Imports
{
    using static hidapi;

    internal class hid_device : SafeHandleZeroIsInvalid
    {
        private hid_device() : base() { }

        protected override bool ReleaseHandle()
        {
            hid_close(handle);
            return true;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct hid_version
    {
        public int major;
        public int minor;
        public int patch;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe readonly struct hid_device_info_internal
    {
        public readonly byte* path;
        public readonly ushort vendorId;
        public readonly ushort productId;
        public readonly byte* serialNumber;
        public readonly ushort releaseBcd;
        public readonly byte* manufacturerName;
        public readonly byte* productName;
        public readonly ushort usagePage;
        public readonly ushort usage;
        public readonly int interfaceNumber;
        public readonly hid_device_info_internal* next;
    }

    internal struct hid_device_info
    {
        public string path;
        public ushort vendorId;
        public ushort productId;
        public ushort releaseBcd;
        public string manufacturerName;
        public string productName;
        public string serialNumber;
        public ushort usagePage;
        public ushort usage;
        public int interfaceNumber;
    }

    // Stack-based enumerator to avoid repeated list allocations
    // This *would* be a ref struct, but C# 7.3 doesn't support pattern-based disposal,
    // so it must be a normal struct
    internal unsafe struct hid_device_enumerator : IDisposable
    {
        private hid_device_info_internal* m_Handle;
        private hid_device_info_internal* m_CurrentPtr;

        public hid_device_info Current { get; private set; }

        public hid_device_enumerator(hid_device_info_internal* handle)
        {
            m_Handle = handle;
            m_CurrentPtr = handle;
            Current = default;
        }

        public hid_device_enumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            if (m_CurrentPtr != null)
            {
                Current = new hid_device_info()
                {
                    path = StringMarshal.FromNullTerminatedAscii(m_CurrentPtr->path),
                    vendorId = m_CurrentPtr->vendorId,
                    productId = m_CurrentPtr->productId,
                    serialNumber = FromNullTerminatedWideStr(m_CurrentPtr->serialNumber),
                    releaseBcd = m_CurrentPtr->releaseBcd,
                    manufacturerName = FromNullTerminatedWideStr(m_CurrentPtr->manufacturerName),
                    productName = FromNullTerminatedWideStr(m_CurrentPtr->productName),
                    usagePage = m_CurrentPtr->usagePage,
                    usage = m_CurrentPtr->usage,
                    interfaceNumber = m_CurrentPtr->interfaceNumber
                };
                m_CurrentPtr = m_CurrentPtr->next;
                return true;
            }

            Current = default;
            return false;
        }

        public void Dispose()
        {
            if (m_Handle != null)
            {
                hid_free_enumeration(m_Handle);
                m_Handle = null;
            }
        }
    }

    internal static unsafe class hidapi
    {
#if UNITY_STANDALONE_LINUX
        const string kLibName = "libhidapi-hidraw.so.0";
#else
        const string kLibName = "hidapi";
#endif

        // TODO: make custom enumerator
        public static hid_device_enumerator hid_enumerate()
        {
            var handle = hid_enumerate(0, 0);
            return new hid_device_enumerator(handle);
        }

        [DllImport(kLibName, SetLastError = true)]
        public static extern int hid_init();

        [DllImport(kLibName, SetLastError = true)]
        public static extern int hid_exit();

        [DllImport(kLibName, SetLastError = true)]
        private static extern hid_device_info_internal* hid_enumerate(
            ushort vendor_id,
            ushort product_id
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern void hid_free_enumeration(
            hid_device_info_internal* devs
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern hid_device hid_open_path(
            [MarshalAs(UnmanagedType.LPStr)] string path
        );

        [DllImport(kLibName)]
        public static extern void hid_close(
            IntPtr dev
        );

        [DllImport(kLibName, EntryPoint = "hid_read_timeout", SetLastError = true)]
        private static extern int _hid_read_timeout(
            hid_device dev,
            byte[] buffer,
            UIntPtr length,
            int milliseconds
        );

        [DllImport(kLibName, EntryPoint = "hid_read_timeout", SetLastError = true)]
        private static extern int _hid_read_timeout(
            hid_device dev,
            void* buffer,
            UIntPtr length,
            int milliseconds
        );

        [DllImport(kLibName, EntryPoint = "hid_write", SetLastError = true)]
        private static extern int _hid_write(
            hid_device dev,
            byte[] buffer,
            UIntPtr length
        );

        [DllImport(kLibName, EntryPoint = "hid_write", SetLastError = true)]
        private static extern int _hid_write(
            hid_device dev,
            void* buffer,
            UIntPtr length
        );

        [DllImport(kLibName, EntryPoint = "hid_get_feature_report", SetLastError = true)]
        private static extern int _hid_get_feature_report(
            hid_device dev,
            byte[] buffer,
            UIntPtr length
        );

        [DllImport(kLibName, EntryPoint = "hid_get_feature_report", SetLastError = true)]
        private static extern int _hid_get_feature_report(
            hid_device dev,
            void* buffer,
            UIntPtr length
        );

        [DllImport(kLibName, EntryPoint = "hid_send_feature_report", SetLastError = true)]
        private static extern int _hid_send_feature_report(
            hid_device dev,
            byte[] buffer,
            UIntPtr length
        );

        [DllImport(kLibName, EntryPoint = "hid_send_feature_report", SetLastError = true)]
        private static extern int _hid_send_feature_report(
            hid_device dev,
            void* buffer,
            UIntPtr length
        );

        [DllImport(kLibName, EntryPoint = "hid_error")]
        private static extern byte* _hid_error(
            hid_device dev
        );

        [DllImport(kLibName, EntryPoint = "hid_error")]
        private static extern byte* _hid_error(
            IntPtr dev
        );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int hid_read_timeout(hid_device dev, byte[] buffer, int milliseconds)
            => _hid_read_timeout(dev, buffer, (UIntPtr)buffer.Length, milliseconds);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int hid_read_timeout(hid_device dev, void* buffer, int length, int milliseconds)
            => _hid_read_timeout(dev, buffer, (UIntPtr)length, milliseconds);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int hid_write(hid_device dev, byte[] buffer)
            => _hid_write(dev, buffer, (UIntPtr)buffer.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int hid_write(hid_device dev, void* buffer, int length)
            => _hid_write(dev, buffer, (UIntPtr)length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int hid_get_feature_report(hid_device dev, byte[] buffer)
            => _hid_get_feature_report(dev, buffer, (UIntPtr)buffer.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int hid_get_feature_report(hid_device dev, void* buffer, int length)
            => _hid_get_feature_report(dev, buffer, (UIntPtr)length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int hid_send_feature_report(hid_device dev, byte[] buffer)
            => _hid_send_feature_report(dev, buffer, (UIntPtr)buffer.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int hid_send_feature_report(hid_device dev, void* buffer, int length)
            => _hid_send_feature_report(dev, buffer, (UIntPtr)length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string hid_error(hid_device dev) => FromNullTerminatedWideStr(_hid_error(dev));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string hid_error() => FromNullTerminatedWideStr(_hid_error(IntPtr.Zero));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FromNullTerminatedWideStr(byte* ptr)
        {
            // While this is intended for Linux only, it's best to support everything correctly
#if UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
            return StringMarshal.FromNullTerminatedUtf32(ptr);
#elif UNITY_STANDALONE_WIN
            return StringMarshal.FromNullTerminatedUtf16(ptr);
#else
            throw new NotImplementedException("Unhandled platform in wide string conversion!");
#endif
        }
    }
}