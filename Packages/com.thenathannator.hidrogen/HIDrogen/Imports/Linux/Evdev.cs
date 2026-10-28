#if UNITY_STANDALONE_LINUX
using System;
using System.Runtime.InteropServices;
using HIDrogen.Imports.Posix;

namespace HIDrogen.Imports.Linux
{
    internal static class Evdev
    {
        public const int EV_SYN = 0;
        public const int EV_KEY = 1;
        public const int EV_ABS = 3;
        public const int SYN_REPORT = 0;
        public const int SYN_DROPPED = 3;

        [StructLayout(LayoutKind.Sequential)]
        internal struct InputEvent
        {
            public IntPtr seconds;
            public IntPtr microseconds;
            public ushort type;
            public ushort code;
            public int value;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct InputId
        {
            public ushort bus;
            public ushort vendor;
            public ushort product;
            public ushort version;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AbsInfo
        {
            public int value;
            public int minimum;
            public int maximum;
            public int fuzz;
            public int flat;
            public int resolution;
        }

        public static int GetBits(fd handle, int type, byte[] bits)
            => Ioctl.ioctl(handle, Ioctl.IOR('E', 0x20 + type, bits.Length), bits);

        public static int GetKeys(fd handle, byte[] bits)
            => Ioctl.ioctl(handle, Ioctl.IOR('E', 0x18, bits.Length), bits);

        public static int GetId(fd handle, ref InputId id)
            => Ioctl.ioctl(handle, Ioctl.IOR<InputId>('E', 0x02), ref id);

        public static int GetAbs(fd handle, int code, ref AbsInfo info)
            => Ioctl.ioctl(handle, Ioctl.IOR<AbsInfo>('E', 0x40 + code), ref info);

        public static bool HasBit(byte[] bits, int code)
            => code >= 0 && code / 8 < bits.Length && (bits[code / 8] & (1 << (code & 7))) != 0;
    }
}
#endif
