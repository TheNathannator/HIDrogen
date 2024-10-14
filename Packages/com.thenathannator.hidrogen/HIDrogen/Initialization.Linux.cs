#if UNITY_STANDALONE_LINUX
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static HidApiBackend s_HidApiBackend;

        static partial void PlatformInitialize()
        {
            s_HidApiBackend = new HidApiBackend();
            LinuxShim.Initialize();
        }

        static partial void PlatformUninitialize()
        {
            s_HidApiBackend?.Dispose();
            LinuxShim.Uninitialize();
        }
    }
}
#endif