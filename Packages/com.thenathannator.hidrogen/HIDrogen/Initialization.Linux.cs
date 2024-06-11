#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static HidApiBackend s_HidApiBackend;

        private static void PlatformInitialize()
        {
            s_HidApiBackend = new HidApiBackend();
            LinuxShim.Initialize();
        }

        private static void PlatformUninitialize()
        {
            s_HidApiBackend?.Dispose();
            LinuxShim.Uninitialize();
        }
    }
}
#endif