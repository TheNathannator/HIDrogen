#if UNITY_STANDALONE_LINUX
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using HIDrogen.Imports.Posix;
using HIDrogen.LowLevel;

namespace HIDrogen.Imports.Linux
{
    #region Handles
    internal abstract class UdevHandle : SafeHandleZeroIsInvalid
    {
        protected readonly Udev m_Udev;

        internal UdevHandle(Udev udev, IntPtr handle, bool ownsHandle)
            : base(handle, ownsHandle)
        {
            m_Udev = udev;
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
            m_Udev.raw_context_unref?.Invoke(handle);
            return true;
        }

        public unsafe udev_device device_new_from_syspath(string syspath)
        {
            using (var syspath_ptr = Udev.MarshalString(syspath))
            {
                var handle = m_Udev.raw_device_new_from_syspath(this, syspath_ptr);
                return new udev_device(m_Udev, handle, true);
            }
        }

        public unsafe udev_device device_new_from_syspath(Udev.OutputString syspath)
        {
            var handle = m_Udev.raw_device_new_from_syspath(this, syspath);
            return new udev_device(m_Udev, handle, true);
        }

        public udev_device device_new_from_devnum(char type, uint devnum)
        {
            var handle = m_Udev.raw_device_new_from_devnum(this, (sbyte)type, devnum);
            return new udev_device(m_Udev, handle, true);
        }

        public unsafe udev_device device_new_from_subsystem_sysname(string subsystem, string sysname)
        {
            using (var subsystem_ptr = Udev.MarshalString(subsystem))
            using (var sysname_ptr = Udev.MarshalString(sysname))
            {
                var handle = m_Udev.raw_device_new_from_subsystem_sysname(this, subsystem_ptr, sysname_ptr);
                return new udev_device(m_Udev, handle, true);
            }
        }

        // public udev_device device_new_from_device_id(string id)
        // {
        //     ...
        // }

        public udev_device device_new_from_environment()
        {
            var handle = m_Udev.raw_device_new_from_environment(this);
            return new udev_device(m_Udev, handle, true);
        }

        public unsafe udev_monitor monitor_new_from_netlink(string name)
        {
            using (var name_ptr = Udev.MarshalString(name))
            {
                var handle = m_Udev.raw_monitor_new_from_netlink(this, name_ptr);
                return new udev_monitor(m_Udev, handle, true);
            }
        }

        public udev_enumerate enumerate_new()
        {
            var handle = m_Udev.raw_enumerate_new(this);
            return new udev_enumerate(m_Udev, handle, true);
        }
    }

    internal readonly ref struct udev_list_entry
    {
        private readonly Udev m_Udev;
        private readonly IntPtr m_Entry;

        public bool IsInvalid => m_Entry == IntPtr.Zero;

        internal udev_list_entry(Udev udev, IntPtr entry)
        {
            m_Udev = udev;
            m_Entry = entry;
        }

        public udev_list_entry get_next()
        {
            var next = m_Udev.raw_list_entry_get_next(m_Entry);
            return new udev_list_entry(m_Udev, next);
        }

        public unsafe udev_list_entry get_by_name(string name)
        {
            using (var name_ptr = Udev.MarshalString(name))
            {
                var next = m_Udev.raw_list_entry_get_by_name(m_Entry, name_ptr);
                return new udev_list_entry(m_Udev, next);
            }
        }

        public unsafe Udev.OutputString get_name()
        {
            byte* ptr = m_Udev.raw_list_entry_get_name(m_Entry);
            return Udev.UnmarshalString(ptr);
        }

        public unsafe Udev.OutputString get_value()
        {
            byte* ptr = m_Udev.raw_list_entry_get_value(m_Entry);
            return Udev.UnmarshalString(ptr);
        }
    }

    internal class udev_device : UdevHandle
    {
        internal udev_device(Udev udev, IntPtr handle, bool ownsHandle)
            : base(udev, handle, ownsHandle)
        {
        }

        protected override bool ReleaseHandle()
        {
            m_Udev.raw_device_unref?.Invoke(handle);
            return true;
        }

        public udev_device get_parent()
        {
            var handle = m_Udev.raw_device_get_parent(this);
            return new udev_device(m_Udev, handle, false); // The handle is *not* owned by us here
        }

        public unsafe udev_device get_parent_with_subsystem_devtype(string subsystem, string devtype)
        {
            using (var subsystem_ptr = Udev.MarshalString(subsystem))
            using (var devtype_ptr = Udev.MarshalString(devtype))
            {
                var handle = m_Udev.raw_device_get_parent_with_subsystem_devtype(this, subsystem_ptr, devtype_ptr);
                return new udev_device(m_Udev, handle, false); // The handle is *not* owned by us here
            }
        }

        public unsafe Udev.OutputString get_devpath()
        {
            byte* ptr = m_Udev.raw_device_get_devpath(this);
            return Udev.UnmarshalString(ptr);
        }

        public unsafe Udev.OutputString get_subsystem()
        {
            byte* ptr = m_Udev.raw_device_get_subsystem(this);
            return Udev.UnmarshalString(ptr);
        }

        public unsafe Udev.OutputString get_devtype()
        {
            byte* ptr = m_Udev.raw_device_get_devtype(this);
            return Udev.UnmarshalString(ptr);
        }

        public unsafe Udev.OutputString get_syspath()
        {
            byte* ptr = m_Udev.raw_device_get_syspath(this);
            return Udev.UnmarshalString(ptr);
        }

        public unsafe Udev.OutputString get_sysname()
        {
            byte* ptr = m_Udev.raw_device_get_sysname(this);
            return Udev.UnmarshalString(ptr);
        }

        public unsafe Udev.OutputString get_sysnum()
        {
            byte* ptr = m_Udev.raw_device_get_sysnum(this);
            return Udev.UnmarshalString(ptr);
        }

        public unsafe Udev.OutputString get_devnode()
        {
            byte* ptr = m_Udev.raw_device_get_devnode(this);
            return Udev.UnmarshalString(ptr);
        }

        public bool get_is_initialized()
        {
            return m_Udev.raw_device_get_is_initialized(this) > 0;
        }

        public udev_list_entry get_devlinks_list_entry()
        {
            var entry = m_Udev.raw_device_get_devlinks_list_entry(this);
            return new udev_list_entry(m_Udev, entry);
        }

        public udev_list_entry get_properties_list_entry()
        {
            var entry = m_Udev.raw_device_get_properties_list_entry(this);
            return new udev_list_entry(m_Udev, entry);
        }

        public udev_list_entry get_tags_list_entry()
        {
            var entry = m_Udev.raw_device_get_tags_list_entry(this);
            return new udev_list_entry(m_Udev, entry);
        }

        // public udev_list_entry get_current_tags_list_entry()
        // {
        //     ...
        // }

        public udev_list_entry get_sysattr_list_entry()
        {
            var entry = m_Udev.raw_device_get_sysattr_list_entry(this);
            return new udev_list_entry(m_Udev, entry);
        }

        public unsafe Udev.OutputString get_property_value(string key)
        {
            using (var key_ptr = Udev.MarshalString(key))
            {
                byte* ptr = m_Udev.raw_device_get_property_value(this, key_ptr);
                return Udev.UnmarshalString(ptr);
            }
        }

        public unsafe Udev.OutputString get_driver()
        {
            byte* ptr = m_Udev.raw_device_get_driver(this);
            return Udev.UnmarshalString(ptr);
        }

        public ulong get_devnum()
        {
            return m_Udev.raw_device_get_devnum(this);
        }

        public unsafe Udev.OutputString get_action()
        {
            byte* ptr = m_Udev.raw_device_get_action(this);
            return Udev.UnmarshalString(ptr);
        }

        public ulong get_seqnum()
        {
            return m_Udev.raw_device_get_seqnum(this);
        }

        public ulong get_usec_since_initialized()
        {
            return m_Udev.raw_device_get_usec_since_initialized(this);
        }

        public unsafe Udev.OutputString get_sysattr_value(string sysattr)
        {
            using (var sysattr_ptr = Udev.MarshalString(sysattr))
            {
                byte* ptr = m_Udev.raw_device_get_sysattr_value(this, sysattr_ptr);
                return Udev.UnmarshalString(ptr);
            }
        }

        // public int set_sysattr_value(string sysattr, string value)
        // {
        //     ...
        // }

        public unsafe bool has_tag(string tag)
        {
            using (var tag_ptr = Udev.MarshalString(tag))
            {
                return m_Udev.raw_device_has_tag(this, tag_ptr) > 0;
            }
        }

        // public unsafe bool has_current_tag(string tag)
        // {
        //     ...
        // }
    }

    internal class udev_monitor : UdevHandle
    {
        internal udev_monitor(Udev udev, IntPtr handle, bool ownsHandle)
            : base(udev, handle, ownsHandle)
        {
        }

        protected override bool ReleaseHandle()
        {
            m_Udev.raw_monitor_unref?.Invoke(handle);
            return true;
        }

        public int enable_receiving()
        {
            return m_Udev.raw_monitor_enable_receiving(this);
        }

        public int set_receive_buffer_size(int size)
        {
            return m_Udev.raw_monitor_set_receive_buffer_size(this, size);
        }

        public fd get_fd()
        {
            int fd = m_Udev.raw_monitor_get_fd(this);
            return new fd(fd, false); // The file descriptor is *not* owned by us here
        }

        public udev_device receive_device()
        {
            var handle = m_Udev.raw_monitor_receive_device(this);
            return new udev_device(m_Udev, handle, true);
        }

        public unsafe int filter_add_match_subsystem_devtype(string subsystem, string devtype)
        {
            using (var subsystem_ptr = Udev.MarshalString(subsystem))
            using (var devtype_ptr = Udev.MarshalString(devtype))
            {
                return m_Udev.raw_monitor_filter_add_match_subsystem_devtype(this, subsystem_ptr, devtype_ptr);
            }
        }

        public unsafe int filter_add_match_tag(string tag)
        {
            using (var tag_ptr = Udev.MarshalString(tag))
            {
                return m_Udev.raw_monitor_filter_add_match_tag(this, tag_ptr);
            }
        }

        public int filter_update()
        {
            return m_Udev.raw_monitor_filter_update(this);
        }

        public int filter_remove()
        {
            return m_Udev.raw_monitor_filter_remove(this);
        }
    }

    internal class udev_enumerate : UdevHandle
    {
        internal udev_enumerate(Udev udev, IntPtr handle, bool ownsHandle)
            : base(udev, handle, ownsHandle)
        {
        }

        protected override bool ReleaseHandle()
        {
            m_Udev.raw_enumerate_unref?.Invoke(handle);
            return true;
        }

        public unsafe int add_match_subsystem(string subsystem)
        {
            using (var subsystem_ptr = Udev.MarshalString(subsystem))
            {
                return m_Udev.raw_enumerate_add_match_subsystem(this, subsystem_ptr);
            }
        }

        public unsafe int add_nomatch_subsystem(string subsystem)
        {
            using (var subsystem_ptr = Udev.MarshalString(subsystem))
            {
                return m_Udev.raw_enumerate_add_nomatch_subsystem(this, subsystem_ptr);
            }
        }

        public unsafe int add_match_sysattr(string sysattr, string value)
        {
            using (var sysattr_ptr = Udev.MarshalString(sysattr))
            using (var value_ptr = Udev.MarshalString(value))
            {
                return m_Udev.raw_enumerate_add_match_sysattr(this, sysattr_ptr, value_ptr);
            }
        }

        public unsafe int add_nomatch_sysattr(string sysattr, string value)
        {
            using (var sysattr_ptr = Udev.MarshalString(sysattr))
            using (var value_ptr = Udev.MarshalString(value))
            {
                return m_Udev.raw_enumerate_add_nomatch_sysattr(this, sysattr_ptr, value_ptr);
            }
        }

        public unsafe int add_match_property(string property, string value)
        {
            using (var property_ptr = Udev.MarshalString(property))
            using (var value_ptr = Udev.MarshalString(value))
            {
                return m_Udev.raw_enumerate_add_match_property(this, property_ptr, value_ptr);
            }
        }

        public unsafe int add_match_sysname(string sysname)
        {
            using (var sysname_ptr = Udev.MarshalString(sysname))
            {
                return m_Udev.raw_enumerate_add_match_sysname(this, sysname_ptr);
            }
        }

        public unsafe int add_match_tag(string tag)
        {
            using (var tag_ptr = Udev.MarshalString(tag))
            {
                return m_Udev.raw_enumerate_add_match_tag(this, tag_ptr);
            }
        }

        public int add_match_parent(udev_device parent)
        {
            return m_Udev.raw_enumerate_add_match_parent(this, parent);
        }

        public int add_match_is_initialized()
        {
            return m_Udev.raw_enumerate_add_match_is_initialized(this);
        }

        public unsafe int add_syspath(IntPtr syspath)
        {
            return m_Udev.raw_enumerate_add_syspath(this, (byte*)syspath);
        }

        public int scan_devices()
        {
            return m_Udev.raw_enumerate_scan_devices(this);
        }

        public int scan_subsystems()
        {
            return m_Udev.raw_enumerate_scan_subsystems(this);
        }

        public udev_list_entry get_list_entry()
        {
            var entry = m_Udev.raw_enumerate_get_list_entry(this);
            return new udev_list_entry(m_Udev, entry);
        }
    }
    #endregion

    internal sealed class Udev : IDisposable
    {
        public unsafe struct InputString : IDisposable
        {
            private byte* m_Ptr;
            private bool m_Owned;

            public InputString(string str)
            {
                if (str == null)
                {
                    m_Ptr = null;
                }
                else
                {
                    m_Ptr = (byte*)Marshal.StringToHGlobalAnsi(str);
                }

                m_Owned = true;
            }

            public InputString(byte* ptr, bool owned)
            {
                m_Ptr = ptr;
                m_Owned = owned;
            }

            public void Dispose()
            {
                if (m_Ptr != null && m_Owned)
                {
                    Marshal.FreeHGlobal((IntPtr)m_Ptr);
                }

                m_Ptr = null;
            }

            public static implicit operator byte*(InputString str) => str.m_Ptr;
        }

        public unsafe ref struct OutputString
        {
            private byte* m_Ptr;

            public bool IsNull => m_Ptr == null;

            public OutputString(byte* ptr)
            {
                m_Ptr = ptr;
            }

            public override string ToString()
            {
                if (m_Ptr == null)
                {
                    return null;
                }

                return Marshal.PtrToStringAnsi((IntPtr)m_Ptr);
            }

            public static implicit operator byte*(OutputString str) => str.m_Ptr;
            public static implicit operator InputString(OutputString str) => new InputString(str.m_Ptr, owned: false);
        }

        // Because not all Linux distros seem to ship a consistently-named version of libudev
        // in their package managers (some have .so, others .so.0 and .so.1, others only .so.1),
        // we need to do this big mess of manual library and export loading so we can
        // properly account for whatever version we might find on the user's machine.
        //
        // Thanks, Linux. -w-

        #region udev_context
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_new(); // -> udev*

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_ref( // -> udev*
            IntPtr handle // udev*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_unref( // -> udev*
            IntPtr handle // udev*
        );
        #endregion

        #region udev_list_entry
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_list_entry_get_next( // -> udev_list_entry*
            IntPtr entry // udev_list_entry*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate IntPtr udev_list_entry_get_by_name( // -> udev_list_entry*
            IntPtr entry, // udev_list_entry*
            byte* name // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_list_entry_get_name( // -> const char*
            IntPtr entry // udev_list_entry*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_list_entry_get_value( // -> const char*
            IntPtr entry // udev_list_entry*
        );
        #endregion

        #region udev_device
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_device_ref( // -> udev_device*
            IntPtr handle // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_device_unref( // -> udev_device*
            IntPtr handle // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate IntPtr udev_device_new_from_syspath( // -> udev_device*
            udev_context udev, // udev*
            byte* syspath // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_device_new_from_devnum( // -> udev_device*
            udev_context udev,  // udev*
            sbyte type, // char
            ulong devnum // dev_t
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate IntPtr udev_device_new_from_subsystem_sysname( // -> udev_device*
            udev_context udev,  // udev*
            byte* subsystem, // const char*
            byte* sysname // const char*
        );

        // Not guaranteed to be available; added in systemd 189
        // [EditorBrowsable(EditorBrowsableState.Advanced)]
        // [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        // public unsafe delegate IntPtr udev_device_new_from_device_id( // -> udev_device*
        //     udev_context udev, // udev*
        //     byte* id // const char*
        // );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_device_new_from_environment( // -> udev_device*
            udev_context udev // udev*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_device_get_parent( // -> udev_device*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate IntPtr udev_device_get_parent_with_subsystem_devtype( // -> udev_device*
            udev_device device, // udev_device*
            byte* subsystem, // const char*
            byte* devtype // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_devpath( // -> const char*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_subsystem( // -> const char*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_devtype( // -> const char*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_syspath( // -> const char*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_sysname( // -> const char*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_sysnum( // -> const char*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_devnode( // -> const char*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate int udev_device_get_is_initialized( // -> int
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_device_get_devlinks_list_entry( // -> udev_list_entry*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_device_get_properties_list_entry( // -> udev_list_entry*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_device_get_tags_list_entry( // -> udev_list_entry*
            udev_device device // udev_device*
        );

        // Not guaranteed to be available; added in systemd 247
        // [EditorBrowsable(EditorBrowsableState.Advanced)]
        // [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        // public delegate IntPtr udev_device_get_current_tags_list_entry( // -> udev_list_entry*
        //     udev_device device // udev_device*
        // );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_device_get_sysattr_list_entry( // -> udev_list_entry*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_property_value( // -> const char*
            udev_device device, // udev_device*
            byte* key // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_driver( // -> const char*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate ulong udev_device_get_devnum( // -> dev_t
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_action( // -> const char*
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate ulong udev_device_get_seqnum( // -> unsigned long long int
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate ulong udev_device_get_usec_since_initialized( // -> unsigned long long int
            udev_device device // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate byte* udev_device_get_sysattr_value( // -> const char*
            udev_device device, // udev_device*
            byte* sysattr // const char*
        );

        // Not guaranteed to be available; added in systemd 199
        // [EditorBrowsable(EditorBrowsableState.Advanced)]
        // [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        // public unsafe delegate int udev_device_set_sysattr_value( // -> int
        //     udev_device device, // udev_device*
        //     byte* sysattr, // const char*
        //     byte* value // const char*
        // );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_device_has_tag( // -> int
            udev_device device, // udev_device*
            byte* tag // const char*
        );

        // Not guaranteed to be available; added in systemd 247
        // [EditorBrowsable(EditorBrowsableState.Advanced)]
        // [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        // public unsafe delegate int udev_device_has_current_tag( // -> int
        //     udev_device device, // udev_device*
        //     byte* tag // const char*
        // );
        #endregion

        #region udev_monitor
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_monitor_ref( // -> udev_monitor*
            IntPtr handle // udev_monitor*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_monitor_unref( // -> udev_monitor*
            IntPtr handle // udev_monitor*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate IntPtr udev_monitor_new_from_netlink( // udev_monitor*
            udev_context udev, // udev*
            byte* name // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate int udev_monitor_enable_receiving( // -> int
            udev_monitor monitor // udev_monitor*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate int udev_monitor_set_receive_buffer_size( // -> int
            udev_monitor monitor, // udev_monitor*
            int size // int
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate int udev_monitor_get_fd( // -> int
            udev_monitor monitor // udev_monitor*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_monitor_receive_device( // -> udev_device*
            udev_monitor monitor // udev_monitor*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_monitor_filter_add_match_subsystem_devtype( // -> int
            udev_monitor monitor, // udev_monitor*
            byte* subsystem, // const char*
            byte* devtype // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_monitor_filter_add_match_tag( // -> int
            udev_monitor monitor, // udev_monitor*
            byte* tag // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate int udev_monitor_filter_update( // -> int
            udev_monitor monitor // udev_monitor*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate int udev_monitor_filter_remove( // -> int
            udev_monitor monitor // udev_monitor*
        );
        #endregion

        #region udev_enumerate
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_enumerate_ref( // -> udev_enumerate*
            IntPtr handle // udev_enumerate*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_enumerate_unref( // -> udev_enumerate*
            IntPtr handle // udev_enumerate*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_enumerate_new( // -> udev_enumerate*
            udev_context udev // udev*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_enumerate_add_match_subsystem( // -> int
            udev_enumerate enumerate, // udev_enumerate*
            byte* subsystem // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_enumerate_add_nomatch_subsystem( // -> int
            udev_enumerate enumerate, // udev_enumerate*
            byte* subsystem // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_enumerate_add_match_sysattr( // -> int
            udev_enumerate enumerate, // udev_enumerate*
            byte* sysattr, // const char*
            byte* value // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_enumerate_add_nomatch_sysattr( // -> int
            udev_enumerate enumerate, // udev_enumerate*
            byte* sysattr, // const char*
            byte* value // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_enumerate_add_match_property( // -> int
            udev_enumerate enumerate, // udev_enumerate*
            byte* property, // const char*
            byte* value // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_enumerate_add_match_sysname( // -> int
            udev_enumerate enumerate, // udev_enumerate*
            byte* sysname // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_enumerate_add_match_tag( // -> int
            udev_enumerate enumerate, // udev_enumerate*
            byte* tag // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate int udev_enumerate_add_match_parent( // -> int
            udev_enumerate enumerate, // udev_enumerate*
            udev_device parent // udev_device*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate int udev_enumerate_add_match_is_initialized( // -> int
            udev_enumerate enumerate // udev_enumerate*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_enumerate_add_syspath( // -> int
            udev_enumerate enumerate, // udev_enumerate*
            byte* syspath // const char*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate int udev_enumerate_scan_devices( // -> int
            udev_enumerate enumerate // udev_enumerate*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate int udev_enumerate_scan_subsystems( // -> int
            udev_enumerate enumerate // udev_enumerate*
        );

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public delegate IntPtr udev_enumerate_get_list_entry( // -> udev_list_entry*
            udev_enumerate enumerate // udev_enumerate*
        );
        #endregion

        #region udev_util
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
        public unsafe delegate int udev_util_encode_string( // int
            byte* str, // const char*
            byte* str_enc, // char*
            UIntPtr len // size_t
        );
        #endregion

        private NativeLibrary m_Library;
        private string m_LibraryPath;

        // udev_context
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_ref raw_context_ref;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_unref raw_context_unref;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_new raw_context_new;

        // udev_list_entry
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_list_entry_get_next raw_list_entry_get_next;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_list_entry_get_by_name raw_list_entry_get_by_name;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_list_entry_get_name raw_list_entry_get_name;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_list_entry_get_value raw_list_entry_get_value;

        // udev_device
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_ref raw_device_ref;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_unref raw_device_unref;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_new_from_syspath raw_device_new_from_syspath;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_new_from_devnum raw_device_new_from_devnum;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_new_from_subsystem_sysname raw_device_new_from_subsystem_sysname;
        // [EditorBrowsable(EditorBrowsableState.Advanced)]
        // public udev_device_new_from_device_id raw_device_new_from_device_id;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_new_from_environment raw_device_new_from_environment;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_parent raw_device_get_parent;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_parent_with_subsystem_devtype raw_device_get_parent_with_subsystem_devtype;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_devpath raw_device_get_devpath;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_subsystem raw_device_get_subsystem;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_devtype raw_device_get_devtype;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_syspath raw_device_get_syspath;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_sysname raw_device_get_sysname;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_sysnum raw_device_get_sysnum;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_devnode raw_device_get_devnode;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_is_initialized raw_device_get_is_initialized;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_devlinks_list_entry raw_device_get_devlinks_list_entry;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_properties_list_entry raw_device_get_properties_list_entry;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_tags_list_entry raw_device_get_tags_list_entry;
        // [EditorBrowsable(EditorBrowsableState.Advanced)]
        // public udev_device_get_current_tags_list_entry raw_device_get_current_tags_list_entry;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_sysattr_list_entry raw_device_get_sysattr_list_entry;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_property_value raw_device_get_property_value;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_driver raw_device_get_driver;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_devnum raw_device_get_devnum;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_action raw_device_get_action;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_seqnum raw_device_get_seqnum;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_usec_since_initialized raw_device_get_usec_since_initialized;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_get_sysattr_value raw_device_get_sysattr_value;
        // [EditorBrowsable(EditorBrowsableState.Advanced)]
        // public udev_device_set_sysattr_value raw_device_set_sysattr_value;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_device_has_tag raw_device_has_tag;
        // [EditorBrowsable(EditorBrowsableState.Advanced)]
        // public udev_device_has_current_tag raw_device_has_current_tag;

        // udev_monitor
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_ref raw_monitor_ref;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_unref raw_monitor_unref;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_new_from_netlink raw_monitor_new_from_netlink;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_enable_receiving raw_monitor_enable_receiving;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_set_receive_buffer_size raw_monitor_set_receive_buffer_size;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_get_fd raw_monitor_get_fd;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_receive_device raw_monitor_receive_device;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_filter_add_match_subsystem_devtype raw_monitor_filter_add_match_subsystem_devtype;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_filter_add_match_tag raw_monitor_filter_add_match_tag;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_filter_update raw_monitor_filter_update;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_monitor_filter_remove raw_monitor_filter_remove;

        // udev_enumerate
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_ref raw_enumerate_ref;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_unref raw_enumerate_unref;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_new raw_enumerate_new;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_add_match_subsystem raw_enumerate_add_match_subsystem;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_add_nomatch_subsystem raw_enumerate_add_nomatch_subsystem;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_add_match_sysattr raw_enumerate_add_match_sysattr;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_add_nomatch_sysattr raw_enumerate_add_nomatch_sysattr;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_add_match_property raw_enumerate_add_match_property;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_add_match_sysname raw_enumerate_add_match_sysname;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_add_match_tag raw_enumerate_add_match_tag;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_add_match_parent raw_enumerate_add_match_parent;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_add_match_is_initialized raw_enumerate_add_match_is_initialized;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_add_syspath raw_enumerate_add_syspath;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_scan_devices raw_enumerate_scan_devices;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_scan_subsystems raw_enumerate_scan_subsystems;
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_enumerate_get_list_entry raw_enumerate_get_list_entry;

        // udev_util
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public udev_util_encode_string raw_util_encode_string;

        public Udev()
        {
            bool TryLoadLibrary(string name)
            {
                if (!NativeLibrary.TryLoad(name, out m_Library))
                    return false;

                // pain
                if (
                    !m_Library.TryGetExport("udev_ref", out raw_context_ref) ||
                    !m_Library.TryGetExport("udev_unref", out raw_context_unref) ||
                    !m_Library.TryGetExport("udev_new", out raw_context_new) ||

                    !m_Library.TryGetExport("udev_list_entry_get_next", out raw_list_entry_get_next) ||
                    !m_Library.TryGetExport("udev_list_entry_get_by_name", out raw_list_entry_get_by_name) ||
                    !m_Library.TryGetExport("udev_list_entry_get_name", out raw_list_entry_get_name) ||
                    !m_Library.TryGetExport("udev_list_entry_get_value", out raw_list_entry_get_value) ||

                    !m_Library.TryGetExport("udev_device_ref", out raw_device_ref) ||
                    !m_Library.TryGetExport("udev_device_unref", out raw_device_unref) ||
                    !m_Library.TryGetExport("udev_device_new_from_syspath", out raw_device_new_from_syspath) ||
                    !m_Library.TryGetExport("udev_device_new_from_devnum", out raw_device_new_from_devnum) ||
                    !m_Library.TryGetExport("udev_device_new_from_subsystem_sysname", out raw_device_new_from_subsystem_sysname) ||
                    // !m_Library.TryGetExport("udev_device_new_from_device_id", out raw_device_new_from_device_id) ||
                    !m_Library.TryGetExport("udev_device_new_from_environment", out raw_device_new_from_environment) ||
                    !m_Library.TryGetExport("udev_device_get_parent", out raw_device_get_parent) ||
                    !m_Library.TryGetExport("udev_device_get_parent_with_subsystem_devtype", out raw_device_get_parent_with_subsystem_devtype) ||
                    !m_Library.TryGetExport("udev_device_get_devpath", out raw_device_get_devpath) ||
                    !m_Library.TryGetExport("udev_device_get_subsystem", out raw_device_get_subsystem) ||
                    !m_Library.TryGetExport("udev_device_get_devtype", out raw_device_get_devtype) ||
                    !m_Library.TryGetExport("udev_device_get_syspath", out raw_device_get_syspath) ||
                    !m_Library.TryGetExport("udev_device_get_sysname", out raw_device_get_sysname) ||
                    !m_Library.TryGetExport("udev_device_get_sysnum", out raw_device_get_sysnum) ||
                    !m_Library.TryGetExport("udev_device_get_devnode", out raw_device_get_devnode) ||
                    !m_Library.TryGetExport("udev_device_get_is_initialized", out raw_device_get_is_initialized) ||
                    !m_Library.TryGetExport("udev_device_get_devlinks_list_entry", out raw_device_get_devlinks_list_entry) ||
                    !m_Library.TryGetExport("udev_device_get_properties_list_entry", out raw_device_get_properties_list_entry) ||
                    !m_Library.TryGetExport("udev_device_get_tags_list_entry", out raw_device_get_tags_list_entry) ||
                    // !m_Library.TryGetExport("udev_device_get_current_tags_list_entry", out raw_device_get_current_tags_list_entry) ||
                    !m_Library.TryGetExport("udev_device_get_sysattr_list_entry", out raw_device_get_sysattr_list_entry) ||
                    !m_Library.TryGetExport("udev_device_get_property_value", out raw_device_get_property_value) ||
                    !m_Library.TryGetExport("udev_device_get_driver", out raw_device_get_driver) ||
                    !m_Library.TryGetExport("udev_device_get_devnum", out raw_device_get_devnum) ||
                    !m_Library.TryGetExport("udev_device_get_action", out raw_device_get_action) ||
                    !m_Library.TryGetExport("udev_device_get_seqnum", out raw_device_get_seqnum) ||
                    !m_Library.TryGetExport("udev_device_get_usec_since_initialized", out raw_device_get_usec_since_initialized) ||
                    !m_Library.TryGetExport("udev_device_get_sysattr_value", out raw_device_get_sysattr_value) ||
                    // !m_Library.TryGetExport("udev_device_set_sysattr_value", out raw_device_set_sysattr_value) ||
                    !m_Library.TryGetExport("udev_device_has_tag", out raw_device_has_tag) ||
                    // !m_Library.TryGetExport("udev_device_has_current_tag", out raw_device_has_current_tag) ||

                    !m_Library.TryGetExport("udev_monitor_ref", out raw_monitor_ref) ||
                    !m_Library.TryGetExport("udev_monitor_unref", out raw_monitor_unref) ||
                    !m_Library.TryGetExport("udev_monitor_new_from_netlink", out raw_monitor_new_from_netlink) ||
                    !m_Library.TryGetExport("udev_monitor_enable_receiving", out raw_monitor_enable_receiving) ||
                    !m_Library.TryGetExport("udev_monitor_set_receive_buffer_size", out raw_monitor_set_receive_buffer_size) ||
                    !m_Library.TryGetExport("udev_monitor_get_fd", out raw_monitor_get_fd) ||
                    !m_Library.TryGetExport("udev_monitor_receive_device", out raw_monitor_receive_device) ||
                    !m_Library.TryGetExport("udev_monitor_filter_add_match_subsystem_devtype", out raw_monitor_filter_add_match_subsystem_devtype) ||
                    !m_Library.TryGetExport("udev_monitor_filter_add_match_tag", out raw_monitor_filter_add_match_tag) ||
                    !m_Library.TryGetExport("udev_monitor_filter_update", out raw_monitor_filter_update) ||
                    !m_Library.TryGetExport("udev_monitor_filter_remove", out raw_monitor_filter_remove) ||

                    !m_Library.TryGetExport("udev_enumerate_ref", out raw_enumerate_ref) ||
                    !m_Library.TryGetExport("udev_enumerate_unref", out raw_enumerate_unref) ||
                    !m_Library.TryGetExport("udev_enumerate_new", out raw_enumerate_new) ||
                    !m_Library.TryGetExport("udev_enumerate_add_match_subsystem", out raw_enumerate_add_match_subsystem) ||
                    !m_Library.TryGetExport("udev_enumerate_add_nomatch_subsystem", out raw_enumerate_add_nomatch_subsystem) ||
                    !m_Library.TryGetExport("udev_enumerate_add_match_sysattr", out raw_enumerate_add_match_sysattr) ||
                    !m_Library.TryGetExport("udev_enumerate_add_nomatch_sysattr", out raw_enumerate_add_nomatch_sysattr) ||
                    !m_Library.TryGetExport("udev_enumerate_add_match_property", out raw_enumerate_add_match_property) ||
                    !m_Library.TryGetExport("udev_enumerate_add_match_sysname", out raw_enumerate_add_match_sysname) ||
                    !m_Library.TryGetExport("udev_enumerate_add_match_tag", out raw_enumerate_add_match_tag) ||
                    !m_Library.TryGetExport("udev_enumerate_add_match_parent", out raw_enumerate_add_match_parent) ||
                    !m_Library.TryGetExport("udev_enumerate_add_match_is_initialized", out raw_enumerate_add_match_is_initialized) ||
                    !m_Library.TryGetExport("udev_enumerate_add_syspath", out raw_enumerate_add_syspath) ||
                    !m_Library.TryGetExport("udev_enumerate_scan_devices", out raw_enumerate_scan_devices) ||
                    !m_Library.TryGetExport("udev_enumerate_scan_subsystems", out raw_enumerate_scan_subsystems) ||
                    !m_Library.TryGetExport("udev_enumerate_get_list_entry", out raw_enumerate_get_list_entry) ||

                    !m_Library.TryGetExport("udev_util_encode_string", out raw_util_encode_string)
                )
                {
                    m_Library.Dispose();
                    Logging.Verbose($"Failed to load libudev from '{name}'");
                    return false;
                }

                m_LibraryPath = name;
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

        public void Dispose()
        {
            raw_context_ref = null;
            raw_context_unref = null;
            raw_context_new = null;

            raw_list_entry_get_next = null;
            raw_list_entry_get_by_name = null;
            raw_list_entry_get_name = null;
            raw_list_entry_get_value = null;

            raw_device_ref = null;
            raw_device_unref = null;
            raw_device_new_from_syspath = null;
            raw_device_new_from_devnum = null;
            raw_device_new_from_subsystem_sysname = null;
            // raw_device_new_from_device_id = null;
            raw_device_new_from_environment = null;
            raw_device_get_parent = null;
            raw_device_get_parent_with_subsystem_devtype = null;
            raw_device_get_devpath = null;
            raw_device_get_subsystem = null;
            raw_device_get_devtype = null;
            raw_device_get_syspath = null;
            raw_device_get_sysname = null;
            raw_device_get_sysnum = null;
            raw_device_get_devnode = null;
            raw_device_get_is_initialized = null;
            raw_device_get_devlinks_list_entry = null;
            raw_device_get_properties_list_entry = null;
            raw_device_get_tags_list_entry = null;
            // raw_device_get_current_tags_list_entry = null;
            raw_device_get_sysattr_list_entry = null;
            raw_device_get_property_value = null;
            raw_device_get_driver = null;
            raw_device_get_devnum = null;
            raw_device_get_action = null;
            raw_device_get_seqnum = null;
            raw_device_get_usec_since_initialized = null;
            raw_device_get_sysattr_value = null;
            // raw_device_set_sysattr_value = null;
            raw_device_has_tag = null;
            // raw_device_has_current_tag = null;

            raw_monitor_ref = null;
            raw_monitor_unref = null;
            raw_monitor_new_from_netlink = null;
            raw_monitor_enable_receiving = null;
            raw_monitor_set_receive_buffer_size = null;
            raw_monitor_get_fd = null;
            raw_monitor_receive_device = null;
            raw_monitor_filter_add_match_subsystem_devtype = null;
            raw_monitor_filter_add_match_tag = null;
            raw_monitor_filter_update = null;
            raw_monitor_filter_remove = null;

            raw_enumerate_ref = null;
            raw_enumerate_unref = null;
            raw_enumerate_new = null;
            raw_enumerate_add_match_subsystem = null;
            raw_enumerate_add_nomatch_subsystem = null;
            raw_enumerate_add_match_sysattr = null;
            raw_enumerate_add_nomatch_sysattr = null;
            raw_enumerate_add_match_property = null;
            raw_enumerate_add_match_sysname = null;
            raw_enumerate_add_match_tag = null;
            raw_enumerate_add_match_parent = null;
            raw_enumerate_add_match_is_initialized = null;
            raw_enumerate_add_syspath = null;
            raw_enumerate_scan_devices = null;
            raw_enumerate_scan_subsystems = null;
            raw_enumerate_get_list_entry = null;

            raw_util_encode_string = null;

            m_Library?.Dispose();
            m_Library = null;
        }

        public udev_context context_new()
        {
            var handle = raw_context_new();
            return new udev_context(this, handle, true);
        }

        // convenience methods
        public static InputString MarshalString(string str)
        {
            return new InputString(str);
        }

        public static unsafe OutputString UnmarshalString(byte* ptr)
        {
            return new OutputString(ptr);
        }
    }
}
#endif
