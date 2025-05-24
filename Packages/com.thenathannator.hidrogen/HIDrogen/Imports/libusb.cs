using System;
using System.Runtime.InteropServices;
using HIDrogen.LowLevel;

namespace HIDrogen.Imports
{
    using static libusb;

    internal enum libusb_error : int
    {
        SUCCESS = 0,
        IO = -1,
        INVALID_PARAM = -2,
        ACCESS = -3,
        NO_DEVICE = -4,
        NOT_FOUND = -5,
        BUSY = -6,
        TIMEOUT = -7,
        OVERFLOW = -8,
        PIPE = -9,
        INTERRUPTED = -10,
        NO_MEM = -11,
        NOT_SUPPORTED = -12,
        OTHER = -13
    }

    internal enum libusb_class_code : byte
    {
        PER_INTERFACE = 0x00,
        AUDIO = 0x01,
        COMM = 0x02,
        HID = 0x03,
        PHYSICAL = 0x05,
        IMAGE = 0x06,
        PRINTER = 0x07,
        MASS_STORAGE = 0x08,
        HUB = 0x09,
        DATA = 0x0a,
        SMART_CARD = 0x0b,
        CONTENT_SECURITY = 0x0d,
        VIDEO = 0x0e,
        PERSONAL_HEALTHCARE = 0x0f,
        DIAGNOSTIC_DEVICE = 0xdc,
        WIRELESS = 0xe0,
        MISCELLANEOUS = 0xef,
        APPLICATION = 0xfe,
        VENDOR_SPEC = 0xff
    }

    internal enum libusb_descriptor_type : byte
    {
        DEVICE = 0x01,
        CONFIG = 0x02,
        STRING = 0x03,
        INTERFACE = 0x04,
        ENDPOINT = 0x05,
        BOS = 0x0f,
        DEVICE_CAPABILITY = 0x10,
        HID = 0x21,
        REPORT = 0x22,
        PHYSICAL = 0x23,
        HUB = 0x29,
        SUPERSPEED_HUB = 0x2a,
        SS_ENDPOINT_COMPANION = 0x30
    }

    internal enum libusb_endpoint_direction : byte
    {
        OUT = 0x00,
        IN = 0x80
    }

    internal enum libusb_standard_request : byte
    {
        GET_STATUS = 0x00,
        CLEAR_FEATURE = 0x01,
        // RESERVED: 0x02,
        SET_FEATURE = 0x03,
        // RESERVED: 0x04,
        SET_ADDRESS = 0x05,
        GET_DESCRIPTOR = 0x06,
        SET_DESCRIPTOR = 0x07,
        GET_CONFIGURATION = 0x08,
        SET_CONFIGURATION = 0x09,
        GET_INTERFACE = 0x0a,
        SET_INTERFACE = 0x0b,
        SYNCH_FRAME = 0x0c,
        SET_SEL = 0x30,
        SET_ISOCH_DELAY = 0x31
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct libusb_device_descriptor
    {
        public byte bLength; // uint8_t
        public byte bDescriptorType; // uint8_t
        public ushort bcdUSB; // uint16_t
        public libusb_class_code bDeviceClass; // uint8_t
        public byte bDeviceSubClass; // uint8_t
        public byte bDeviceProtocol; // uint8_t
        public byte bMaxPacketSize0; // uint8_t
        public ushort idVendor; // uint16_t
        public ushort idProduct; // uint16_t
        public ushort bcdDevice; // uint16_t
        public byte iManufacturer; // uint8_t
        public byte iProduct; // uint8_t
        public byte iSerialNumber; // uint8_t
        public byte bNumConfigurations; // uint8_t
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct libusb_config_descriptor
    {
        public byte bLength; // uint8_t
        public byte bDescriptorType; // uint8_t
        public ushort wTotalLength; // uint16_t
        public byte bNumInterfaces; // uint8_t
        public byte bConfigurationValue; // uint8_t
        public byte iConfiguration; // uint8_t
        public byte bmAttributes; // uint8_t
        public byte MaxPower; // uint8_t
        public libusb_interface* interfaces; // const struct libusb_interface*
        public byte* extra; // const unsigned char*
        public int extra_length; // int
    };

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct libusb_interface
    {
        public libusb_interface_descriptor* altsetting; // const struct libusb_interface_descriptor*
        public int num_altsetting; // int
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct libusb_interface_descriptor
    {
        public byte bLength; // uint8_t
        public byte bDescriptorType; // uint8_t
        public byte bInterfaceNumber; // uint8_t
        public byte bAlternateSetting; // uint8_t
        public byte bNumEndpoints; // uint8_t
        public libusb_class_code bInterfaceClass; // uint8_t
        public byte bInterfaceSubClass; // uint8_t
        public byte bInterfaceProtocol; // uint8_t
        public byte iInterface; // uint8_t
        public libusb_endpoint_descriptor* endpoints; // const struct libusb_endpoint_descriptor*
        public byte* extra; // const unsigned char*
        public int extra_length; // int
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct libusb_endpoint_descriptor
    {
        public byte bLength; // uint8_t
        public byte bDescriptorType; // uint8_t
        public byte bEndpointAddress; // uint8_t
        public byte bmAttributes; // uint8_t
        public ushort wMaxPacketSize; // uint16_t
        public byte bInterval; // uint8_t
        public byte bRefresh; // uint8_t
        public byte bSynchAddress; // uint8_t
        public byte* extra; // const unsigned char*
        public int extra_length;
    }

    internal class libusb_context : SafeHandleZeroIsInvalid
    {
        private libusb_context() { } // for P/Invoke
        public libusb_context(IntPtr handle, bool ownsHandle) : base(handle, ownsHandle) { }

        protected override bool ReleaseHandle()
        {
            libusb_exit(handle);
            return true;
        }
    }

    internal class libusb_device : SafeHandleZeroIsInvalid
    {
        private libusb_device() { } // for P/Invoke
        public libusb_device(IntPtr handle, bool ownsHandle) : base(handle, ownsHandle) { }

        protected override bool ReleaseHandle()
        {
            libusb_unref_device(handle);
            return true;
        }
    }

    // Stack-based struct to avoid allocations until we determine it's a device we're interested in
    internal unsafe ref struct libusb_temp_device
    {
        private IntPtr m_Handle;
        private readonly bool m_OwnsHandle;

        public bool IsInvalid => m_Handle == IntPtr.Zero;

        public libusb_temp_device(IntPtr handle, bool ownsHandle)
        {
            m_Handle = handle;
            m_OwnsHandle = ownsHandle;
        }

        public IntPtr DangerousGetHandle() => m_Handle;

        public void Dispose()
        {
            if (m_OwnsHandle && m_Handle != IntPtr.Zero)
            {
                libusb_unref_device(m_Handle);
                m_Handle = IntPtr.Zero;
            }
        }
    }

    internal class libusb_device_handle : SafeHandleZeroIsInvalid
    {
        private libusb_device_handle() { } // for P/Invoke
        public libusb_device_handle(IntPtr handle, bool ownsHandle) : base(handle, ownsHandle) { }

        public libusb_device_handle(IntPtr handle)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            libusb_close(handle);
            return true;
        }
    }

    // Stack-based enumerator to avoid repeated list allocations
    // This *would* be a ref struct, but C# 7.3 doesn't support pattern-based disposal,
    // so it must be a normal struct
    internal unsafe struct libusb_device_list : IDisposable
    {
        private void** m_List;
        private long m_Count;
        private long m_Index;

        public IntPtr Current { get; private set; }

        public unsafe libusb_device_list(void** list, long count)
        {
            m_List = list;
            m_Count = count;
            m_Index = 0;
            Current = IntPtr.Zero;
        }

        public libusb_device_list GetEnumerator() => this;

        public bool MoveNext()
        {
            if (m_Index < m_Count)
            {
                Current = (IntPtr)m_List[m_Index];
                m_Index++;
                return true;
            }

            Current = IntPtr.Zero;
            return false;
        }

        public void Dispose()
        {
            if (m_List != null)
            {
                libusb_free_device_list(m_List, true);
                m_List = null;
                m_Count = 0;
            }
        }
    }

    internal static class libusb
    {
#if UNITY_STANDALONE_LINUX
        private const string kLibName = "libusb-1.0.so.0";
#else
        // Ensure this DLL is available in /Assets/Plugins.
        private const string kLibName = "libusb-1.0";
#endif

        private const CallingConvention kCallConvention = CallingConvention.Winapi;

        #region libusb_context

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_init")]
        private static extern libusb_error _libusb_init( // -> int
            out IntPtr ctx // libusb_context**
        );

        public static libusb_error libusb_init(out libusb_context ctx)
        {
            var result = _libusb_init(out var handle);
            ctx = result == libusb_error.SUCCESS
                ? new libusb_context(handle, ownsHandle: true)
                : null;

            return result;
        }

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static extern void libusb_exit( // -> void
            IntPtr ctx // libusb_context*
        );

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_get_device_list")]
        private static extern unsafe IntPtr _libusb_get_device_list( // -> ssize_t
            libusb_context ctx, // libusb_context*
            out void** list // libusb_device***
        );

        #endregion

        #region libusb_device

        public static unsafe libusb_error libusb_get_device_list( // -> ssize_t
            libusb_context ctx, // libusb_context*
            out libusb_device_list list // libusb_device***
        )
        {
            long count = (long)_libusb_get_device_list(ctx, out void** listPtr);
            if (count < 0)
            {
                list = default;
                return (libusb_error)count;
            }

            list = new libusb_device_list(listPtr, count);
            return libusb_error.SUCCESS;
        }

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static extern unsafe void libusb_free_device_list( // -> void
            void** list, // libusb_device**
            [MarshalAs(UnmanagedType.Bool)] bool unref_devices // int
        );

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_ref_device")]
        private static extern IntPtr _libusb_ref_device( // -> libusb_device*
            IntPtr dev // libusb_device*
        );

        public static libusb_device libusb_ref_device(in libusb_temp_device dev)
        {
            var handle = _libusb_ref_device(dev.DangerousGetHandle());
            return new libusb_device(handle, ownsHandle: true);
        }

        public static libusb_device libusb_ref_device(libusb_device dev)
        {
            var handle = _libusb_ref_device(dev.DangerousGetHandle());
            return new libusb_device(handle, ownsHandle: true);
        }

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static extern void libusb_unref_device( // -> void
            IntPtr dev // libusb_device*
        );

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_get_device_descriptor")]
        private static extern libusb_error _libusb_get_device_descriptor( // -> int
            IntPtr dev, // libusb_device*
            out libusb_device_descriptor desc // libusb_device_descriptor*
        );

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static extern libusb_error libusb_get_device_descriptor( // -> int
            libusb_device dev, // libusb_device*
            out libusb_device_descriptor desc // libusb_device_descriptor*
        );

        public static libusb_error libusb_get_device_descriptor( // -> int
            in libusb_temp_device dev, // libusb_device*
            out libusb_device_descriptor desc // libusb_device_descriptor*
        )
        {
            return _libusb_get_device_descriptor(dev.DangerousGetHandle(), out desc);
        }

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_get_active_config_descriptor")]
        private static unsafe extern libusb_error _libusb_get_active_config_descriptor( // -> int
            IntPtr dev, // libusb_device*
            out libusb_config_descriptor* config // struct libusb_config_descriptor**
        );

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static unsafe extern libusb_error libusb_get_active_config_descriptor( // -> int
            libusb_device dev, // libusb_device*
            out libusb_config_descriptor* config // struct libusb_config_descriptor**
        );

        public static unsafe libusb_error libusb_get_active_config_descriptor( // -> int
            in libusb_temp_device dev, // libusb_device*
            out libusb_config_descriptor* config // struct libusb_config_descriptor**
        )
        {
            return _libusb_get_active_config_descriptor(dev.DangerousGetHandle(), out config);
        }

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_get_config_descriptor")]
        private static unsafe extern libusb_error _libusb_get_config_descriptor( // -> int
            IntPtr dev, // libusb_device*
            byte config_index, // uint8_t
            out libusb_config_descriptor* config // struct libusb_config_descriptor**
        );

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static unsafe extern libusb_error libusb_get_config_descriptor( // -> int
            libusb_device dev, // libusb_device*
            byte config_index, // uint8_t
            out libusb_config_descriptor* config // struct libusb_config_descriptor**
        );

        public static unsafe libusb_error libusb_get_config_descriptor( // -> int
            in libusb_temp_device dev, // libusb_device*
            byte config_index, // uint8_t
            out libusb_config_descriptor* config // struct libusb_config_descriptor**
        )
        {
            return _libusb_get_config_descriptor(dev.DangerousGetHandle(), config_index, out config);
        }

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_get_config_descriptor_by_value")]
        private static unsafe extern libusb_error _libusb_get_config_descriptor_by_value( // -> int
            IntPtr dev, // libusb_device*
            byte bConfigurationValue, // uint8_t
            out libusb_config_descriptor* config // struct libusb_config_descriptor**
        );

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static unsafe extern libusb_error libusb_get_config_descriptor_by_value( // -> int
            libusb_device dev, // libusb_device*
            byte bConfigurationValue, // uint8_t
            out libusb_config_descriptor* config // struct libusb_config_descriptor**
        );

        public static unsafe libusb_error libusb_get_config_descriptor_by_value( // -> int
            in libusb_temp_device dev, // libusb_device*
            byte bConfigurationValue, // uint8_t
            out libusb_config_descriptor* config // struct libusb_config_descriptor**
        )
        {
            return _libusb_get_config_descriptor_by_value(dev.DangerousGetHandle(), bConfigurationValue, out config);
        }

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static unsafe extern void libusb_free_config_descriptor( // -> void
            libusb_config_descriptor* config // struct libusb_config_descriptor*
        );

        #endregion

        #region libusb_device_handle

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_open")]
        private static extern libusb_error _libusb_open( // -> int
            IntPtr dev, // libusb_device*
            out IntPtr dev_handle // libusb_device_handle**
        );

        private static libusb_error libusb_open( // -> int
            IntPtr dev, // libusb_device*
            out libusb_device_handle dev_handle // libusb_device_handle**
        )
        {
            var result = _libusb_open(dev, out var handle);
            dev_handle = result == libusb_error.SUCCESS
                ? new libusb_device_handle(handle, ownsHandle: true)
                : null;

            return result;
        }

        public static libusb_error libusb_open( // -> int
            libusb_device dev, // libusb_device*
            out libusb_device_handle dev_handle // libusb_device_handle**
        )
        {
            return libusb_open(dev.DangerousGetHandle(), out dev_handle);
        }

        public static libusb_error libusb_open( // -> int
            in libusb_temp_device dev, // libusb_device*
            out libusb_device_handle dev_handle // libusb_device_handle**
        )
        {
            return libusb_open(dev.DangerousGetHandle(), out dev_handle);
        }

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static extern void libusb_close( // -> void
            IntPtr dev_handle // libusb_device_handle*
        );

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_set_auto_detach_kernel_driver")]
        private static extern libusb_error _libusb_set_auto_detach_kernel_driver( // -> int
            libusb_device_handle dev_handle, // libusb_device_handle*
            [MarshalAs(UnmanagedType.Bool)] bool enable // int
        );

        public static libusb_error libusb_set_auto_detach_kernel_driver( // -> int
            libusb_device_handle dev_handle, // libusb_device_handle*
            bool enable // int
        )
        {
            var result = _libusb_set_auto_detach_kernel_driver(dev_handle, enable);

            // Ignore unsupported result for platforms which don't support driver detaching
            if (result == libusb_error.NOT_SUPPORTED)
            {
                return libusb_error.SUCCESS;
            }

            return result;
        }

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static unsafe extern libusb_error libusb_set_configuration( // -> int
            libusb_device_handle dev_handle, // libusb_device_handle*
            int configuration // int
        );

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static extern libusb_error libusb_claim_interface( // -> int
            libusb_device_handle dev_handle, // libusb_device_handle*
            int interface_number // int
        );

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static extern libusb_error libusb_release_interface( // -> int
            libusb_device_handle dev_handle, // libusb_device_handle*
            int interface_number // int
        );

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static unsafe extern libusb_error libusb_set_interface_alt_setting( // -> int
            libusb_device_handle dev_handle, // libusb_device_handle*
            int interface_number, // int
            int alternate_setting // int
        );

        [DllImport(kLibName, CallingConvention = kCallConvention)]
        public static unsafe extern libusb_error libusb_control_transfer( // -> int
            libusb_device_handle dev_handle, // libusb_device_handle*
            byte request_type, // uint8_t
            byte bRequest, // uint8_t
            ushort wValue, // uint16_t
            ushort wIndex, // uint16_t
            byte* data, // unsigned char*
            ushort wLength, // uint16_t
            uint timeout // unsigned int
        );

        public static unsafe libusb_error libusb_get_descriptor( // -> int
            libusb_device_handle dev_handle, // libusb_device_handle*
            byte desc_type, // uint8_t
            byte desc_index, // uint8_t
            byte* data, // unsigned char*
            int length, // int
            uint timeout // note: added
        )
        {
            return libusb_control_transfer(
                dev_handle,
                (byte)libusb_endpoint_direction.IN,
                (byte)libusb_standard_request.GET_DESCRIPTOR,
                (ushort)((desc_type << 8) | desc_index),
                0,
                data,
                (ushort)length,
                timeout
            );
        }

        public static unsafe libusb_error libusb_get_string_descriptor( // -> int
            libusb_device_handle dev_handle, // libusb_device_handle*
            byte desc_index, // uint8_t
            ushort langid, // uint16_t
            byte* data, // unsigned char*
            int length, // int
            uint timeout // note: added
        )
        {
            return libusb_control_transfer(
                dev_handle,
                (byte)libusb_endpoint_direction.IN,
                (byte)libusb_standard_request.GET_DESCRIPTOR,
                (ushort)(((int)libusb_descriptor_type.STRING << 8) | desc_index),
                langid,
                data,
                (ushort)length,
                timeout
            );
        }

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_interrupt_transfer")]
        public static unsafe extern libusb_error libusb_interrupt_transfer( // -> int
            libusb_device_handle dev_handle, // libusb_device_handle*
            byte endpoint, // unsigned char
            byte* data, // unsigned char*
            int length, // int
            out int actual_length, // int*
            uint timeout // unsigned int
        );

        public static unsafe libusb_error libusb_interrupt_transfer(
            libusb_device_handle dev_handle,
            byte endpoint,
            byte[] data,
            out int actual_length,
            uint timeout
        )
        {
            fixed (byte* ptr = data)
            {
                return libusb_interrupt_transfer(dev_handle, endpoint, ptr, data.Length, out actual_length, timeout);
            }
        }

        public static unsafe libusb_error libusb_interrupt_transfer(
            libusb_device_handle dev_handle,
            byte endpoint,
            byte[] data,
            int offset,
            int length,
            out int actual_length,
            uint timeout
        )
        {
            if (data.Length < offset)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if ((data.Length - offset) < length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            fixed (byte* ptr = data)
            {
                return libusb_interrupt_transfer(dev_handle, endpoint, ptr + offset, length, out actual_length, timeout);
            }
        }

        #endregion

        #region Other

        [DllImport(kLibName, CallingConvention = kCallConvention, EntryPoint = "libusb_strerror")]
        private static extern IntPtr _libusb_strerror( // -> const char*
            libusb_error errcode // int
        );

        public static string libusb_strerror(libusb_error errcode)
        {
            var ptr = _libusb_strerror(errcode);
            return Marshal.PtrToStringAnsi(ptr);
        }

        public static bool libusb_checkerror(libusb_error errcode, string message)
        {
            if (errcode != libusb_error.SUCCESS)
            {
                libusb_logerror(errcode, message);
                return false;
            }

            return true;
        }

        public static void libusb_logerror(libusb_error errcode, string message)
        {
            Logging.Error($"{message}: {libusb_strerror(errcode)} (0x{(int)errcode:X8})");
        }

        #endregion
    }
}