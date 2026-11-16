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
            LinuxShim.Initialize();

            BackendManager.TryCreateBackend<EvdevBackend>();
            BackendManager.TryCreateBackend<HidRawBackend>();
            BackendManager.TryCreateService<USBService>();
            BackendManager.TryCreateService<UdevDeviceMonitor>();
        }

        static partial void PlatformUninitialize()
        {
            LinuxShim.Uninitialize();
        }
    }
}
#endif