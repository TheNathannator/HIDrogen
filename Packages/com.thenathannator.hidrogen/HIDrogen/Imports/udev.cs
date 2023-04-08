using System;
using System.Runtime.InteropServices;
using HIDrogen.LowLevel;

namespace HIDrogen.Imports
{
    internal class udev : SafeHandleZeroIsInvalid
    {
        private udev() : base() { }

        protected override bool ReleaseHandle()
        {
            Udev.udev_unref(handle);
            return true;
        }
    }

    internal class udev_device : SafeHandleZeroIsInvalid
    {
        private udev_device() : base() { }

        protected override bool ReleaseHandle()
        {
            Udev.udev_device_unref(handle);
            return true;
        }
    }

    internal class udev_monitor : SafeHandleZeroIsInvalid
    {
        private udev_monitor() : base() { }

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
    }
}