#if UNITY_STANDALONE_WIN
using HIDrogen.Backend;
using HIDrogen.Services;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        static partial void PlatformInitialize()
        {
            BackendManager.TryCreateService<USBService>();
#if UNITY_2022_2_OR_NEWER
            BackendManager.TryCreateBackend<XInputBackend>();
#endif
            BackendManager.TryCreateBackend<GameInputBackend>();
        }

        static partial void PlatformUninitialize()
        {
        }
    }
}
#endif