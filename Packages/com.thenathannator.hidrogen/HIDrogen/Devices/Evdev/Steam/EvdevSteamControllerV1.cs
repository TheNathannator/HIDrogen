using HIDrogen.Devices.LowLevel;
using HIDrogen.Imports.Linux;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace HIDrogen.Devices
{
    // Not platform guarded

    using static ControlNames;
    using static DisplayNames;

    /// <summary>
    /// The original Steam Controller from 2015.
    /// </summary>
    [InputControlLayout(hideInUI = true)]
    internal class EvdevSteamControllerV1 : Gamepad
    {
        internal static void Initialize()
        {
            var mappings = new EvdevDeviceMapping()
                .WithKey(input_key_code.BTN_START, (StartButton, DisplayStart))
                .WithKey(input_key_code.BTN_SELECT, (SelectButton, DisplaySelect))
                .WithKey(input_key_code.BTN_MODE, (SystemButton, DisplaySteam))

                .WithKey(input_key_code_alt.BTN_SOUTH, (ButtonSouth, DisplayA))
                .WithKey(input_key_code_alt.BTN_EAST, (ButtonEast, DisplayB))
                .WithKey(input_key_code_alt.BTN_WEST, (ButtonWest, DisplayX))
                .WithKey(input_key_code_alt.BTN_NORTH, (ButtonNorth, DisplayY))

                .WithHat(0, (Dpad, DisplayDpad))

                .WithKey(input_key_code.BTN_TL, (LeftShoulder, DisplayL1))
                .WithKey(input_key_code.BTN_TR, (RightShoulder, DisplayR1))

                .WithAbsButton(input_abs_code.ABS_HAT2Y, (LeftTrigger, DisplayL2))
                .WithAbsButton(input_abs_code.ABS_HAT2X, (RightTrigger, DisplayR2))
                .WithKey(input_key_code.BTN_TL_2, (LeftTriggerPress, DisplayL2Press))
                .WithKey(input_key_code.BTN_TR_2, (RightTriggerPress, DisplayR2Press))

                .WithStick(input_abs_code.ABS_X, (LeftStick, DisplayLeftStick))
                .WithStick(input_abs_code.ABS_RX, (RightStick, DisplayRightStick))
                .WithKey(input_key_code.BTN_THUMB_L, (LeftStickPress, DisplayL3))
                .WithKey(input_key_code.BTN_THUMB_R, (RightStickPress, DisplayR3))

                .WithStick(input_abs_code.ABS_HAT0X, (LeftTouchpad, DisplayLeftTouchpad), EvdevStickOption.InvertY)
                .WithStick(input_abs_code.ABS_HAT1X, (RightTouchpad, DisplayRightTouchpad), EvdevStickOption.InvertY)
                .WithKey(input_key_code.BTN_THUMB, new EvdevControlMapping()
                {
                    path = LeftTouchpadPress,
                    displayName = DisplayLeftTouchpadPress,
                    options = EvdevMappingOption.Synthetic,
                })
                .WithKey(input_key_code.BTN_THUMB_2, new EvdevControlMapping()
                {
                    path = RightTouchpadPress,
                    displayName = DisplayRightTouchpadPress,
                    options = EvdevMappingOption.Synthetic,
                });

            // Steam Controller v1 (wired)
            EvdevLayoutFinder.RegisterLayout<EvdevSteamControllerV1>(
                mappings.Clone()
                    .WithKey(input_key_code.BTN_GRIP_L, (LeftGrip1, DisplayL4))
                    .WithKey(input_key_code.BTN_GRIP_R, (RightGrip1, DisplayR4)),
                new EvdevDeviceMatcher().WithHardwareIds(0x28DE, 0x1102)
            );
            // Steam Controller v1 (BLE)
            EvdevLayoutFinder.RegisterLayout<EvdevSteamControllerV1>(
                mappings.Clone()
                    .WithKey(input_key_code.BTN_TRIGGER_HAPPY_1, (LeftGrip1, DisplayL4))
                    .WithKey(input_key_code.BTN_TRIGGER_HAPPY_2, (RightGrip1, DisplayR4)),
                new EvdevDeviceMatcher().WithHardwareIds(0x28DE, 0x1142)
            );
        }
    }
}
