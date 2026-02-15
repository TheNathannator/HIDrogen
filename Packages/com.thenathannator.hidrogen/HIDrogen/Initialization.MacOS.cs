#if UNITY_STANDALONE_OSX
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        static partial void PlatformInitialize()
        {
        }

        static partial void PlatformUninitialize()
        {
        }
    }
}
#endif