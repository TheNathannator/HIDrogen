#if UNITY_STANDALONE_LINUX
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HIDrogen.Imports.Linux
{
    using static Ioctl;

    [StructLayout(LayoutKind.Sequential)]
    internal struct hidraw_report_descriptor
    {
        public int size;
        public unsafe fixed byte value[HidRaw.HID_MAX_DESCRIPTOR_SIZE];
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

        public static readonly IoCode<int> HIDIOCGRDESCSIZE = IOR<int>('H', 0x01);
        public static readonly IoCode<hidraw_report_descriptor> HIDIOCGRDESC = IOR<hidraw_report_descriptor>('H', 0x02);
        public static readonly IoCode<hidraw_devinfo> HIDIOCGRAWINFO = IOR<hidraw_devinfo>('H', 0x03);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IoCode HIDIOCGRAWNAME(int len)
            => IOR('H', 0x04, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IoCode HIDIOCGRAWPHYS(int len)
            => IOR('H', 0x05, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IoCode HIDIOCSFEATURE(int len)
            => IOWR('H', 0x06, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IoCode HIDIOCGFEATURE(int len)
            => IOWR('H', 0x07, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IoCode HIDIOCGRAWUNIQ(int len)
            => IOR('H', 0x08, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IoCode HIDIOCGINPUT(int len)
            => IOWR('H', 0x0A, len);
    }
}
#endif