using System.Runtime.InteropServices;

namespace HIDrogen.Imports
{
    internal static partial class Libc
    {
        public const int S_IFCHR  = 0x2000;  // character device
        public const int S_IFBLK  = 0x6000;  // block device

        /// <summary>
        /// POSIX statx data structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Statx
        {
            /// <summary>Mask of bits indicating filled fields.</summary>
            public uint Mask;

            /// <summary>Block size for filesystem I/O.</summary>
            public uint BlockSize;

            /// <summary>Extra file attribute indicators</summary>
            public ulong Attributes;

            /// <summary>Number of hard links.</summary>
            public uint HardLinks;

            /// <summary>User ID of owner.</summary>
            public uint Uid;

            /// <summary>Group ID of owner.</summary>
            public uint Gid;

            /// <summary>File type and mode.</summary>
            public ushort Mode;
            private ushort Padding01;

            /// <summary>Inode number.</summary>
            public ulong Inode;

            /// <summary>Total size in bytes.</summary>
            public ulong Size;

            /// <summary>Number of 512B blocks allocated.</summary>
            public ulong Blocks;

            /// <summary>Mask to show what's supported in <see cref="Attributes"/>.</summary>
            public ulong AttributesMask;

            /// <summary>Last access time.</summary>
            public StatxTimeStamp AccessTime;

            /// <summary>Creation time.</summary>
            public StatxTimeStamp CreationTime;

            /// <summary>Last status change time.</summary>
            public StatxTimeStamp StatusChangeTime;

            /// <summary>Last modification time.</summary>
            public StatxTimeStamp LastModificationTime;

            public uint RDevIdMajor;
            public uint RDevIdMinor;
            public uint DevIdMajor;
            public uint DevIdMinor;
            public ulong MountId;

            private ulong Padding02;
            private ulong Padding03;
            private ulong Padding04;
            private ulong Padding05;
            private ulong Padding06;
            private ulong Padding07;
            private ulong Padding08;
            private ulong Padding09;
            private ulong Padding10;
            private ulong Padding11;
            private ulong Padding12;
            private ulong Padding13;
            private ulong Padding14;
            private ulong Padding15;

            public char DeviceType =>
                (Mode & S_IFCHR) == S_IFCHR ? 'c'
                : (Mode & S_IFBLK) == S_IFBLK ? 'b'
                : '\0';

            public uint DeviceNumber =>
                (RDevIdMinor & 0xff) |
                ((RDevIdMajor & 0xfff) << 8) |
                (((uint) (RDevIdMinor & ~0xff)) << 12) |
                (((uint) (RDevIdMajor & ~0xfff)) << 32);
        }

        /// <summary>
        /// Time stamp structure used by statx.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct StatxTimeStamp
        {
            /// <summary>Seconds since the Epoch (UNIX time).</summary>
            public long Seconds;

            /// <summary>Nanoseconds since <see cref="Seconds"/>.</summary>
            public uint Nanoseconds;
        }

        [DllImport(kLibName, SetLastError = true)]
        public static extern int statx(int dirfd, string path, int flags, uint mask, out Statx data);
    }
}