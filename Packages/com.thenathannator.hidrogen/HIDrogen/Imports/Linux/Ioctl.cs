#if UNITY_STANDALONE_LINUX
using System;
using System.Runtime.InteropServices;
using HIDrogen.Imports.Posix;

namespace HIDrogen.Imports.Linux
{
    internal struct IoCode
    {
        public const int NRBITS = 8;
        public const int TYPEBITS = 8;
#if UNITY_64 // these constants are technically architecture-specific
        public const int SIZEBITS = 14;
        public const int DIRBITS = 2;
#else
        #error "IOC_(SIZEBITS|DIRBITS) need to be defined for the current architecture"
        public const int SIZEBITS = 0;
        public const int DIRBITS = 0;
#endif

        public const int NRMASK = (1 << NRBITS) - 1;
        public const int TYPEMASK = (1 << TYPEBITS) - 1;
        public const int SIZEMASK = (1 << SIZEBITS) - 1;
        public const int DIRMASK = (1 << DIRBITS) - 1;

        public const int NRSHIFT = 0;
        public const int TYPESHIFT = NRSHIFT + NRBITS;
        public const int SIZESHIFT = TYPESHIFT + TYPEBITS;
        public const int DIRSHIFT = SIZESHIFT + SIZEBITS;

#if UNITY_64 // these constants are technically architecture-specific
        public const int IOC_NONE = 0;
        public const int IOC_WRITE = 1;
        public const int IOC_READ = 2;
#else
        #error "IOC_(NONE|WRITE|READ) need to be defined for the current architecture"
        public const int IOC_NONE = 0;
        public const int IOC_WRITE = 0;
        public const int IOC_READ = 0;
#endif

        public const int IOC_IN = IOC_WRITE << DIRSHIFT;
        public const int IOC_OUT = IOC_READ << DIRSHIFT;
        public const int IOC_INOUT = (IOC_WRITE | IOC_READ) << DIRSHIFT;
        public const int IOCSIZE_MASK = SIZEMASK << SIZESHIFT;
        public const int IOCSIZE_SHIFT = SIZESHIFT;

        public int value;

        public int direction => (value >> DIRSHIFT) & DIRMASK;
        public int type => (value >> TYPESHIFT) & TYPEMASK;
        public int number => (value >> NRSHIFT) & NRMASK;

        public int size
        {
            get => (value >> SIZESHIFT) & SIZEMASK;
            set => this.value = (this.value & ~(SIZEMASK << SIZESHIFT)) | ((value & SIZEMASK) << SIZESHIFT);
        }

        public IoCode(int direction, int type, int number, int size)
        {
            if ((uint)number > NRMASK)
                throw new ArgumentOutOfRangeException(nameof(number));
            if ((uint)type > TYPEMASK)
                throw new ArgumentOutOfRangeException(nameof(type));
            if ((uint)size > SIZEMASK)
                throw new ArgumentOutOfRangeException(nameof(size));
            if ((uint)direction > DIRMASK)
                throw new ArgumentOutOfRangeException(nameof(direction));

            value =
                ((direction & DIRMASK) << DIRSHIFT) |
                ((type & TYPEMASK) << TYPESHIFT) |
                ((number & NRMASK) << NRSHIFT) |
                ((size & SIZEMASK) << SIZESHIFT);
        }
    }

    internal struct IoCode<T>
        where T : unmanaged
    {
        public IoCode @base;

        public int value => @base.value;

        public int direction => @base.direction;
        public int type => @base.type;
        public int number => @base.number;

        public int size
        {
            get => @base.size;
            set => @base.size = value;
        }

        public unsafe int count
        {
            get => @base.size / sizeof(T);
            set => @base.size = value * sizeof(T);
        }

        public unsafe IoCode(int direction, int type, int number, int count)
        {
            @base = new IoCode(direction, type, number, count * sizeof(T));
        }
    }

    internal static partial class Ioctl
    {
        private const string kLibName = Libc.LibName;

        public static IoCode IO(int type, int number)
            => new IoCode(IoCode.IOC_NONE, type, number, 0);

        public static IoCode IOR(int type, int number, int size)
            => new IoCode(IoCode.IOC_READ, type, number, size);

        public static IoCode IOW(int type, int number, int size)
            => new IoCode(IoCode.IOC_WRITE, type, number, size);

        public static IoCode IOWR(int type, int number, int size)
            => new IoCode(IoCode.IOC_READ | IoCode.IOC_WRITE, type, number, size);

        public static IoCode<T> IOR<T>(int type, int number, int count = 1)
            where T : unmanaged
            => new IoCode<T>(IoCode.IOC_READ, type, number, count);

        public static IoCode<T> IOW<T>(int type, int number, int count = 1)
            where T : unmanaged
            => new IoCode<T>(IoCode.IOC_WRITE, type, number, count);

        public static IoCode<T> IOWR<T>(int type, int number, int count = 1)
            where T : unmanaged
            => new IoCode<T>(IoCode.IOC_READ | IoCode.IOC_WRITE, type, number, count);

        [DllImport(kLibName, EntryPoint = "ioctl", SetLastError = true)]
        private static extern int _ioctl(
            int fd, // int
            UIntPtr request // unsigned long int
        );

        [DllImport(kLibName, EntryPoint = "ioctl", SetLastError = true)]
        private static extern unsafe int _ioctl(
            int fd, // int
            UIntPtr request, // unsigned long int
            void* arg // ...
        );

        // TODO: is this actually valid?
        // public static int ioctl(fd fd, IoCode request)
        // {
        //     if (request.size > 0)
        //     {
        //         throw new ArgumentException($"An argument must be provided for an ioc with non-zero size.");
        //     }

        //     return _ioctl(fd.DangerousGetFileDescriptor(), (UIntPtr)request.value);
        // }

        public static unsafe int ioctl_unsafe(fd fd, IoCode request, void* arg)
        {
            return _ioctl(fd.DangerousGetFileDescriptor(), (UIntPtr)request.value, arg);
        }

        public static unsafe int ioctl(fd fd, IoCode request, byte* arg, int argLength)
        {
            if ((uint)argLength < (uint)request.size)
            {
                throw new ArgumentException($"Argument is too small for the specified request (expected {request.size}, got {argLength})");
            }

            return ioctl_unsafe(fd, request, arg);
        }

        public static unsafe int ioctl(fd fd, IoCode request, byte[] arg)
        {
            fixed (byte* ptr = arg)
            {
                return ioctl(fd, request, ptr, arg?.Length ?? 0);
            }
        }

        public static unsafe int ioctl<T>(fd fd, IoCode<T> request, T* arg, int argCount)
            where T : unmanaged
        {
            if (request.size % sizeof(T) != 0)
            {
                throw new ArgumentException($"Malformed request code (expected a multiple of {sizeof(T)}; {request.size} % {sizeof(T)} -> {request.size % sizeof(T)})");
            }

            return ioctl(fd, request.@base, (byte*)arg, argCount * sizeof(T));
        }

        public static unsafe int ioctl<T>(fd fd, IoCode<T> request, ref T arg)
            where T : unmanaged
        {
            fixed (T* ptr = &arg)
            {
                return ioctl(fd, request, ptr, 1);
            }
        }

        public static unsafe int ioctl<T>(fd fd, IoCode<T> request, T[] arg)
            where T : unmanaged
        {
            fixed (T* ptr = arg)
            {
                return ioctl(fd, request, ptr, arg?.Length ?? 0);
            }
        }
    }
}
#endif
