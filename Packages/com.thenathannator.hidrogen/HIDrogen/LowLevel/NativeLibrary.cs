#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    #define NATIVELIB_WINDOWS
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    #define NATIVELIB_POSIX
#endif

using System;
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
#else
            #warning "NativeLibrary.TryLoad not yet supported for the current platform"
            throw new NotSupportedException();
#endif

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
#else
            #warning "NativeLibrary.TryGetExport not yet supported for the current platform"
            throw new NotSupportedException();
#endif
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
#else
            #warning "NativeLibrary.ReleaseHandle not yet supported for the current platform"
            return false;
#endif
        }
    }
}