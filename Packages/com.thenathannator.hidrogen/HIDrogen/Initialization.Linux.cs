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
            TryInitializeService(ref s_USBBackend);
            if (TryInitializeBackend(ref s_HidApiBackend))
            {
                LinuxShim.Initialize();
            }
        }

        static partial void PlatformUninitialize()
        {
            TryUninitializeService(ref s_USBBackend);
            TryUninitializeBackend(ref s_HidApiBackend);
            LinuxShim.Uninitialize();
        }
    }
}
#endif