#if UNITY_STANDALONE_WIN
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        static partial void PlatformInitialize()
        {
#if UNITY_2022_2_OR_NEWER
            s_BackendManager.TryCreateBackend<XInputBackend>();
#endif
            s_BackendManager.TryCreateBackend<GameInputBackend>();
        }

        static partial void PlatformUninitialize()
        {
        }
    }
}
#endif