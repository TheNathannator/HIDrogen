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
            s_XInputBackend = new XInputBackend();
#endif
            s_GameInputBackend = new GameInputBackend();
        }


        static partial void PlatformUninitialize()
        {
#if UNITY_2022_2_OR_NEWER
            s_XInputBackend?.Dispose();
#endif
            s_GameInputBackend?.Dispose();
        }
    }
}
#endif