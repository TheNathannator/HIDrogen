#if UNITY_STANDALONE_LINUX
using HIDrogen.Backend.Linux;
using HIDrogen.Services.Linux;
using HIDrogen.Services.USB;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        static partial void PlatformInitialize()
        {
            if (BackendManager.TryCreateBackend<EvdevBackend>())
            {
                EvdevLayoutFinder.Initialize();
            }

            BackendManager.TryCreateBackend<HidRawBackend>();
            BackendManager.TryCreateService<USBService>();
            BackendManager.TryCreateService<UdevDeviceMonitor>();
            BackendManager.TryCreateService<LinuxShim>();
        }

        static partial void PlatformUninitialize()
        {
        }
    }
}
#endif