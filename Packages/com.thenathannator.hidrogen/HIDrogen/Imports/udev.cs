using System;
using System.Runtime.InteropServices;
using HIDrogen.LowLevel;

namespace HIDrogen.Imports
{
    internal class udev : SafeHandleZeroIsInvalid
    {
        internal udev() : base() { }
        internal udev(IntPtr handle, bool ownsHandle) : base(handle, ownsHandle) { }

        protected override bool ReleaseHandle()
        {
            Udev.udev_unref(handle);
            return true;
        }
    }

    internal class udev_device : SafeHandleZeroIsInvalid
    {
        internal udev_device() : base() { }
        internal udev_device(IntPtr handle, bool ownsHandle) : base(handle, ownsHandle) { }

        protected override bool ReleaseHandle()
        {
            Udev.udev_device_unref(handle);
            return true;
        }
    }

    internal class udev_enumerate : SafeHandleZeroIsInvalid
    {
        internal udev_enumerate() : base() { }
        internal udev_enumerate(IntPtr handle, bool ownsHandle) : base(handle, ownsHandle) { }

        protected override bool ReleaseHandle()
        {
            Udev.udev_enumerate_unref(handle);
            return true;
        }
    }

    internal class udev_monitor : SafeHandleZeroIsInvalid
    {
        internal udev_monitor() : base() { }
        internal udev_monitor(IntPtr handle, bool ownsHandle) : base(handle, ownsHandle) { }

        protected override bool ReleaseHandle()
        {
            Udev.udev_monitor_unref(handle);
            return true;
        }
    }

    internal static class Udev
    {
        const string kLibName = "libudev.so";

        [DllImport(kLibName, SetLastError = true)]
	    public static extern udev udev_new();

        [DllImport(kLibName)]
	    public static extern IntPtr udev_unref(
            IntPtr udev
        );

        [DllImport(kLibName)]
        public static extern IntPtr udev_device_unref(
            IntPtr udev_device
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern udev_monitor udev_monitor_new_from_netlink(
            udev udev,
            [MarshalAs(UnmanagedType.LPStr)] string name
        );

        [DllImport(kLibName)]
        public static extern IntPtr udev_monitor_unref(
            IntPtr udev_monitor
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern int udev_monitor_filter_add_match_subsystem_devtype(
            udev_monitor udev_monitor,
            [MarshalAs(UnmanagedType.LPStr)] string subsystem,
            [MarshalAs(UnmanagedType.LPStr)] string devtype
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern int udev_monitor_enable_receiving(
            udev_monitor udev_monitor
        );

        [DllImport(kLibName, EntryPoint = "udev_monitor_get_fd", SetLastError = true)]
        private static extern int _udev_monitor_get_fd(
            udev_monitor udev_monitor
        );

        public static fd udev_monitor_get_fd(udev_monitor udev_monitor)
        {
            int fd = _udev_monitor_get_fd(udev_monitor);
            return fd < 0 ? new fd() : new fd(fd, false); // The file descriptor is *not* owned by us here
        }

        [DllImport(kLibName, SetLastError = true)]
        public static extern udev_device udev_monitor_receive_device(
            udev_monitor udev_monitor
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern udev_enumerate udev_enumerate_new(
            udev udev
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern int udev_enumerate_add_match_parent(
            udev_enumerate udev_enumerate,
            udev_device parent
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern int udev_enumerate_add_match_subsystem(
            udev_enumerate udev_enumerate,
            [MarshalAs(UnmanagedType.LPStr)] string subsystem
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern int  udev_enumerate_scan_devices(
            udev_enumerate udev_enumerate
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern IntPtr udev_enumerate_get_list_entry(
            udev_enumerate udev_enumerate
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern IntPtr udev_list_entry_get_next(
            IntPtr entry
        );

        [DllImport(kLibName, EntryPoint = "udev_list_entry_get_name", SetLastError = true)]
        private unsafe static extern byte* _udev_list_entry_get_name(
            IntPtr entry
        );

        [DllImport(kLibName, EntryPoint = "udev_list_entry_get_value", SetLastError = true)]
        private unsafe static extern byte* _udev_list_entry_get_value(
            IntPtr entry
        );

        public unsafe static string udev_list_entry_get_name(IntPtr entry) {
            return StringMarshal.FromNullTerminatedAscii(_udev_list_entry_get_name(entry));
        }

        public unsafe static string udev_list_entry_get_value(IntPtr entry) {
            return StringMarshal.FromNullTerminatedAscii(_udev_list_entry_get_value(entry));
        }

        [DllImport(kLibName, EntryPoint = "udev_device_get_syspath", SetLastError = true)]
        private unsafe static extern byte* _udev_device_get_syspath(
            udev_device device
        );

        [DllImport(kLibName, EntryPoint = "udev_device_get_sysattr_value", SetLastError = true)]
        private unsafe static extern byte* _udev_device_get_sysattr_value(
            udev_device device,
            [MarshalAs(UnmanagedType.LPStr)] string attribute
        );

        public unsafe static string udev_device_get_sysattr_value(udev_device device, string attribute) {
            return StringMarshal.FromNullTerminatedAscii(_udev_device_get_sysattr_value(device, attribute));
        }

        public unsafe static string udev_device_get_syspath(udev_device device) {
            return StringMarshal.FromNullTerminatedAscii(_udev_device_get_syspath(device));
        }

        [DllImport(kLibName, SetLastError = true)]
        public static extern IntPtr udev_device_get_sysattr_list_entry(udev_device device);

        [DllImport(kLibName, SetLastError = true)]
        public static extern IntPtr udev_enumerate_unref(
            IntPtr udev_enumerate
        );

        [DllImport(kLibName, EntryPoint = "udev_device_get_parent_with_subsystem_devtype", SetLastError = true)]
        private static extern IntPtr _udev_device_get_parent_with_subsystem_devtype(
            udev_device device,
            [MarshalAs(UnmanagedType.LPStr)] string subsystem,
            [MarshalAs(UnmanagedType.LPStr)] string devtype
        );

        public static udev_device udev_device_get_parent_with_subsystem_devtype(
            udev_device device, string subsystem, string devtype)
        {
            IntPtr devPtr = _udev_device_get_parent_with_subsystem_devtype(device, subsystem, devtype);
            return devPtr == IntPtr.Zero ? new udev_device() : new udev_device(devPtr, false); // The device is *not* owned by us here
        }

        [DllImport(kLibName, SetLastError = true)]
        public static extern udev_device udev_device_new_from_devnum(
            udev udev,
            char type,
            uint devnum
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern udev_device udev_device_new_from_syspath(
            udev udev,
            [MarshalAs(UnmanagedType.LPStr)] string path
        );
    }
}