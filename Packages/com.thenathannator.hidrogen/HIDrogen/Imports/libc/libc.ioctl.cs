using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HIDrogen.Imports
{
    internal static partial class Libc
    {
        public const int IOC_NRBITS = 8;
        public const int IOC_TYPEBITS = 8;
        public const int IOC_SIZEBITS = 14;
        public const int IOC_DIRBITS = 2;

        public const int IOC_NRMASK = (1 << IOC_NRBITS) - 1;
        public const int IOC_TYPEMASK = (1 << IOC_TYPEBITS) - 1;
        public const int IOC_SIZEMASK = (1 << IOC_SIZEBITS) - 1;
        public const int IOC_DIRMASK = (1 << IOC_DIRBITS) - 1;

        public const int IOC_NRSHIFT = 0;
        public const int IOC_TYPESHIFT = IOC_NRSHIFT + IOC_NRBITS;
        public const int IOC_SIZESHIFT = IOC_TYPESHIFT + IOC_TYPEBITS;
        public const int IOC_DIRSHIFT = IOC_SIZESHIFT + IOC_SIZEBITS;

        public const int IOC_NONE = 0;
        public const int IOC_WRITE = 1;
        public const int IOC_READ = 2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOC(int dir, int type, int nr, int size)
            => ((dir & IOC_DIRMASK) << IOC_DIRSHIFT) |
               ((type & IOC_TYPEMASK) << IOC_TYPESHIFT) |
               ((nr & IOC_NRMASK) << IOC_NRSHIFT) |
               ((size & IOC_SIZEMASK) << IOC_SIZESHIFT);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int IOC<T>(int dir, int type, int nr)
            where T : unmanaged
            => IOC(dir, type, nr, sizeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IO(int type, int nr)
            => IOC(IOC_NONE, type, nr, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOR(int type, int nr, int size)
            => IOC(IOC_READ, type, nr, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOR<T>(int type, int nr)
            where T : unmanaged
            => IOC<T>(IOC_READ, type, nr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOW(int type, int nr, int size)
            => IOC(IOC_WRITE, type, nr, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOW<T>(int type, int nr)
            where T : unmanaged
            => IOC<T>(IOC_WRITE, type, nr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOWR(int type, int nr, int size)
            => IOC(IOC_READ | IOC_WRITE, type, nr, size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOWR<T>(int type, int nr)
            where T : unmanaged
            => IOC<T>(IOC_READ | IOC_WRITE, type, nr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOC_DIR(int ioc)
            => (ioc >> IOC_DIRSHIFT) & IOC_DIRMASK;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOC_TYPE(int ioc)
            => (ioc >> IOC_TYPESHIFT) & IOC_TYPEMASK;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOC_NR(int ioc)
            => (ioc >> IOC_NRSHIFT) & IOC_NRMASK;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IOC_SIZE(int ioc)
            => (ioc >> IOC_SIZESHIFT) & IOC_SIZEMASK;

        public const int IOC_IN = IOC_WRITE << IOC_DIRSHIFT;
        public const int IOC_OUT = IOC_READ << IOC_DIRSHIFT;
        public const int IOC_INOUT = (IOC_WRITE | IOC_READ) << IOC_DIRSHIFT;
        public const int IOCSIZE_MASK = IOC_SIZEMASK << IOC_SIZESHIFT;
        public const int IOCSIZE_SHIFT = IOC_SIZESHIFT;

        [DllImport(kLibName, EntryPoint = "ioctl", SetLastError = true)]
        private static extern int _ioctl(
            int fd,
            UIntPtr request
        );

        [DllImport(kLibName, EntryPoint = "ioctl", SetLastError = true)]
        private static extern unsafe int _ioctl(
            int fd,
            UIntPtr request,
            void* arg
        );

        [DllImport(kLibName, EntryPoint = "ioctl", SetLastError = true)]
        private static extern int _ioctl(
            int fd,
            UIntPtr request,
            byte[] arg
        );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ioctl(fd fd, int request)
            => _ioctl(fd.DangerousGetFileDescriptor(), (UIntPtr)request);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int ioctl(fd fd, int request, void* arg)
            => _ioctl(fd.DangerousGetFileDescriptor(), (UIntPtr)request, arg);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ioctl(fd fd, int request, byte[] arg)
        {
            int size = IOC_SIZE(request);
            if (arg != null && arg.Length != size)
                throw new ArgumentException($"Buffer does not match request size! Expected {size}, got {arg.Length}");

            return _ioctl(fd.DangerousGetFileDescriptor(), (UIntPtr)request, arg);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int ioctl<T>(fd fd, int request, ref T arg)
            where T : unmanaged
        {
            fixed (T* ptr = &arg)
            {
                return ioctl(fd, request, ptr);
            }
        }
    }
}