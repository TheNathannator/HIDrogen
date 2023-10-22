using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HIDrogen.LowLevel;

namespace HIDrogen.Imports
{
    internal class hid_device : SafeHandleZeroIsInvalid
    {
        private hid_device() : base() { }

        protected override bool ReleaseHandle()
        {
            HidApi.hid_close(handle);
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

        public hid_version releaseVersion => new hid_version()
        {
            major = (releaseBcd & 0xFF00) >> 8,
            minor = releaseBcd & 0xFF,
        };
    }

    internal static unsafe class HidApi
    {
        [StructLayout(LayoutKind.Sequential)]
        private unsafe readonly struct hid_device_info_internal
        {
            private readonly byte* m_Path;
            public readonly ushort vendorId;
            public readonly ushort productId;
            private readonly byte* m_SerialNumber;
            public readonly ushort releaseBcd;
            private readonly byte* m_ManufacturerName;
            private readonly byte* m_ProductName;
            public readonly ushort usagePage;
            public readonly ushort usage;
            public readonly int interfaceNumber;
            internal readonly IntPtr m_Next;

            public string path => StringMarshal.FromNullTerminatedAscii(m_Path);
            public string serialNumber => FromNullTerminatedWideStr(m_SerialNumber);
            public string manufacturerName => FromNullTerminatedWideStr(m_ManufacturerName);
            public string productName => FromNullTerminatedWideStr(m_ProductName);
        }

        const string kLibName = "libhidapi-hidraw.so.0";

        public static IEnumerable<hid_device_info> hid_enumerate()
        {
            IntPtr hEnum = IntPtr.Zero;
            try
            {
                hEnum = hid_enumerate(0, 0);
                var info = hEnum;
                while (info != IntPtr.Zero)
                {
                    yield return get(info);
                    info = getNext(info);
                }
            }
            finally
            {
                if (hEnum != IntPtr.Zero)
                    hid_free_enumeration(hEnum);
            }

            // Local functions used to work around not being able to use unsafe code in enumerators
            unsafe hid_device_info get(IntPtr hInfo)
            {
                var ptr = (hid_device_info_internal*)hInfo;
                return new hid_device_info()
                {
                    path = ptr->path,
                    vendorId = ptr->vendorId,
                    productId = ptr->productId,
                    serialNumber = ptr->serialNumber,
                    releaseBcd = ptr->releaseBcd,
                    manufacturerName = ptr->manufacturerName,
                    productName = ptr->productName,
                    usagePage = ptr->usagePage,
                    usage = ptr->usage,
                    interfaceNumber = ptr->interfaceNumber
                };
            }

            unsafe IntPtr getNext(IntPtr hInfo)
            {
                var ptr = (hid_device_info_internal*)hInfo;
                return ptr->m_Next;
            }
        }

        [DllImport(kLibName, SetLastError = true)]
        public static extern int hid_init();

        [DllImport(kLibName, SetLastError = true)]
        public static extern int hid_exit();

        [DllImport(kLibName, SetLastError = true)]
        private static extern IntPtr hid_enumerate(
            ushort vendor_id,
            ushort product_id
        );

        [DllImport(kLibName, SetLastError = true)]
        private static extern void hid_free_enumeration(
            IntPtr devs
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
        private static string FromNullTerminatedWideStr(byte* ptr)
        {
            // While this is intended for Linux only, it's best to support everything correctly
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return StringMarshal.FromNullTerminatedUtf32(ptr);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return StringMarshal.FromNullTerminatedUtf16(ptr);
#else
            throw new NotImplementedException("Unhandled platform in wide string conversion!");
#endif
        }
    }
}