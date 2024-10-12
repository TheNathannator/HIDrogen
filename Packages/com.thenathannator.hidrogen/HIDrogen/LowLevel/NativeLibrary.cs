using System;
using HIDrogen.Imports;

namespace HIDrogen.LowLevel
{
    internal class NativeLibrary : SafeHandleZeroIsInvalid
    {
        public override bool IsInvalid => handle == IntPtr.Zero;

        private NativeLibrary(IntPtr handle) : base(handle, true) { }

        public static bool TryLoad(string name, out NativeLibrary library)
        {
            IntPtr handle;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            handle = Kernel32.LoadLibrary(name);
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

        public bool TryGetExport(string name, out IntPtr address)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            address = Kernel32.GetProcAddress(handle, name);
#else
            #warning "NativeLibrary.TryGetExport not yet supported for the current platform"
            throw new NotSupportedException();
#endif
            return address != IntPtr.Zero;
        }

        protected override bool ReleaseHandle()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return Kernel32.FreeLibrary(handle);
#else
            #warning "NativeLibrary.ReleaseHandle not yet supported for the current platform"
            return false;
#endif
        }
    }
}