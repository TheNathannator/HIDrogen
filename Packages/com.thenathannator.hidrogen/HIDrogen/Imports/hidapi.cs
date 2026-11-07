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
        public int major; // int
        public int minor; // int
        public int patch; // int
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe ref struct hid_device_info
    {
        public IntPtr pathPtr; // char*
        public ushort vendorId; // unsigned short
        public ushort productId; // unsigned short
        public IntPtr serialNumberPtr; // wchar_t*
        public ushort releaseBcd; // unsigned short
        public IntPtr manufacturerNamePtr; // wchar_t*
        public IntPtr productNamePtr; // wchar_t*
        public ushort usagePage; // unsigned short
        public ushort usage; // unsigned short
        public int interfaceNumber; // int
        public hid_device_info* next; // hid_device_info*

        public AnsiString path => new AnsiString(pathPtr, false);
        public GnuWideString manufacturerName => new GnuWideString(serialNumberPtr, false);
        public GnuWideString productName => new GnuWideString(manufacturerNamePtr, false);
        public GnuWideString serialNumber => new GnuWideString(productNamePtr, false);
    }

    internal static unsafe class hidapi
    {
#if UNITY_STANDALONE_LINUX
        const string kLibName = "libhidapi-hidraw.so.0";
#else
        const string kLibName = "hidapi";
#endif

        [DllImport(kLibName, SetLastError = true)]
        public static extern int hid_init();

        [DllImport(kLibName, SetLastError = true)]
        public static extern int hid_exit();

        [DllImport(kLibName, SetLastError = true)]
        public static extern hid_device_info* hid_enumerate(
            ushort vendor_id,
            ushort product_id
        );

        public static hid_device_info* hid_enumerate()
        {
            return hid_enumerate(0, 0);
        }

        [DllImport(kLibName, SetLastError = true)]
        public static extern void hid_free_enumeration(
            hid_device_info* devs
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
        private static extern IntPtr _hid_error(
            hid_device dev
        );

        [DllImport(kLibName, EntryPoint = "hid_error")]
        private static extern IntPtr _hid_error(
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
        public static string hid_error(hid_device dev) => GnuWideString.UnmarshalPtr(_hid_error(dev));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string hid_error() => GnuWideString.UnmarshalPtr(_hid_error(IntPtr.Zero));
    }
}