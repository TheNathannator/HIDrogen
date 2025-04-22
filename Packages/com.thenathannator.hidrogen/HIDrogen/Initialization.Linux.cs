#if UNITY_STANDALONE_LINUX
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static HidApiBackend s_HidApiBackend;
        private static USBBackend s_USBBackend;

        static partial void PlatformInitialize()
        {
            s_USBBackend = new USBBackend();
            s_HidApiBackend = new HidApiBackend();
            LinuxShim.Initialize();
        }

        static partial void PlatformUninitialize()
        {
            s_USBBackend?.Dispose();
            s_HidApiBackend?.Dispose();
            LinuxShim.Uninitialize();
        }
    }
}
#endif