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
            // These registrations act as a best-effort fallback for gamepad devices
            // which do not match any specialized layout.
            //
            // Because the Linux input landscape is so old and fragmented, there are multiple possible
            // ways for gamepads to report their inputs. We only generically support a select few,
            // to keep things as sane as possible.
            //
            // Shoulders have two conventional ways each of being specified:
            // - BTN_TL/BTN_TR
            // - ABS_HAT1Y/ABS_HAT1X
            //
            // Triggers have two *documented* ways of being specified according to the
            // [gamepad specification](https://docs.kernel.org/input/gamepad.html), but
            // conventionally there is also a third way of specifying them:
            // - BTN_TL2/BTN_TR2
            // - ABS_HAT2Y/ABS_HAT2X
            // - ABS_Z/ABS_RZ
            // In practice, very few things actually make use of ABS_HAT2Y/ABS_HAT2X; instead
            // ABS_Z/ABS_RZ is the most common (as it is HID convention).
            //
            // We only support the following shoulder/trigger combinations generically:
            // - BTN_TL/BTN_TR, BTN_TL2/BTN_TR2
            // - BTN_TL/BTN_TR, ABS_Z/ABS_RZ
            // If other combinations are common and do not cause conflicts with the other generic
            // combinations, they may be supported here in the future, but generally it is preferred
            // to specially match and map any devices which do not fall under these categories.

            var baseMappings = new EvdevDeviceMapping()
                .WithKey(input_key_code.BTN_START, "start")
                .WithKey(input_key_code.BTN_SELECT, "select")
                .WithKey(input_key_code.BTN_A, "buttonSouth")
                .WithKey(input_key_code.BTN_B, "buttonEast")
                .WithKey(input_key_code.BTN_X, "buttonWest")
                .WithKey(input_key_code.BTN_Y, "buttonNorth")
                .WithHat(0, "dpad")
                .WithStick(input_abs_code.ABS_X, "leftStick")
                .WithStick(input_abs_code.ABS_RX, "rightStick")
                .WithKey(input_key_code.BTN_THUMB_L, "leftStickPress")
                .WithKey(input_key_code.BTN_THUMB_R, "rightStickPress");

            EvdevLayoutFinder.RegisterLayout<EvdevGamepad>(
                baseMappings.Clone()
                    .WithKey(input_key_code.BTN_TL, "leftShoulder")
                    .WithKey(input_key_code.BTN_TR, "rightShoulder")
                    .WithKey(input_key_code.BTN_TL2, "leftTrigger")
                    .WithKey(input_key_code.BTN_TR2, "rightTrigger"),
                new EvdevDeviceMatcher()
            );

            EvdevLayoutFinder.RegisterLayout<EvdevGamepad>(
                baseMappings.Clone()
                    .WithKey(input_key_code.BTN_TL, "leftShoulder")
                    .WithKey(input_key_code.BTN_TR, "rightShoulder")
                    .WithAbsButton(input_abs_code.ABS_Z, "leftTrigger")
                    .WithAbsButton(input_abs_code.ABS_RZ, "rightTrigger"),
                new EvdevDeviceMatcher()
            );
        }
    }
}
