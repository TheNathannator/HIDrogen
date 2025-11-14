#if UNITY_STANDALONE_OSX
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static USBBackend s_USBBackend;

        static partial void PlatformInitialize()
        {
            TryInitializeService(ref s_USBBackend);
        }

        static partial void PlatformUninitialize()
        {
            TryUninitializeService(ref s_USBBackend);
        }
    }
}
#endif