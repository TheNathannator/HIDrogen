#if UNITY_STANDALONE_LINUX
using System;
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Services.Linux
{
    /// <summary>
    /// Shims out the existing Linux support so we can replace it with our own.
    /// </summary>
    internal class LinuxShim : ICustomInputService
    {
        public void Start()
        {
            // Shim native device discovery, as shimming their descriptions is not enough
            // InputManager InputSystem.s_Manager;
            var inputManagerField = typeof(InputSystem).GetField(
                "s_Manager",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            // IInputRuntime InputManager.m_Runtime;
            var inputRuntimeField = inputManagerField.FieldType.GetField(
                "m_Runtime",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            // Action<int, string> IInputRuntime.onDeviceDiscovered { get; set; }
            var onDeviceDiscoveredProperty = inputRuntimeField.FieldType.GetProperty(
                "onDeviceDiscovered",
                BindingFlags.Public | BindingFlags.Instance
            );

            object inputManager = inputManagerField.GetValue(null);
            object inputRuntime = inputRuntimeField.GetValue(inputManager);
            onDeviceDiscoveredProperty.SetValue(inputRuntime, null);

            // Just to be thorough, also remove SDL layout finder
            var sdlLayoutBuilder = typeof(InputSystem).Assembly.GetType("UnityEngine.InputSystem.Linux.SDLLayoutBuilder");
            var sdlLayoutFinderMethod = sdlLayoutBuilder.GetMethod(
                "OnFindLayoutForDevice",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new Type[]
                {
                    typeof(InputDeviceDescription).MakeByRefType(),
                    typeof(string),
                    typeof(InputDeviceExecuteCommandDelegate)
                },
                null
            );
            var sdlLayoutFinder = (InputDeviceFindControlLayoutDelegate) sdlLayoutFinderMethod.CreateDelegate(
                typeof(InputDeviceFindControlLayoutDelegate)
            );

            InputSystem.onFindLayoutForDevice -= sdlLayoutFinder;
            // Devices that would have been handled by the layout finder before will still
            // match the Joystick layout naturally, so we need to do another hack to ensure
            // an error will occur during matching
            InputSystem.onFindLayoutForDevice += OnFindLayoutForDevice;

            // Additional last-ditch effort to remove native devices
            InputSystem.onDeviceChange += OnDeviceChange;

            // Remove shimmed devices
            foreach (var device in InputSystem.devices)
            {
                RemoveIfShimmed(device);
            }

            foreach (var device in InputSystem.disconnectedDevices)
            {
                RemoveIfShimmed(device);
            }
        }

        public void Stop()
        {
        }

        public void Update()
        {
        }

        public void Dispose()
        {
        }

        // Last-ditch efforts to ensure these devices don't show up

        private static string OnFindLayoutForDevice(ref InputDeviceDescription description, string matchedLayout,
            InputDeviceExecuteCommandDelegate executeCommandDelegate)
        {
            if (description.interfaceName == "Linux" ||
                description.interfaceName == "SDL")
            {
                // NOTE: While this does work, it causes an exception internally,
                // which produces log noise and may potentially affect some things adversely.
                // I'm fine with keeping this because this is not the primary mechanism for blocking these devices
                return "Unsupported";
            }

            return null;
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            RemoveIfShimmed(device);
        }

        private static void RemoveIfShimmed(InputDevice device)
        {
            if (device != null && device.native)
            {
                Logging.Verbose($"Removing disallowed native device {device}");
                InputSystem.RemoveDevice(device);
            }
        }
    }
}
#endif
