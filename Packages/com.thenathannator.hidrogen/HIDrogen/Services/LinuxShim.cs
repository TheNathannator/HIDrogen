#if UNITY_STANDALONE_LINUX
using System;
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend
{
    /// <summary>
    /// Shims out the existing Linux support so we can replace it with our own.
    /// </summary>
    internal static class LinuxShim
    {
        private const string kLinuxInterface = "Linux";
        // private const string kSDLInterface = "SDL";

        private static readonly InputDeviceFindControlLayoutDelegate s_SdlLayoutFinder =
            (InputDeviceFindControlLayoutDelegate) Assembly.GetAssembly(typeof(InputSystem))
                .GetType("UnityEngine.InputSystem.Linux.SDLLayoutBuilder")
                .GetMethod("OnFindLayoutForDevice",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new Type[] { typeof(InputDeviceDescription).MakeByRefType(), typeof(string), typeof(InputDeviceExecuteCommandDelegate) },
                    null)
                .CreateDelegate(typeof(InputDeviceFindControlLayoutDelegate));

        internal static void Initialize()
        {
            // Delay to first input system update
            InputSystem.onBeforeUpdate += OnUpdate;
        }

        internal static void Uninitialize()
        {
            InputSystem.onBeforeUpdate -= OnUpdate;
            InputSystem.onFindLayoutForDevice -= OnFindLayoutForDevice;
        }

        private static void OnUpdate()
        {
            InputSystem.onBeforeUpdate -= OnUpdate;

            // Remove SDL layout finder from layout find callbacks
            InputSystem.onFindLayoutForDevice -= s_SdlLayoutFinder;
            InputSystem.onFindLayoutForDevice += OnFindLayoutForDevice;

            // Remove shimmed devices
            foreach (var device in InputSystem.devices)
                RemoveIfShimmed(device);

            foreach (var device in InputSystem.disconnectedDevices)
                RemoveIfShimmed(device);
        }

        private static string OnFindLayoutForDevice(ref InputDeviceDescription description, string matchedLayout,
            InputDeviceExecuteCommandDelegate executeCommandDelegate)
        {
            if (description.interfaceName != kLinuxInterface)
                return null;

            // TODO: This does work, but it causes an exception internally
            // Unsure if this'll affect things adversely; also causes log noise
            return "Unsupported";
        }

        private static void RemoveIfShimmed(InputDevice device)
        {
            // This happens sometimes on domain reload for some reason
            if (device == null)
                return;

            if (device.description.interfaceName == kLinuxInterface) // || interfaceName != kSDLInterface) // Not ignored as Xbox devices are not handled by hidraw
            {
                Logging.Verbose($"Removing device {device}");
                InputSystem.RemoveDevice(device);
            }
        }
    }
}
#endif