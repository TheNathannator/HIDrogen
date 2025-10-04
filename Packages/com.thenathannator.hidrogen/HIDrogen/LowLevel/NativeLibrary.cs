#if UNITY_STANDALONE_WIN
    #define NATIVELIB_SUPPORT
    #define NATIVELIB_WINDOWS
#elif UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
    #define NATIVELIB_SUPPORT
    #define NATIVELIB_POSIX
#endif

#if NATIVELIB_SUPPORT

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if NATIVELIB_WINDOWS
    using HIDrogen.Imports.Windows;
#elif NATIVELIB_POSIX
    using HIDrogen.Imports.Posix;
#endif

namespace HIDrogen.LowLevel
{
    internal class NativeLibrary : SafeHandleZeroIsInvalid
    {
        public override bool IsInvalid => handle == IntPtr.Zero;

        private NativeLibrary(IntPtr handle) : base(handle, true) { }

        public static bool TryLoad(string name, out NativeLibrary library)
        {
            IntPtr handle;
#if NATIVELIB_WINDOWS
            handle = Kernel32.LoadLibrary(name);
#elif NATIVELIB_POSIX
            handle = Dlfcn.dlopen(name, RTLD.LAZY);
#endif

            CheckLoadError(handle, "Failed to load library {0}", name);
            if (handle == IntPtr.Zero)
            {
                library = null;
                return false;
            }

            library = new NativeLibrary(handle);
            return true;
        }

        private bool _TryGetExport(string name, out IntPtr address)
        {
#if NATIVELIB_WINDOWS
            address = Kernel32.GetProcAddress(handle, name);
#elif NATIVELIB_POSIX
            address = Dlfcn.dlsym(handle, name);
#endif
            CheckLoadError(address, "Failed to load export {0}", name);
            return address != IntPtr.Zero;
        }

        public bool TryGetExport<T>(string name, out T func)
            where T : Delegate
        {
            if (!_TryGetExport(name, out var address))
            {
                func = null;
                return false;
            }

            func = Marshal.GetDelegateForFunctionPointer<T>(address);
            return true;
        }

        public T GetExport<T>(string name)
            where T : Delegate
        {
            if (!TryGetExport(name, out T func))
                throw new EntryPointNotFoundException($"Failed to load export {name}!");

            return func;
        }

        protected override bool ReleaseHandle()
        {
#if NATIVELIB_WINDOWS
            return Kernel32.FreeLibrary(handle);
#elif NATIVELIB_POSIX
            return Dlfcn.dlclose(handle);
#endif
        }

        [Conditional("HIDROGEN_VERBOSE_LOGGING")]
        private static void CheckLoadError(IntPtr handle, string format, string name)
        {
            if (handle != IntPtr.Zero)
                return;

            string message;
#if NATIVELIB_WINDOWS
            int error = Marshal.GetLastWin32Error();
            message = $"{new System.ComponentModel.Win32Exception(error).Message} (0x{error:X8})";
#elif NATIVELIB_POSIX
            message = $"{Dlfcn.dlerror()} ({Posix.errno})";
#endif
            Logging.Verbose($"{string.Format(format, name)}: {message}");
        }
    }
}
#endif
