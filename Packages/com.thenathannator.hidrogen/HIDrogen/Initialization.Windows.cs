#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static GameInputBackend s_GameInputBackend;

        static partial void PlatformInitialize()
        {
            s_GameInputBackend = new GameInputBackend();
        }


        static partial void PlatformUninitialize()
        {
            s_GameInputBackend?.Dispose();
        }
    }
}
#endif