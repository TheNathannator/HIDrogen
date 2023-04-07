#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    /// <summary>
    /// Shims out the existing Linux support so we can replace it with our own.
    /// </summary>
    internal static class LinuxShim
    {
        internal static void Initialize()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            string interfaceName = device.description.interfaceName;
            if (interfaceName != "Linux" && interfaceName != "SDL")
                return;

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.HardReset: // Occurs on domain relod
                    // InputSystem.DisableDevice(device); // This causes a deadlock for an extended period, at least on my (lower-end) laptop
                    InputSystem.RemoveDevice(device);
                    break;

                default:
                    break;
            }
        }
    }
}
#endif