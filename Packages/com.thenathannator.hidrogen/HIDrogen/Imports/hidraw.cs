using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HIDrogen.Imports
{
    using static Libc;
    using static HidRaw;

    [StructLayout(LayoutKind.Sequential)]
    internal struct hidraw_report_descriptor
    {
        public int size;
        public unsafe fixed byte value[HID_MAX_DESCRIPTOR_SIZE];
    }

    struct hidraw_devinfo
    {
        public uint bustype;
        public short vendor;
        public short product;
    };

    internal static class HidRaw
    {
        public const int HID_MAX_DESCRIPTOR_SIZE = 4096;

        public static readonly int HIDIOCGRDESCSIZE = IOR<int>('H', 0x01);
        public static readonly int HIDIOCGRDESC = IOR<hidraw_report_descriptor>('H', 0x02);
        public static readonly int HIDIOCGRAWINFO = IOR<hidraw_devinfo>('H', 0x03);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HIDIOCGRAWNAME(int len)
            => IOR('H', 0x04, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HIDIOCGRAWPHYS(int len)
            => IOR('H', 0x05, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HIDIOCSFEATURE(int len)
            => IOWR('H', 0x06, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HIDIOCGFEATURE(int len)
            => IOWR('H', 0x07, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HIDIOCGRAWUNIQ(int len)
            => IOR('H', 0x08, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HIDIOCGINPUT(int len)
            => IOWR('H', 0x0A, len);
    }
}