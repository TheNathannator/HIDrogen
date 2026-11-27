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
    /// The newer Steam Controller from 2026.
    /// </summary>
    [InputControlLayout(hideInUI = true)]
    internal class EvdevSteamControllerV2 : Gamepad
    {
        internal static void Initialize()
        {
            var mappings = new EvdevDeviceMapping()
                .WithKey(input_key_code.BTN_START, (StartButton, DisplayStart))
                .WithKey(input_key_code.BTN_SELECT, (SelectButton, DisplaySelect))
                .WithKey(input_key_code.BTN_MODE, (SystemButton, DisplaySteam))
                .WithKey(input_key_code.BTN_BASE, (ShareButton, DisplayQuickAccess))

                .WithKey(input_key_code_alt.BTN_A, (ButtonSouth, DisplayA))
                .WithKey(input_key_code_alt.BTN_B, (ButtonEast, DisplayB))
                .WithKey(input_key_code_alt.BTN_X, (ButtonWest, DisplayX))
                .WithKey(input_key_code_alt.BTN_Y, (ButtonNorth, DisplayY))

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
                .WithKey(input_key_code.BTN_THUMB, (LeftTouchpadPress, DisplayLeftTouchpadPress))
                .WithKey(input_key_code.BTN_THUMB_2, (RightTouchpadPress, DisplayRightTouchpadPress));

            var gripMappings = mappings.Clone()
                .WithKey(input_key_code.BTN_GRIP_L, (LeftGrip1, DisplayL4))
                .WithKey(input_key_code.BTN_GRIP_R, (RightGrip1, DisplayR4))
                .WithKey(input_key_code.BTN_GRIP_L_2, (LeftGrip2, DisplayL5))
                .WithKey(input_key_code.BTN_GRIP_R_2, (RightGrip2, DisplayR5));

            var triggerHappyMappings = mappings.Clone()
                .WithKey(input_key_code.BTN_TRIGGER_HAPPY_1, (LeftGrip1, DisplayL4))
                .WithKey(input_key_code.BTN_TRIGGER_HAPPY_2, (RightGrip1, DisplayR4))
                .WithKey(input_key_code.BTN_TRIGGER_HAPPY_3, (LeftGrip2, DisplayL5))
                .WithKey(input_key_code.BTN_TRIGGER_HAPPY_4, (RightGrip2, DisplayR5));

            // Steam Controller v2 (Ibex) (wired)
            EvdevLayoutFinder.RegisterLayout<EvdevSteamControllerV2>(
                gripMappings,
                new EvdevDeviceMatcher().WithHardwareIds(0x28DE, 0x1302)
            );
            EvdevLayoutFinder.RegisterLayout<EvdevSteamControllerV2>(
                triggerHappyMappings,
                new EvdevDeviceMatcher().WithHardwareIds(0x28DE, 0x1302)
            );

            // Steam Controller v2 (Ibex) (BLE)
            EvdevLayoutFinder.RegisterLayout<EvdevSteamControllerV2>(
                gripMappings,
                new EvdevDeviceMatcher().WithHardwareIds(0x28DE, 0x1303)
            );
            EvdevLayoutFinder.RegisterLayout<EvdevSteamControllerV2>(
                triggerHappyMappings,
                new EvdevDeviceMatcher().WithHardwareIds(0x28DE, 0x1303)
            );

            // Steam Controller v2 puck (Proteus)
            EvdevLayoutFinder.RegisterLayout<EvdevSteamControllerV2>(
                gripMappings,
                new EvdevDeviceMatcher().WithHardwareIds(0x28DE, 0x1304)
            );
            EvdevLayoutFinder.RegisterLayout<EvdevSteamControllerV2>(
                triggerHappyMappings,
                new EvdevDeviceMatcher().WithHardwareIds(0x28DE, 0x1304)
            );

            // Steam Machine internal receiver (Nereid)
            EvdevLayoutFinder.RegisterLayout<EvdevSteamControllerV2>(
                gripMappings,
                new EvdevDeviceMatcher().WithHardwareIds(0x28DE, 0x1305)
            );
            EvdevLayoutFinder.RegisterLayout<EvdevSteamControllerV2>(
                triggerHappyMappings,
                new EvdevDeviceMatcher().WithHardwareIds(0x28DE, 0x1305)
            );
        }
    }
}
