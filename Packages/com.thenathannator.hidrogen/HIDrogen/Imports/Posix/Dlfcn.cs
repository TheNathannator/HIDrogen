using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HIDrogen.Imports.Posix
{
    internal enum RTLD : int
    {
        LAZY = 0x00001,
        NOW = 0x00002,

        BINDING_MASK = 0x3,

        NOLOAD = 0x00004,
        DEEPBIND = 0x00008,
    }

    internal static partial class Dlfcn
    {
        private const string kLibName = Posix.LibName;

        [DllImport(kLibName, SetLastError = true)]
        public static extern IntPtr dlopen(
            [MarshalAs(UnmanagedType.LPStr)] string file,
            RTLD mode
        );

        [DllImport(kLibName, SetLastError = true, EntryPoint = "dlclose")]
        private static extern int _dlclose(
            IntPtr handle
        );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool dlclose(IntPtr handle)
            => _dlclose(handle) == 0; // 0 means success, non-0 means error

        [DllImport(kLibName, SetLastError = true)]
        public static extern IntPtr dlsym(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string name
        );

        [DllImport(kLibName, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public static extern string dlerror();
    }
}