using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HIDrogen.Imports
{
    internal class fd : SafeHandleMinusOneIsInvalid
    {
        internal fd() : base(false) => SetHandle((IntPtr)(-1));
        internal fd(int fd, bool ownsHandle) : base(ownsHandle) => SetHandle((IntPtr)fd);

        protected override bool ReleaseHandle() => Libc.close((int)handle) >= 0;
        public int DangerousGetFileDescriptor() => (int)DangerousGetHandle();
    }

    internal static partial class Libc
    {
        private struct pollfd
        {
            public int fd;
            public short events;
            public short revents;
        };

        public const int O_RDONLY = 0x00;
        public const int O_WRONLY = 0x01;
        public const int O_RDWR = 0x02;

        public const int O_NONBLOCK = 0x800;
        public const int O_CLOEXEC = 02000000;

        public const short POLLIN = 0x001;
        public const short POLLPRI = 0x002;
        public const short POLLOUT = 0x004;

        [DllImport(kLibName, EntryPoint = "open", SetLastError = true)]
        private static extern int _open(
            [MarshalAs(UnmanagedType.LPStr)] string file,
            int flags
        );

        [DllImport(kLibName, SetLastError = true)]
        public static extern int close(
            int fd
        );

        [DllImport(kLibName, EntryPoint = "read", SetLastError = true)]
        private static extern unsafe UIntPtr _read(
            int fd,
            void* buffer,
            UIntPtr bufferSize
        );

        [DllImport(kLibName, EntryPoint = "read", SetLastError = true)]
        private static extern UIntPtr _read(
            int fd,
            byte[] buffer,
            UIntPtr bufferSize
        );

        [DllImport(kLibName, EntryPoint = "write", SetLastError = true)]
        private static extern unsafe UIntPtr _write(
            int fd,
            void* buffer,
            UIntPtr bufferSize
        );

        [DllImport(kLibName, EntryPoint = "write", SetLastError = true)]
        private static extern UIntPtr _write(
            int fd,
            byte[] buffer,
            UIntPtr bufferSize
        );

        [DllImport(kLibName, EntryPoint = "poll", SetLastError = true)]
        private static extern unsafe int _poll(
            pollfd* fds,
            UIntPtr fdCount,
            int timeout
        );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static fd open(string file, int flags)
        {
            int fd = _open(file, flags);
            return fd < 0 ? new fd() : new fd(fd, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int read(fd fd, void* buffer, int bufferSize)
            => (int)_read(fd.DangerousGetFileDescriptor(), buffer, (UIntPtr)bufferSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int read(fd fd, byte[] buffer)
            => (int)_read(fd.DangerousGetFileDescriptor(), buffer, (UIntPtr)buffer?.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int write(fd fd, void* buffer, int bufferSize)
            => (int)_write(fd.DangerousGetFileDescriptor(), buffer, (UIntPtr)bufferSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int write(fd fd, byte[] buffer)
            => (int)_write(fd.DangerousGetFileDescriptor(), buffer, (UIntPtr)buffer?.Length);

        public static unsafe int poll(short events, int timeout, fd fd)
        {
            var buffer = new pollfd()
            {
                fd = fd.DangerousGetFileDescriptor(),
                events = events
            };

            return _poll(&buffer, (UIntPtr)1, timeout);
        }

        public static unsafe int poll(short events, int timeout, params fd[] fds)
        {
            var buffer = stackalloc pollfd[fds.Length];
            for (int i = 0; i < fds.Length; i++)
            {
                buffer[i].fd = fds[i].DangerousGetFileDescriptor();
                buffer[i].events = events;
            };

            return _poll(buffer, (UIntPtr)fds.Length, timeout);
        }
    }
}