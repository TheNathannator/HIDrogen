#if !(UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
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