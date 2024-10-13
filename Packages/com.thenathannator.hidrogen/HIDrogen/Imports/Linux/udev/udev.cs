#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Runtime.InteropServices;
using HIDrogen.Imports.Posix;
using HIDrogen.LowLevel;

namespace HIDrogen.Imports.Linux
{
    using udev_monitor = Udev.Monitor;
    using udev_enumerate = Udev.Enumerate;
    using udev_device = Udev.Device;

    internal class Udev : SafeHandleZeroIsInvalid
    {
        internal class Monitor : SafeHandleZeroIsInvalid
        {
            private readonly Udev m_udev;
            private readonly bool m_AddedRef;

            internal Monitor(Udev udev, IntPtr handle, bool ownsHandle)
                : base(handle, ownsHandle)
            {
                m_udev = udev;
                udev.DangerousAddRef(ref m_AddedRef);
            }

            protected override bool ReleaseHandle()
            {
                m_udev.m_udev_monitor_unref(handle);
                if (m_AddedRef)
                    m_udev.DangerousRelease();
                return true;
            }

            public int filter_add_match_subsystem_devtype(string subsystem, string devtype)
                => m_udev.m_udev_monitor_filter_add_match_subsystem_devtype(this, subsystem, devtype);

            public int enable_receiving()
                => m_udev.m_udev_monitor_enable_receiving(this);

            public udev_device receive_device()
                => m_udev.m_udev_monitor_receive_device(this);

            public fd get_fd()
            {
                int fd = m_udev.m_udev_monitor_get_fd(this);
                return fd < 0
                    ? new fd()
                    : new fd(fd, false); // The file descriptor is *not* owned by us here
            }
        }

        public class Enumerate : SafeHandleZeroIsInvalid
        {
            private readonly Udev m_udev;
            private readonly bool m_AddedRef;

            internal Enumerate(Udev udev, IntPtr handle, bool ownsHandle)
                : base(handle, ownsHandle)
            {
                m_udev = udev;
                udev.DangerousAddRef(ref m_AddedRef);
            }

            protected override bool ReleaseHandle()
            {
                m_udev.m_udev_enumerate_unref(handle);
                if (m_AddedRef)
                    m_udev.DangerousRelease();
                return true;
            }

            public int add_match_parent(udev_device parent)
                => m_udev.m_udev_enumerate_add_match_parent(this, parent);

            public int add_match_subsystem(string subsystem)
                => m_udev.m_udev_enumerate_add_match_subsystem(this, subsystem);

            public int scan_devices()
                => m_udev.m_udev_enumerate_scan_devices(this);

            public ListEntry get_list_entry()
                => new ListEntry(m_udev, m_udev.m_udev_enumerate_get_list_entry(this));
        }

        public readonly ref struct ListEntry
        {
            private readonly Udev m_udev;
            private readonly IntPtr m_Entry;

            public bool IsInvalid => m_Entry == IntPtr.Zero;

            internal ListEntry(Udev udev, IntPtr entry)
            {
                m_udev = udev;
                m_Entry = entry;
            }

            public ListEntry get_next()
                => new ListEntry(m_udev, m_udev.m_udev_list_entry_get_next(m_Entry));

            public string get_name()
                => m_udev.m_udev_list_entry_get_name(m_Entry);

            public string get_value()
                => m_udev.m_udev_list_entry_get_value(m_Entry);
        }

        public class Device : SafeHandleZeroIsInvalid
        {
            private readonly Udev m_udev;
            private readonly bool m_AddedRef;

            internal Device(Udev udev, IntPtr handle, bool ownsHandle)
                : base(handle, ownsHandle)
            {
                m_udev = udev;
                udev.DangerousAddRef(ref m_AddedRef);
            }

            protected override bool ReleaseHandle()
            {
                m_udev.m_udev_device_unref(handle);
                if (m_AddedRef)
                    m_udev.DangerousRelease();
                return true;
            }

            public string get_syspath()
                => m_udev.m_udev_device_get_syspath(this);

            public string get_sysattr_value(string attribute)
                => m_udev.m_udev_device_get_sysattr_value(this, attribute);

            public ListEntry get_sysattr_list_entry()
                => new ListEntry(m_udev, m_udev.m_udev_device_get_sysattr_list_entry(this));

            public udev_device get_parent_with_subsystem_devtype(string subsystem, string devtype)
            {
                var devPtr = m_udev.m_udev_device_get_parent_with_subsystem_devtype(this, subsystem, devtype);
                return devPtr == IntPtr.Zero
                    ? null
                    : new udev_device(m_udev, devPtr, false); // The device is *not* owned by us here
            }
        }

        const string kLibName = "libudev.so.0";

        // Because not all Linux distros seem to ship a consistently-named version of libudev
        // in their package managers (some have .so, others .so.0 and .so.1, others only .so.1),
        // we need to do this big mess of manual library and export loading so we can
        // properly account for whatever version we might find on the user's machine.
        //
        // Thanks, Linux. -w-

        #region udev
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_new();

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr _udev_unref(
            IntPtr handle
        );
        #endregion

        #region udev_monitor
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr _udev_monitor_new_from_netlink(
            Udev udev,
            [MarshalAs(UnmanagedType.LPStr)] string name
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate int _udev_monitor_filter_add_match_subsystem_devtype(
            udev_monitor udev_monitor,
            [MarshalAs(UnmanagedType.LPStr)] string subsystem,
            [MarshalAs(UnmanagedType.LPStr)] string devtype
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate int _udev_monitor_enable_receiving(
            udev_monitor udev_monitor
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate udev_device _udev_monitor_receive_device(
            udev_monitor udev_monitor
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate int _udev_monitor_get_fd(
            udev_monitor udev_monitor
        );
        #endregion

        #region udev_enumerate
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_enumerate_new(
            Udev udev
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate int _udev_enumerate_add_match_parent(
            udev_enumerate udev_enumerate,
            udev_device parent
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate int _udev_enumerate_add_match_subsystem(
            udev_enumerate udev_enumerate,
            [MarshalAs(UnmanagedType.LPStr)] string subsystem
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate int _udev_enumerate_scan_devices(
            udev_enumerate udev_enumerate
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_enumerate_get_list_entry(
            udev_enumerate udev_enumerate
        );
        #endregion

        #region udev_list_entry
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_list_entry_get_next(
            IntPtr entry
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private delegate string _udev_list_entry_get_name(
            IntPtr entry
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private delegate string _udev_list_entry_get_value(
            IntPtr entry
        );
        #endregion

        #region udev_device
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_device_new_from_devnum(
            Udev udev,
            char type,
            uint devnum
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_device_new_from_syspath(
            Udev udev,
            [MarshalAs(UnmanagedType.LPStr)] string path
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private delegate string _udev_device_get_syspath(
            udev_device device
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private delegate string _udev_device_get_sysattr_value(
            udev_device device,
            [MarshalAs(UnmanagedType.LPStr)] string attribute
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_device_get_sysattr_list_entry(
            udev_device device
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_device_get_parent_with_subsystem_devtype(
            udev_device device,
            [MarshalAs(UnmanagedType.LPStr)] string subsystem,
            [MarshalAs(UnmanagedType.LPStr)] string devtype
        );
        #endregion

        private NativeLibrary m_Library;

        private _udev_new m_udev_new;

        private _udev_unref m_udev_unref;
        private _udev_unref m_udev_device_unref;
        private _udev_unref m_udev_monitor_unref;
        private _udev_unref m_udev_enumerate_unref;

        private _udev_monitor_new_from_netlink m_udev_monitor_new_from_netlink;
        private _udev_monitor_filter_add_match_subsystem_devtype m_udev_monitor_filter_add_match_subsystem_devtype;
        private _udev_monitor_enable_receiving m_udev_monitor_enable_receiving;
        private _udev_monitor_receive_device m_udev_monitor_receive_device;
        private _udev_monitor_get_fd m_udev_monitor_get_fd;

        private _udev_enumerate_new m_udev_enumerate_new;
        private _udev_enumerate_add_match_parent m_udev_enumerate_add_match_parent;
        private _udev_enumerate_add_match_subsystem m_udev_enumerate_add_match_subsystem;
        private _udev_enumerate_scan_devices m_udev_enumerate_scan_devices;
        private _udev_enumerate_get_list_entry m_udev_enumerate_get_list_entry;

        private _udev_list_entry_get_next m_udev_list_entry_get_next;
        private _udev_list_entry_get_name m_udev_list_entry_get_name;
        private _udev_list_entry_get_value m_udev_list_entry_get_value;

        private _udev_device_new_from_devnum m_udev_device_new_from_devnum;
        private _udev_device_new_from_syspath m_udev_device_new_from_syspath;
        private _udev_device_get_syspath m_udev_device_get_syspath;
        private _udev_device_get_sysattr_value m_udev_device_get_sysattr_value;
        private _udev_device_get_sysattr_list_entry m_udev_device_get_sysattr_list_entry;
        private _udev_device_get_parent_with_subsystem_devtype m_udev_device_get_parent_with_subsystem_devtype;

        public Udev()
        {
            bool TryLoadLibrary(string name)
            {
                if (!NativeLibrary.TryLoad(name, out m_Library))
                    return false;

                // pain
                if (
                    !m_Library.TryGetExport("udev_new", out m_udev_new)

                    || !m_Library.TryGetExport("udev_unref", out m_udev_unref)
                    || !m_Library.TryGetExport("udev_device_unref", out m_udev_device_unref)
                    || !m_Library.TryGetExport("udev_monitor_unref", out m_udev_monitor_unref)
                    || !m_Library.TryGetExport("udev_enumerate_unref", out m_udev_enumerate_unref)

                    || !m_Library.TryGetExport("udev_monitor_new_from_netlink", out m_udev_monitor_new_from_netlink)
                    || !m_Library.TryGetExport("udev_monitor_filter_add_match_subsystem_devtype", out m_udev_monitor_filter_add_match_subsystem_devtype)
                    || !m_Library.TryGetExport("udev_monitor_enable_receiving", out m_udev_monitor_enable_receiving)
                    || !m_Library.TryGetExport("udev_monitor_receive_device", out m_udev_monitor_receive_device)
                    || !m_Library.TryGetExport("udev_monitor_get_fd", out m_udev_monitor_get_fd)

                    || !m_Library.TryGetExport("udev_enumerate_new", out m_udev_enumerate_new)
                    || !m_Library.TryGetExport("udev_enumerate_add_match_parent", out m_udev_enumerate_add_match_parent)
                    || !m_Library.TryGetExport("udev_enumerate_add_match_subsystem", out m_udev_enumerate_add_match_subsystem)
                    || !m_Library.TryGetExport("udev_enumerate_scan_devices", out m_udev_enumerate_scan_devices)
                    || !m_Library.TryGetExport("udev_enumerate_get_list_entry", out m_udev_enumerate_get_list_entry)

                    || !m_Library.TryGetExport("udev_list_entry_get_next", out m_udev_list_entry_get_next)
                    || !m_Library.TryGetExport("udev_list_entry_get_name", out m_udev_list_entry_get_name)
                    || !m_Library.TryGetExport("udev_list_entry_get_value", out m_udev_list_entry_get_value)

                    || !m_Library.TryGetExport("udev_device_new_from_devnum", out m_udev_device_new_from_devnum)
                    || !m_Library.TryGetExport("udev_device_new_from_syspath", out m_udev_device_new_from_syspath)
                    || !m_Library.TryGetExport("udev_device_get_syspath", out m_udev_device_get_syspath)
                    || !m_Library.TryGetExport("udev_device_get_sysattr_value", out m_udev_device_get_sysattr_value)
                    || !m_Library.TryGetExport("udev_device_get_sysattr_list_entry", out m_udev_device_get_sysattr_list_entry)
                    || !m_Library.TryGetExport("udev_device_get_parent_with_subsystem_devtype", out m_udev_device_get_parent_with_subsystem_devtype)
                )
                {
                    m_Library.Dispose();
                    Logging.Verbose($"Failed to load libudev from '{name}'");
                    return false;
                }

                return true;
            }

            if (
                !TryLoadLibrary("libudev.so.1")
                // We're fine to use libudev.so.0 here since we don't make use of any of the functions removed in v1
                // (udev_monitor_from_socket, udev_queue_get_failed_list_entry, udev_get_{dev,sys,run}_path)
                && !TryLoadLibrary("libudev.so.0")
                && !TryLoadLibrary("libudev.so")
                && !TryLoadLibrary("libudev")
            )
            {
                // i better not see this exception in any logs or i'm gonna scream
                throw new DllNotFoundException("Failed to load libudev!");
            }

            var udev = m_udev_new();
            if (udev == IntPtr.Zero)
            {
                Logging.InteropError("Failed to initialize udev");
                throw new Exception("Failed to initialize udev!");
            }

            SetHandle(udev);
        }

        protected override bool ReleaseHandle()
        {
            m_udev_unref(handle);

            m_udev_new = null;

            m_udev_unref = null;
            m_udev_device_unref = null;
            m_udev_monitor_unref = null;
            m_udev_enumerate_unref = null;

            m_udev_monitor_new_from_netlink = null;
            m_udev_monitor_filter_add_match_subsystem_devtype = null;
            m_udev_monitor_enable_receiving = null;
            m_udev_monitor_receive_device = null;
            m_udev_monitor_get_fd = null;

            m_udev_enumerate_new = null;
            m_udev_enumerate_add_match_parent = null;
            m_udev_enumerate_add_match_subsystem = null;
            m_udev_enumerate_scan_devices = null;
            m_udev_enumerate_get_list_entry = null;

            m_udev_list_entry_get_next = null;
            m_udev_list_entry_get_name = null;
            m_udev_list_entry_get_value = null;

            m_udev_device_new_from_devnum = null;
            m_udev_device_new_from_syspath = null;
            m_udev_device_get_syspath = null;
            m_udev_device_get_sysattr_value = null;
            m_udev_device_get_sysattr_list_entry = null;
            m_udev_device_get_parent_with_subsystem_devtype = null;

            m_Library?.Dispose();
            m_Library = null;
            return true;
        }

        public udev_monitor monitor_new_from_netlink(string name)
            => new udev_monitor(this, m_udev_monitor_new_from_netlink(this, name), true);

        public udev_enumerate enumerate_new()
            => new udev_enumerate(this, m_udev_enumerate_new(this), true);

        public udev_device device_new_from_devnum(char type, uint devnum)
            => new udev_device(this, m_udev_device_new_from_devnum(this, type, devnum), true);

        public udev_device device_new_from_syspath(string path)
            => new udev_device(this, m_udev_device_new_from_syspath(this, path), true);
    }
}
#endif
