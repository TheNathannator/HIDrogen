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

    // Partially based on https://github.com/mellinoe/nativelibraryloader/blob/master/NativeLibraryLoader/Libdl.cs
    // Note: NativeLibrary is not used here, as this is the very class which powers it on Unix systems.
    // We *must* rely on the .NET runtime to load things for us,
    // hence two separate definitions referring to different file names.
    internal static partial class Dlfcn
    {
        internal static class Libdl
        {
            private const string kLibName = "libdl";

            [DllImport(kLibName, SetLastError = true)]
            public static extern IntPtr dlopen(
                [MarshalAs(UnmanagedType.LPStr)] string file,
                RTLD mode
            );

            [DllImport(kLibName, SetLastError = true)]
            public static extern int dlclose(
                IntPtr handle
            );

            [DllImport(kLibName, SetLastError = true)]
            public static extern IntPtr dlsym(
                IntPtr handle,
                [MarshalAs(UnmanagedType.LPStr)] string name
            );

            [DllImport(kLibName, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.LPStr)]
            public static extern string dlerror();
        }

        internal static class Libdl2
        {
            private const string kLibName = "libdl.so.2";

            [DllImport(kLibName, SetLastError = true)]
            public static extern IntPtr dlopen(
                [MarshalAs(UnmanagedType.LPStr)] string file,
                RTLD mode
            );

            [DllImport(kLibName, SetLastError = true)]
            public static extern int dlclose(
                IntPtr handle
            );

            [DllImport(kLibName, SetLastError = true)]
            public static extern IntPtr dlsym(
                IntPtr handle,
                [MarshalAs(UnmanagedType.LPStr)] string name
            );

            [DllImport(kLibName, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.LPStr)]
            public static extern string dlerror();
        }

        private static readonly bool s_UseLibdl2 = false;

        static Dlfcn()
        {
            try
            {
                Libdl.dlerror();
            }
            catch
            {
                s_UseLibdl2 = true;
            }
        }

        public static IntPtr dlopen(string file, RTLD mode)
            => s_UseLibdl2 ? Libdl2.dlopen(file, mode) : Libdl.dlopen(file, mode);

        public static bool dlclose(IntPtr handle)
        {
            int result = s_UseLibdl2 ? Libdl2.dlclose(handle) : Libdl.dlclose(handle);
            return result == 0; // 0 means success, non-0 means error
        }

        public static IntPtr dlsym(IntPtr handle, string name)
            => s_UseLibdl2 ? Libdl2.dlsym(handle, name) : Libdl.dlsym(handle, name);

        public static string dlerror()
            => s_UseLibdl2 ? Libdl2.dlerror() : Libdl.dlerror();
    }
}