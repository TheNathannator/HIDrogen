#if UNITY_STANDALONE_LINUX
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static HidApiBackend s_HidApiBackend;
        private static USBBackend s_USBBackend;
        private static EvdevBackend s_EvdevBackend;

        static partial void PlatformInitialize()
        {
            TryInitializeService(ref s_USBBackend);
            bool hidInitialized = TryInitializeBackend(ref s_HidApiBackend);
            bool evdevInitialized = TryInitializeBackend(ref s_EvdevBackend);
            if (evdevInitialized)
                s_EvdevBackend.hidBackend = s_HidApiBackend;
            if (hidInitialized || evdevInitialized)
                LinuxShim.Initialize();
        }

        static partial void PlatformUninitialize()
        {
            TryUninitializeService(ref s_USBBackend);
            TryUninitializeBackend(ref s_EvdevBackend);
            TryUninitializeBackend(ref s_HidApiBackend);
            LinuxShim.Uninitialize();
        }
    }
}
#endif