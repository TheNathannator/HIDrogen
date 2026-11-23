#if UNITY_STANDALONE_LINUX
using System.Runtime.InteropServices;

// Naming conventions match the original header
#pragma warning disable IDE1006

namespace HIDrogen.Imports.Linux
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct usb_descriptor_header
    {
        public byte bLength;
        public byte bDescriptorType;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct usb_interface_descriptor
    {
        public byte bLength;
        public byte bDescriptorType;

        public byte bInterfaceNumber;
        public byte bAlternateSetting;
        public byte bNumEndpoints;
        public byte bInterfaceClass;
        public byte bInterfaceSubClass;
        public byte bInterfaceProtocol;
        public byte iInterface;
    }
}
#endif