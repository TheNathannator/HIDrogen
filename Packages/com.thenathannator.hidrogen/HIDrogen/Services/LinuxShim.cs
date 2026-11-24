#if UNITY_STANDALONE_LINUX
using System;
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Services.Linux
{
    /// <summary>
    /// Shims out the existing Linux support so we can replace it with our own.
    /// </summary>
    internal class LinuxShim : ICustomInputService
    {
        private const string kLinuxInterface = "Linux";
        private const string kSDLInterface = "SDL";

        private Action<int, string> m_RealNativeDeviceDiscovered;

        // Use constructor to ensure native devices are always shimmed when HIDrogen is initialized,
        // regardless of when backends are actually started
        public LinuxShim()
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
            m_RealNativeDeviceDiscovered = (Action<int, string>)onDeviceDiscoveredProperty.GetValue(inputRuntime);
            onDeviceDiscoveredProperty.SetValue(inputRuntime, (Action<int, string>)OnNativeDeviceDiscovered);

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

            // Also remove HID layout finder; devices without explicit HID layouts should fall back to evdev
            var hidLayoutFinderMethod = typeof(HID).GetMethod(
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
            var hidLayoutFinder = (InputDeviceFindControlLayoutDelegate) hidLayoutFinderMethod.CreateDelegate(
                typeof(InputDeviceFindControlLayoutDelegate)
            );
            InputSystem.onFindLayoutForDevice -= sdlLayoutFinder;

            // Just in case, plan B:
            // Devices that would have been handled by the SDL layout finder before will still
            // match the Joystick layout naturally due to the description specifying a device class,
            // so we need to do another hack to ensure an error will occur during matching
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

        public void Start()
        {
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

        private void OnNativeDeviceDiscovered(int deviceId, string deviceDescriptor)
        {
            var description = InputDeviceDescription.FromJson(deviceDescriptor);
            if (!IsShimmed(description))
            {
                m_RealNativeDeviceDiscovered?.Invoke(deviceId, deviceDescriptor);
            }
        }

        // Last-ditch efforts to ensure these devices don't show up

        private static string OnFindLayoutForDevice(ref InputDeviceDescription description, string matchedLayout,
            InputDeviceExecuteCommandDelegate executeCommandDelegate)
        {
            if (IsShimmed(description))
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

        private static bool IsShimmed(InputDeviceDescription description)
        {
            return (description.interfaceName == kLinuxInterface ||
                description.interfaceName == kSDLInterface) &&
                // Mouse and keyboard may be reported through these interfaces; don't shim them out
                description.deviceClass != nameof(Mouse) &&
                description.deviceClass != nameof(Keyboard) &&
                description.deviceClass != nameof(Pen) &&
                description.deviceClass != nameof(Touchscreen);
        }

        private static void RemoveIfShimmed(InputDevice device)
        {
            if (device != null && IsShimmed(device.description))
            {
                Logging.Verbose($"Removing disallowed native device {device}");

                // Removing the device immediately may cause a queue race between other events,
                // such as a SYNC, so we queue the removal instead
                var removeEvent = DeviceRemoveEvent.Create(device.deviceId);
                InputSystem.QueueEvent(ref removeEvent);
            }
        }
    }
}
#endif
