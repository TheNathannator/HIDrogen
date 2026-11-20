#if UNITY_STANDALONE_LINUX
using System.Runtime.InteropServices;

// Naming conventions match the original header
#pragma warning disable IDE1006

namespace HIDrogen.Imports.Linux
{
    // Descriptor layouts, from linux/usb/ch9.h.
    // These match the byte stream found in a USB device's sysfs `descriptors` file,

    internal static class UsbDescriptorType
    {
        public const byte USB_DT_DEVICE = 0x01;
        public const byte USB_DT_CONFIG = 0x02;
        public const byte USB_DT_STRING = 0x03;
        public const byte USB_DT_INTERFACE = 0x04;
        public const byte USB_DT_ENDPOINT = 0x05;
    }

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
