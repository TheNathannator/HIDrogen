#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
namespace HIDrogen.Imports
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
}
#endif