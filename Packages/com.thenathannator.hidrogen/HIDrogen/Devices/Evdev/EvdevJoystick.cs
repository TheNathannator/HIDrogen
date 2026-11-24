using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace HIDrogen.Devices
{
    // Not platform guarded

    /// <summary>
    /// A joystick reported via evdev on Linux, with no pre-defined form.
    /// </summary>
    [InputControlLayout(hideInUI = true)]
    internal class EvdevJoystick : InputDevice
    {
        internal static void Initialize()
        {
            InputSystem.RegisterLayout<EvdevJoystick>();
        }
    }
}
