#if UNITY_STANDALONE_LINUX
using System;
using System.Runtime.InteropServices;
using HIDrogen.Imports.Posix;
using HIDrogen.LowLevel;

namespace HIDrogen.Imports.Linux
{
    #region Handles
    internal abstract class UdevHandle : SafeHandleZeroIsInvalid
    {
        protected readonly Udev m_udev;
        private readonly bool m_AddedRef;

        internal UdevHandle(Udev udev, IntPtr handle, bool ownsHandle)
            : base(handle, ownsHandle)
        {
            m_udev = udev;

            // Explicitly suppress Udev finalizer until our own disposal,
            // to ensure DangerousRelease is handled correctly in finalizers
            GC.SuppressFinalize(m_udev);
            m_udev.DangerousAddRef(ref m_AddedRef);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (m_AddedRef)
            {
                GC.ReRegisterForFinalize(m_udev);
                m_udev.DangerousRelease();
            }
        }

    }

    internal class udev_context : UdevHandle
    {
        internal udev_context(Udev udev, IntPtr handle, bool ownsHandle)
            : base(udev, handle, ownsHandle)
        {
        }

        protected override bool ReleaseHandle()
        {
            m_udev.context_unref(handle);
            return true;
        }

        public udev_monitor monitor_new_from_netlink(string name)
            => m_udev.monitor_new_from_netlink(this, name);

        public udev_enumerate enumerate_new()
            => m_udev.enumerate_new(this);

        public udev_device device_new_from_devnum(char type, uint devnum)
            => m_udev.device_new_from_devnum(this, type, devnum);

        public udev_device device_new_from_syspath(string path)
            => m_udev.device_new_from_syspath(this, path);
    }

    internal class udev_monitor : UdevHandle
    {
        internal udev_monitor(Udev udev, IntPtr handle, bool ownsHandle)
            : base(udev, handle, ownsHandle)
        {
        }

        protected override bool ReleaseHandle()
        {
            m_udev.monitor_unref(handle);
            return true;
        }

        public int filter_add_match_subsystem_devtype(string subsystem, string devtype)
            => m_udev.monitor_filter_add_match_subsystem_devtype(this, subsystem, devtype);

        public int enable_receiving()
            => m_udev.monitor_enable_receiving(this);

        public udev_device receive_device()
            => m_udev.monitor_receive_device(this);

        public fd get_fd()
            => m_udev.monitor_get_fd(this);
    }

    internal class udev_enumerate : UdevHandle
    {
        internal udev_enumerate(Udev udev, IntPtr handle, bool ownsHandle)
            : base(udev, handle, ownsHandle)
        {
        }

        protected override bool ReleaseHandle()
        {
            m_udev.enumerate_unref(handle);
            return true;
        }

        public int add_match_parent(udev_device parent)
            => m_udev.enumerate_add_match_parent(this, parent);

        public int add_match_subsystem(string subsystem)
            => m_udev.enumerate_add_match_subsystem(this, subsystem);

        public int scan_devices()
            => m_udev.enumerate_scan_devices(this);

        public udev_list_entry get_list_entry()
            => m_udev.enumerate_get_list_entry(this);
    }

    public readonly ref struct udev_list_entry
    {
        private readonly Udev m_udev;
        private readonly IntPtr m_Entry;

        public bool IsInvalid => m_Entry == IntPtr.Zero;

        internal udev_list_entry(Udev udev, IntPtr entry)
        {
            m_udev = udev;
            m_Entry = entry;
        }

        public udev_list_entry get_next()
            => m_udev.list_entry_get_next(m_Entry);

        public string get_name()
            => m_udev.list_entry_get_name(m_Entry);

        public string get_value()
            => m_udev.list_entry_get_value(m_Entry);
    }

    internal class udev_device : UdevHandle
    {
        internal udev_device(Udev udev, IntPtr handle, bool ownsHandle)
            : base(udev, handle, ownsHandle)
        {
        }

        protected override bool ReleaseHandle()
        {
            m_udev.device_unref(handle);
            return true;
        }

        public string get_syspath()
            => m_udev.device_get_syspath(this);

        public string get_sysattr_value(string attribute)
            => m_udev.device_get_sysattr_value(this, attribute);

        public udev_list_entry get_sysattr_list_entry()
            => m_udev.device_get_sysattr_list_entry(this);

        public udev_device get_parent_with_subsystem_devtype(string subsystem, string devtype)
            => m_udev.device_get_parent_with_subsystem_devtype(this, subsystem, devtype);
    }
    #endregion

    internal sealed class Udev : IDisposable
    {
        public static Udev Instance { get; } = new Udev();

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
            udev_context udev,
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
        private delegate IntPtr _udev_monitor_receive_device(
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
            udev_context udev
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
        private delegate IntPtr _udev_list_entry_get_name(
            IntPtr entry
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_list_entry_get_value(
            IntPtr entry
        );
        #endregion

        #region udev_device
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_device_new_from_devnum(
            udev_context udev,
            char type,
            uint devnum
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_device_new_from_syspath(
            udev_context udev,
            [MarshalAs(UnmanagedType.LPStr)] string path
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_device_get_syspath(
            udev_device device
        );

        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        private delegate IntPtr _udev_device_get_sysattr_value(
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

        private _udev_new m_udev_context_new;

        private _udev_unref m_udev_context_unref;
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
                    !m_Library.TryGetExport("udev_new", out m_udev_context_new)

                    || !m_Library.TryGetExport("udev_unref", out m_udev_context_unref)
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
        }

        public void DangerousAddRef(ref bool success)
            => m_Library.DangerousAddRef(ref success);

        public void DangerousRelease()
            => m_Library.DangerousRelease();

        public void Dispose()
        {
            m_udev_context_new = null;

            m_udev_context_unref = null;
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
        }

        public udev_context context_new()
            => new udev_context(this, m_udev_context_new(), true);

        public void context_unref(IntPtr handle)
            => m_udev_context_unref(handle);

        public void device_unref(IntPtr handle)
            => m_udev_device_unref(handle);

        public void monitor_unref(IntPtr handle)
            => m_udev_monitor_unref(handle);

        public void enumerate_unref(IntPtr handle)
            => m_udev_enumerate_unref(handle);

        #region udev_monitor
        public udev_monitor monitor_new_from_netlink(udev_context context, string name)
            => new udev_monitor(this, m_udev_monitor_new_from_netlink(context, name), true);

        public int monitor_filter_add_match_subsystem_devtype(udev_monitor monitor, string subsystem, string devtype)
            => m_udev_monitor_filter_add_match_subsystem_devtype(monitor, subsystem, devtype);

        public int monitor_enable_receiving(udev_monitor monitor)
            => m_udev_monitor_enable_receiving(monitor);

        public udev_device monitor_receive_device(udev_monitor monitor)
            => new udev_device(this, m_udev_monitor_receive_device(monitor), true);

        public fd monitor_get_fd(udev_monitor monitor)
        {
            int fd = m_udev_monitor_get_fd(monitor);
            return fd < 0
                ? new fd()
                : new fd(fd, false); // The file descriptor is *not* owned by us here
        }
        #endregion

        #region udev_enumerate
        public udev_enumerate enumerate_new(udev_context context)
            => new udev_enumerate(this, m_udev_enumerate_new(context), true);

        public int enumerate_add_match_parent(udev_enumerate enumerate, udev_device parent)
            => m_udev_enumerate_add_match_parent(enumerate, parent);

        public int enumerate_add_match_subsystem(udev_enumerate enumerate, string subsystem)
            => m_udev_enumerate_add_match_subsystem(enumerate, subsystem);

        public int enumerate_scan_devices(udev_enumerate enumerate)
            => m_udev_enumerate_scan_devices(enumerate);

        public udev_list_entry enumerate_get_list_entry(udev_enumerate enumerate)
            => new udev_list_entry(this, m_udev_enumerate_get_list_entry(enumerate));
        #endregion

        #region udev_list_entry
        public udev_list_entry list_entry_get_next(IntPtr entry)
            => new udev_list_entry(this, m_udev_list_entry_get_next(entry));

        public string list_entry_get_name(IntPtr entry)
        {
            var ptr = m_udev_list_entry_get_name(entry);
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
        }

        public string list_entry_get_value(IntPtr entry)
        {
            var ptr = m_udev_list_entry_get_value(entry);
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
        }
        #endregion

        #region udev_device
        public udev_device device_new_from_devnum(udev_context context, char type, uint devnum)
            => new udev_device(this, m_udev_device_new_from_devnum(context, type, devnum), true);

        public udev_device device_new_from_syspath(udev_context context, string path)
            => new udev_device(this, m_udev_device_new_from_syspath(context, path), true);

        public string device_get_syspath(udev_device device)
        {
            var ptr = m_udev_device_get_syspath(device);
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
        }

        public string device_get_sysattr_value(udev_device device, string attribute)
        {
            var ptr = m_udev_device_get_sysattr_value(device, attribute);
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
        }

        public udev_list_entry device_get_sysattr_list_entry(udev_device device)
            => new udev_list_entry(this, m_udev_device_get_sysattr_list_entry(device));

        public udev_device device_get_parent_with_subsystem_devtype(udev_device device, string subsystem, string devtype)
        {
            var devPtr = m_udev_device_get_parent_with_subsystem_devtype(device, subsystem, devtype);
            return devPtr == IntPtr.Zero
                ? null
                : new udev_device(this, devPtr, false); // The device is *not* owned by us here
        }
        #endregion

    }
}
#endif
