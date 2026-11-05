#if UNITY_STANDALONE_LINUX
using HIDrogen.Backend;
using HIDrogen.Services;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        static partial void PlatformInitialize()
        {
            BackendManager.TryCreateService<USBService>();
            if (BackendManager.TryCreateBackend<HidApiBackend>())
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