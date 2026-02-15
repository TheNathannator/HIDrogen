#if UNITY_STANDALONE_LINUX
using HIDrogen.Backend;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        static partial void PlatformInitialize()
        {
            if (s_BackendManager.TryCreateBackend<HidApiBackend>())
            {
                LinuxShim.Initialize();
            }
        }

        static partial void PlatformUninitialize()
        {
            LinuxShim.Uninitialize();
        }
    }
}
#endif