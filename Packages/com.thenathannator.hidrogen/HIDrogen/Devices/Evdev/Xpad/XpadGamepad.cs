using HIDrogen.Devices.LowLevel;
using HIDrogen.Imports.Linux;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace HIDrogen.Devices
{
    // Not platform guarded

    using static ControlNames;
    using static DisplayNames;

    [InputControlLayout(hideInUI = true, displayName = "Xbox 360 Controller")]
    internal sealed class XpadGamepad : Gamepad
    {
        internal static void Initialize()
        {
            var mappings = new EvdevDeviceMapping()
                .WithKey(input_key_code.BTN_START, (StartButton, DisplayMenu))
                .WithKey(input_key_code.BTN_SELECT, (SelectButton, DisplayView))
                .WithKey(input_key_code.BTN_MODE, (SystemButton, DisplayGuide))

                .WithKey(input_key_code_alt.BTN_A, (ButtonSouth, DisplayA))
                .WithKey(input_key_code_alt.BTN_B, (ButtonEast, DisplayB))
                .WithKey(input_key_code_alt.BTN_X, (ButtonWest, DisplayX))
                .WithKey(input_key_code_alt.BTN_Y, (ButtonNorth, DisplayY))

                .WithHat(0, Dpad)

                .WithKey(input_key_code.BTN_TL, (LeftShoulder, DisplayLeftBumper))
                .WithKey(input_key_code.BTN_TR, (RightShoulder, DisplayRightBumper))
                .WithAbsButton(input_abs_code.ABS_Z, (LeftTrigger, DisplayLeftTrigger))
                .WithAbsButton(input_abs_code.ABS_RZ, (RightTrigger, DisplayRightTrigger))

                .WithStick(input_abs_code.ABS_X, (LeftStick, DisplayLeftStick))
                .WithStick(input_abs_code.ABS_RX, (RightStick, DisplayRightStick))
                .WithKey(input_key_code.BTN_THUMB_L, (LeftStickPress, DisplayLeftStickPress))
                .WithKey(input_key_code.BTN_THUMB_R, (RightStickPress, DisplayRightStickPress))

                // xpad reports individual d-pad directions under TRIGGER_HAPPY,
                // regardless of if the regular inputs are mapped as hat axes
                .WithIgnoredKey(input_key_code.BTN_TRIGGER_HAPPY_1)
                .WithIgnoredKey(input_key_code.BTN_TRIGGER_HAPPY_2)
                .WithIgnoredKey(input_key_code.BTN_TRIGGER_HAPPY_3)
                .WithIgnoredKey(input_key_code.BTN_TRIGGER_HAPPY_4);

            EvdevLayoutFinder.RegisterLayout<XpadGamepad>(
                mappings,
                new EvdevDeviceMatcher()
                    .WithDriver(EvdevDefinitions.DriverXpad)
            );
        }
    }
}
