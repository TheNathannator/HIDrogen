using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HIDrogen.Imports {

    // Define the device descriptor structure
    [StructLayout(LayoutKind.Sequential)]
    public struct LibUSBDeviceDescriptor
    {
        public byte bLength;
        public byte bDescriptorType;
        public short bcdUSB;
        public byte bDeviceClass;
        public byte bDeviceSubClass;
        public byte bDeviceProtocol;
        public byte bMaxPacketSize0;
        public ushort idVendor;
        public ushort idProduct;
        public ushort bcdDevice;
        public byte iManufacturer;
        public byte iProduct;
        public byte iSerialNumber;
        public byte bNumConfigurations;
    }

    public enum LibUSBError
    {
        SUCCESS = 0,
        ERROR_IO = -1,
        ERROR_INVALID_PARAM = -2,
        ERROR_ACCESS = -3,
        ERROR_NO_DEVICE = -4,
        ERROR_NOT_FOUND = -5,
        ERROR_BUSY = -6,
        ERROR_TIMEOUT = -7,
        ERROR_OVERFLOW = -8,
        ERROR_PIPE = -9,
        ERROR_INTERRUPTED = -10,
        ERROR_NO_MEM = -11,
        ERROR_NOT_SUPPORTED = -12,
        ERROR_OTHER = -13
    }

    internal class Imports {

        // Ensure this DLL is available in /Assets/Plugins.
        private const string LIBUSB_LIBRARY = "libusb-1.0.0";

        // P/Invoke declarations
        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern LibUSBError libusb_init(ref IntPtr handle);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern int libusb_get_device_list(IntPtr ctx, ref IntPtr list);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libusb_free_device_list(IntPtr list, int unref_devices);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libusb_exit(IntPtr handle);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libusb_set_auto_detach_kernel_driver(IntPtr device_handle, int enable);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern LibUSBError libusb_claim_interface(IntPtr device_handle, int interface_number);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern LibUSBError libusb_interrupt_transfer(IntPtr device_handle, uint endpoint, byte[] data, int length, out int transferred, int timeout);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern LibUSBError libusb_release_interface(IntPtr device_handle, int interface_number);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern LibUSBError libusb_open(IntPtr dev, ref IntPtr handle);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern void libusb_close(IntPtr handle);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern LibUSBError libusb_get_device_descriptor(IntPtr dev, ref LibUSBDeviceDescriptor desc);

        [DllImport(LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr libusb_strerror(LibUSBError errcode);

        public static string ErrorToString(LibUSBError errcode)
        {
            var ptr = Imports.libusb_strerror(errcode);
            return Marshal.PtrToStringAnsi(ptr);
        }
    }

    internal class LibUSB : IDisposable
    {
        private IntPtr _context = IntPtr.Zero;
        private IntPtr _deviceList = IntPtr.Zero;

        public LibUSB()
        {
            var result = Imports.libusb_init(ref _context);
            if (result < 0)
            {
                throw new Exception($"Failed to initialize LibUSB: {Imports.ErrorToString(result)} (0x{result:X8})");
            }

            Logging.Verbose("LibUSB loaded successfully.");
        }

        ~LibUSB()
        {
            Dispose();
        }

        // Return a list of LibUSBDevice objects
        public List<LibUSBDevice> GetDeviceList()
        {
            var devices = new List<LibUSBDevice>();

            int result = Imports.libusb_get_device_list(_context, ref _deviceList);
            if (result < 0)
            {
                Logging.Error($"Failed to get USB device list: {Imports.ErrorToString((LibUSBError)result)} (0x{result:X8})");
                return devices;
            }

            for (int i = 0; i < result; i++)
            {
                IntPtr device = Marshal.ReadIntPtr(_deviceList + i * 8);

                // Create the device, and add it to the list.
                devices.Add(new LibUSBDevice(device));
            }

            return devices;
        }

        public void FreeDeviceList()
        {
            Imports.libusb_free_device_list(_deviceList, 1);
        }

        public void Dispose()
        {
            if (_context != IntPtr.Zero)
            {
                Imports.libusb_exit(_context);
                _context = IntPtr.Zero;
            }
        }
    }

    internal class LibUSBDevice : IDisposable
    {
        private readonly IntPtr _devicePointer;
        private LibUSBDeviceDescriptor _descriptor;
        private IntPtr _handle = IntPtr.Zero;

        public LibUSBDevice(IntPtr devicePointer)
        {
            _devicePointer = devicePointer;
        }

        ~LibUSBDevice()
        {
            Dispose();
        }

        // The pointer can serve as a unique ID, persisting
        // between libusb_get_device_list calls.
        public int Id => (int)_devicePointer;

        public LibUSBDeviceDescriptor GetDescriptor()
        {
            var result = Imports.libusb_get_device_descriptor(_devicePointer, ref _descriptor);
            if (result != 0)
            {
                Logging.Error($"Failed to retrieve USB device descriptor: {Imports.ErrorToString(result)} (0x{result:X8})");
            }

            return _descriptor;
        }

        // Open the device
        public void Open()
        {
            var result = Imports.libusb_open(_devicePointer, ref _handle);

            if (result != LibUSBError.SUCCESS)
            {
                Logging.Error($"Failed to open USB device: {Imports.ErrorToString(result)} (0x{(int)result:X8})");
            }

            // Automatically detach/re-attach any kernel drivers that might be using this device. (linux only)
            Imports.libusb_set_auto_detach_kernel_driver(_handle, 1);
        }

        public void ClaimInterface(int interfaceIndex)
        {
            var result = Imports.libusb_claim_interface(_handle, interfaceIndex);
    
            if (result != LibUSBError.SUCCESS)
            {
                Logging.Error($"Failed to claim USB interface: {Imports.ErrorToString(result)} (0x{(int)result:X8})");
            }
        }

        public void ReleaseInterface(int interfaceIndex)
        {
            var result = Imports.libusb_release_interface(_handle, interfaceIndex);

            if (result != LibUSBError.SUCCESS)
            {
                Logging.Error($"Failed to release USB interface: {Imports.ErrorToString(result)} (0x{(int)result:X8})");
            }
        }

        public LibUSBError InterruptTranferOut(uint endpoint, byte[] payload)
        {
            int transferred;

            var result = Imports.libusb_interrupt_transfer(_handle, endpoint, payload, payload.Length, out transferred, 1000);

            if (result != LibUSBError.SUCCESS)
            {
                Logging.Error($"Failed to send USB interrupt transfer: {Imports.ErrorToString(result)} (0x{(int)result:X8})");
            }

            return result;
        }

        public LibUSBError InterruptTranferIn(uint endpoint, byte[] buffer)
        {
            int transferred;

            var result = Imports.libusb_interrupt_transfer(_handle, endpoint + 128, buffer, buffer.Length, out transferred, 0);

            if (result != LibUSBError.SUCCESS)
            {
                Logging.Error($"Error while receiving USB interrupt transfer: {Imports.ErrorToString(result)} (0x{(int)result:X8})");
            }

            return result;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                Imports.libusb_close(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}