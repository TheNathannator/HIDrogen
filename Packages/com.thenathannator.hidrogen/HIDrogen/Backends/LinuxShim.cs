#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    /// <summary>
    /// Shims out the existing Linux support so we can replace it with our own.
    /// </summary>
    internal static class LinuxShim
    {
        private const string kLinuxInterface = "Linux";
        // private const string kSDLInterface = "SDL";

        internal static void Initialize()
        {
            InputSystem.onDeviceChange += OnDeviceChange;

#if !HIDROGEN_KEEP_NATIVE_DEVICES
            Logging.Verbose("Removing devices from the 'Linux' interface");
            foreach (var device in InputSystem.devices)
                RemoveIfShimmed(device);

            foreach (var device in InputSystem.disconnectedDevices)
                RemoveIfShimmed(device);
#endif
        }

        internal static void Uninitialize()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
#if !HIDROGEN_KEEP_NATIVE_DEVICES
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.HardReset: // Occurs on domain reload
                    RemoveIfShimmed(device);
                    break;

                default:
                    break;
            }
#endif
        }

#if !HIDROGEN_KEEP_NATIVE_DEVICES
        private static void RemoveIfShimmed(InputDevice device)
        {
            // This happens sometimes on domain reload for some reason
            if (device == null)
                return;

            if (device.description.interfaceName == kLinuxInterface) // || interfaceName != kSDLInterface) // Not ignored as Xbox devices are not handled by hidraw
            {
                Logging.Verbose($"Removing device {device}");
                InputSystem.RemoveDevice(device);
                // InputSystem.DisableDevice(device); // This causes a deadlock for an extended period, at least on my (lower-end) laptop
            }
        }
#endif
    }
}
#endif