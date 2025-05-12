#if UNITY_STANDALONE_WIN
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
#if UNITY_2022_2_OR_NEWER
        private static XInputBackend s_XInputBackend;
#endif
        private static GameInputBackend s_GameInputBackend;

        static partial void PlatformInitialize()
        {
#if UNITY_2022_2_OR_NEWER
            TryInitializeBackend(ref s_XInputBackend);
#endif
            TryInitializeBackend(ref s_GameInputBackend);
        }


        static partial void PlatformUninitialize()
        {
#if UNITY_2022_2_OR_NEWER
            TryUninitializeBackend(ref s_XInputBackend);
#endif
            TryUninitializeBackend(ref s_GameInputBackend);
        }
    }
}
#endif