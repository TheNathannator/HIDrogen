#if !(UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static void PlatformInitialize()
        {
        }

        private static void PlatformUninitialize()
        {
        }
    }
}
#endif