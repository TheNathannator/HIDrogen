using HIDrogen.Imports.Linux;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace HIDrogen.Backend.Linux
{
    // NOTE: This layout is never directly matched;
    // instead a derived layout is always built on-demand via EvdevLayoutFinder
    [InputControlLayout(hideInUI = true)]
    internal class EvdevGamepad : Gamepad
    {
        internal static void Initialize()
        {
            EvdevLayoutFinder.RegisterLayout<EvdevGamepad>(
                new EvdevDeviceMapping()
                    .WithKey(input_key_code.BTN_A, "buttonSouth")
                    .WithKey(input_key_code.BTN_B, "buttonEast")
                    .WithKey(input_key_code.BTN_X, "buttonWest")
                    .WithKey(input_key_code.BTN_Y, "buttonNorth")
                    .WithHat(0, "dpad")
                    .WithKey(input_key_code.BTN_TL, "leftShoulder")
                    .WithKey(input_key_code.BTN_TR, "rightShoulder")
                    .WithKey(input_key_code.BTN_THUMB_L, "leftStickPress")
                    .WithKey(input_key_code.BTN_THUMB_R, "rightStickPress")
                    .WithKey(input_key_code.BTN_START, "start")
                    .WithKey(input_key_code.BTN_SELECT, "select")
                    .WithStick(input_abs_code.ABS_X, "leftStick")
                    .WithStick(input_abs_code.ABS_RX, "rightStick")
                    .WithAbsButton(input_abs_code.ABS_Z, "leftTrigger")
                    .WithAbsButton(input_abs_code.ABS_RZ, "rightTrigger"),
                new EvdevDeviceMatcher()
            );
        }
    }
}
