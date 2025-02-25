using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LibUSBWrapper {

    public static class Global
    {
        // Ensure this DLL is available in /Assets/Plugins.
        public const string LIBUSB_LIBRARY = "libusb-1.0.0";
    }

    public class LibUSB : IDisposable
    {
        // P/Invoke declarations
        [DllImport(Global.LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libusb_init(ref IntPtr handle);

        [DllImport(Global.LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libusb_get_device_list(IntPtr ctx, ref IntPtr list);

        [DllImport(Global.LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libusb_free_device_list(IntPtr list, int unref_devices);

        [DllImport(Global.LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libusb_exit(IntPtr handle);

        private IntPtr _context = IntPtr.Zero;
        private IntPtr _deviceList = IntPtr.Zero;

        public LibUSB()
        {
            int result = libusb_init(ref _context);
            if (result < 0)
            {
                throw new Exception("[LibUSB] Failed to initialize, error code: " + result);
            }

            Debug.Log("[LibUSB] initialized successfully.");
        }

        // Return a list of LibUSBDevice objects
        public List<LibUSBDevice> GetDeviceList()
        {
            var devices = new List<LibUSBDevice>();

            int result = libusb_get_device_list(_context, ref _deviceList);
            if (result < 0)
            {
                Debug.Log($"[LibUSBWrapper] failed to get device list {result}.");
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
            libusb_free_device_list(_deviceList, 1);
        }

        public void Dispose()
        {
            Debug.Log("[LibUSB] Dispose");
            libusb_exit(_context);
        }
    }

    public class LibUSBDevice : IDisposable
    {
        [DllImport(Global.LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libusb_claim_interface(IntPtr device_handle, int interface_number);

        [DllImport(Global.LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libusb_interrupt_transfer(IntPtr device_handle, uint endpoint, byte[] data, int length, out int transferred, int timeout);

        [DllImport(Global.LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libusb_release_interface(IntPtr device_handle, int interface_number);

        [DllImport(Global.LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libusb_open(IntPtr dev, ref IntPtr handle);

        [DllImport(Global.LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libusb_close(IntPtr handle);

        [DllImport(Global.LIBUSB_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libusb_get_device_descriptor(IntPtr dev, ref LibUSBDeviceDescriptor desc);

        // Define the device descriptor structure
        [StructLayout(LayoutKind.Sequential)]
        private struct LibUSBDeviceDescriptor
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

        private readonly IntPtr _devicePointer;
        private readonly LibUSBDeviceDescriptor _descriptor;
        private IntPtr _handle = IntPtr.Zero;

        public LibUSBDevice(IntPtr devicePointer)
        {
            // Populate the device descriptor.
            var result = libusb_get_device_descriptor(devicePointer, ref _descriptor);
            if (result != 0)
            {
                Debug.Log("Failed to populate device descriptor.");
            }

            _devicePointer = devicePointer;
        }

        // Get the Vendor ID
        public ushort VendorID => _descriptor.idVendor;

        // Get the Product ID
        public ushort ProductID => _descriptor.idProduct;

        // Open the device
        public void Open()
        {
            int result = libusb_open(_devicePointer, ref _handle);
            if (result != 0)
            {
                Debug.Log("Failed to open device.");
            }
        }

        public void ClaimInterface(int interfaceIndex)
        {
            int claimResult = libusb_claim_interface(_handle, interfaceIndex);
            if (claimResult < 0)
            {
                Debug.Log("Failed to claim interface, error code: " + claimResult);
            }
        }

        public void ReleaseInterface(int interfaceIndex)
        {
            int releaseResult = libusb_release_interface(_handle, interfaceIndex);
            if (releaseResult < 0)
            {
                Debug.Log("[LibUSB] Failed to release interface, error code: " + releaseResult);
            }
        }

        public void InterruptTranferOut(uint endpoint, byte[] payload)
        {
            int transferred;

            int transferResult = libusb_interrupt_transfer(_handle, endpoint, payload, payload.Length, out transferred, 1000);

            if (transferResult < 0)
            {
                Debug.Log("Failed to send payload, error code: " + transferResult);
                return;
            }

            Debug.Log("sent payload");
        }

        public void ListenForDeviceInterrupts(uint endpoint, int bufferSize, Action<byte[]> callback, CancellationToken cancellationToken)
        {
            byte[] interruptData = new byte[bufferSize];
            
            Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    int transferred;
                    int result = libusb_interrupt_transfer(_handle, endpoint + 128, interruptData, bufferSize, out transferred, 0);

                    if (result < 0)
                    {
                        Debug.Log($"Error during interrupt transfer: {result}");
                        return;
                    }

                    if (transferred > 0)
                    {
                        callback(interruptData);
                    }
                }
            }, cancellationToken);
        }

        public void Dispose() {
            Debug.Log("[LibUSBDevice] dispose");

            libusb_close(_handle);
        }
    }
}