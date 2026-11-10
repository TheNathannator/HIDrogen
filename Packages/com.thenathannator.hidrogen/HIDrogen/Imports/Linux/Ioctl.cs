#if UNITY_STANDALONE_LINUX
using System;
using System.Runtime.InteropServices;
using HIDrogen.Imports.Posix;

namespace HIDrogen.Imports.Linux
{
    internal readonly struct IoCode
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

        public readonly int value;

        public int direction => (value >> DIRSHIFT) & DIRMASK;
        public int type => (value >> TYPESHIFT) & TYPEMASK;
        public int number => (value >> NRSHIFT) & NRMASK;
        public int size => (value >> SIZESHIFT) & SIZEMASK;

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

    internal readonly struct IoCode<T>
        where T : unmanaged
    {
        public readonly IoCode @base;

        public int value => @base.value;

        public int direction => @base.direction;
        public int type => @base.type;
        public int number => @base.number;
        public int size => @base.size;

        public IoCode(int direction, int type, int number, int size)
        {
            @base = new IoCode(direction, type, number, size);
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

        public static unsafe IoCode<T> IOR<T>(int type, int number)
            where T : unmanaged
            => new IoCode<T>(IoCode.IOC_READ, type, number, sizeof(T));

        public static unsafe IoCode<T> IOW<T>(int type, int number)
            where T : unmanaged
            => new IoCode<T>(IoCode.IOC_WRITE, type, number, sizeof(T));

        public static unsafe IoCode<T> IOWR<T>(int type, int number)
            where T : unmanaged
            => new IoCode<T>(IoCode.IOC_READ | IoCode.IOC_WRITE, type, number, sizeof(T));

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

        private static int ioctl(fd fd, int request)
        {
            return _ioctl(fd.DangerousGetFileDescriptor(), (UIntPtr)request);
        }

        private static unsafe int ioctl(fd fd, int request, void* arg)
        {
            return _ioctl(fd.DangerousGetFileDescriptor(), (UIntPtr)request, arg);
        }

        public static int ioctl(fd fd, IoCode request)
        {
            if (request.size > 0)
            {
                throw new ArgumentException($"An argument must be provided for an ioc with non-zero size.");
            }

            return ioctl(fd, request.value);
        }

        public static unsafe int ioctl(fd fd, IoCode request, void* arg, int argSize)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(nameof(arg));
            }

            if ((uint)argSize < (uint)request.size)
            {
                throw new ArgumentException($"Argument is too small for the specified request (expected {request.size}, got {argSize})");
            }

            return ioctl(fd, request.value, arg);
        }

        public static unsafe int ioctl(fd fd, IoCode request, byte[] arg)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(nameof(arg));
            }

            fixed (byte* ptr = arg)
            {
                return ioctl(fd, request, ptr, arg.Length);
            }
        }

        public static unsafe int ioctl<T>(fd fd, IoCode<T> request, ref T arg)
            where T : unmanaged
        {
            if (request.size != sizeof(T))
            {
                throw new ArgumentException($"Malformed request code (expected {sizeof(T)}, got {request.size})");
            }

            fixed (T* ptr = &arg)
            {
                return ioctl(fd, request.@base, ptr, sizeof(T));
            }
        }
    }
}
#endif
