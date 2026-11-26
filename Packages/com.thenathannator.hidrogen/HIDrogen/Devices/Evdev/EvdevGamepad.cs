using HIDrogen.Devices.LowLevel;
using HIDrogen.Imports.Linux;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace HIDrogen.Devices
{
    // Not platform guarded

    using static ControlNames;

    [InputControlLayout(hideInUI = true, displayName = "Generic Gamepad")]
    internal sealed class EvdevGamepad : Gamepad
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
            // - Fully digital (BTN_TL/BTN_TR, BTN_TL2/BTN_TR2)
            // - Digital shoulders, analog triggers (BTN_TL/BTN_TR, ABS_Z/ABS_RZ)
            // If other combinations are common and do not cause conflicts with the other generic
            // combinations, they may be supported here in the future, but generally it is preferred
            // to specially match and map any devices which do not fall under these categories.

            var baseMappings = new EvdevDeviceMapping()
                .WithKey(input_key_code.BTN_START, StartButton)
                .WithKey(input_key_code.BTN_SELECT, SelectButton)

                .WithKey(input_key_code_alt.BTN_SOUTH, ButtonSouth)
                .WithKey(input_key_code_alt.BTN_EAST, ButtonEast)
                .WithKey(input_key_code_alt.BTN_WEST, ButtonWest)
                .WithKey(input_key_code_alt.BTN_NORTH, ButtonNorth)

                .WithHat(0, Dpad)

                .WithStick(input_abs_code.ABS_X, LeftStick)
                .WithStick(input_abs_code.ABS_RX, RightStick)
                .WithKey(input_key_code.BTN_THUMB_L, LeftStickPress)
                .WithKey(input_key_code.BTN_THUMB_R, RightStickPress);

            // Fully digital
            EvdevLayoutFinder.RegisterLayout<EvdevGamepad>(
                baseMappings.Clone()
                    .WithKey(input_key_code.BTN_TL, LeftShoulder)
                    .WithKey(input_key_code.BTN_TR, RightShoulder)
                    .WithKey(input_key_code.BTN_TL_2, LeftTrigger)
                    .WithKey(input_key_code.BTN_TR_2, RightTrigger),
                new EvdevDeviceMatcher()
            );

            // Digital shoulders, analog triggers
            EvdevLayoutFinder.RegisterLayout<EvdevGamepad>(
                baseMappings.Clone()
                    .WithKey(input_key_code.BTN_TL, LeftShoulder)
                    .WithKey(input_key_code.BTN_TR, RightShoulder)
                    .WithAbsButton(input_abs_code.ABS_Z, LeftTrigger)
                    .WithAbsButton(input_abs_code.ABS_RZ, RightTrigger),
                new EvdevDeviceMatcher()
            );
        }
    }
}
