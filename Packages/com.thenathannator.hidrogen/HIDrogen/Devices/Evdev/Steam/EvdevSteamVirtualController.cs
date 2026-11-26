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
    /// The virtual gamepad created by Steam Input.
    /// </summary>
    [InputControlLayout(hideInUI = true)]
    internal class EvdevSteamVirtualController : Gamepad
    {
        internal static void Initialize()
        {
            EvdevLayoutFinder.RegisterLayout<EvdevSteamVirtualController>(
                new EvdevDeviceMapping()
                    .WithKey(input_key_code.BTN_START, (StartButton, DisplayStart))
                    .WithKey(input_key_code.BTN_SELECT, (SelectButton, DisplaySelect))
                    .WithKey(input_key_code.BTN_MODE, (SystemButton, DisplaySystem))

                    .WithKey(input_key_code_alt.BTN_SOUTH, (ButtonSouth, DisplayA))
                    .WithKey(input_key_code_alt.BTN_EAST, (ButtonEast, DisplayB))
                    .WithKey(input_key_code_alt.BTN_WEST, (ButtonWest, DisplayX))
                    .WithKey(input_key_code_alt.BTN_NORTH, (ButtonNorth, DisplayY))

                    .WithHat(0, (Dpad, DisplayDpad))

                    .WithKey(input_key_code.BTN_TL, (LeftShoulder, DisplayL1))
                    .WithKey(input_key_code.BTN_TR, (RightShoulder, DisplayR1))

                    .WithAbsButton(input_abs_code.ABS_Z, (LeftTrigger, DisplayL2))
                    .WithAbsButton(input_abs_code.ABS_RZ, (RightTrigger, DisplayR2))

                    .WithStick(input_abs_code.ABS_X, (LeftStick, DisplayLeftStick))
                    .WithStick(input_abs_code.ABS_RX, (RightStick, DisplayRightStick))
                    .WithKey(input_key_code.BTN_THUMB_L, (LeftStickPress, DisplayL3))
                    .WithKey(input_key_code.BTN_THUMB_R, (RightStickPress, DisplayR3)),

                new EvdevDeviceMatcher()
                    .WithHardwareIds(0x28DE, 0x11FF)
            );
        }
    }
}
