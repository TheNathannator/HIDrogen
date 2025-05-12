#if UNITY_STANDALONE_OSX
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static USBBackend s_USBBackend;

        static partial void PlatformInitialize()
        {
            TryInitializeBackend(ref s_USBBackend);
        }

        static partial void PlatformUninitialize()
        {
            TryUninitializeBackend(ref s_USBBackend);
        }
    }
}
#endif