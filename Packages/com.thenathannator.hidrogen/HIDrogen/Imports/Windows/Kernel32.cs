#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

namespace HIDrogen.Imports.Windows
{
    internal enum Win32Error : uint
    {
        ERROR_SUCCESS = 0,

        ERROR_DEVICE_NOT_CONNECTED = 1167,
    }

    internal enum HResult : int
    {
        S_OK = 0,

        E_NOTIMPL = unchecked((int)0x80004001),
    }

    internal static class Kernel32
    {
        internal const string kFileName = "kernel32.dll";

        [DllImport(kFileName, CharSet = CharSet.Unicode, EntryPoint = "LoadLibraryW")]
        internal static extern IntPtr LoadLibrary(
            [MarshalAs(UnmanagedType.LPWStr)] string lpLibFileName
        );

        [DllImport(kFileName, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(
            IntPtr hLibModule
        );

        [DllImport(kFileName)]
        internal static extern IntPtr GetProcAddress(
            IntPtr hModule,
            [MarshalAs(UnmanagedType.LPStr)] string lpProcName
        );
    }
}
#endif