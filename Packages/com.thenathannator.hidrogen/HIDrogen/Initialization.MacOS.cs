#if UNITY_STANDALONE_OSX
using HIDrogen.Backend;
using HIDrogen.Services;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        static partial void PlatformInitialize()
        {
            s_BackendManager.TryCreateService<USBService>();
        }

        static partial void PlatformUninitialize()
        {
        }
    }
}
#endif