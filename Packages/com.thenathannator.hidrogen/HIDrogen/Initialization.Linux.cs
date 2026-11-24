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
            BackendManager.TryCreateBackend<EvdevBackend>();
            BackendManager.TryCreateBackend<HidRawBackend>();
            // TODO: Temporarily disabled until it can be integrated with UdevDeviceMonitor
            // BackendManager.TryCreateService<USBService>();
            BackendManager.TryCreateService<UdevDeviceMonitor>();
            BackendManager.TryCreateService<LinuxShim>();
        }

        static partial void PlatformUninitialize()
        {
        }
    }
}
#endif