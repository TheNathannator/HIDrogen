#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static GameInputBackend s_GameInputBackend;

        private static void PlatformInitialize()
        {
            s_GameInputBackend = new GameInputBackend();
        }

        private static void PlatformUninitialize()
        {
            s_GameInputBackend?.Dispose();
        }
    }
}
#endif