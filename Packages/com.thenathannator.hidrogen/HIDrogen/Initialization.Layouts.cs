using HIDrogen.Controls;
using HIDrogen.Devices;
using HIDrogen.Devices.LowLevel;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static void RegisterLayouts()
        {
            // Layout finders
            EvdevLayoutFinder.Initialize();

            // Controls
            IntegerAxisControl.Initialize();
            IntegerButtonControl.Initialize();

            // Fallback device layouts
            EvdevJoystick.Initialize();
            EvdevGamepad.Initialize();

            // Steam
            EvdevSteamVirtualController.Initialize();
            EvdevSteamControllerV1.Initialize();
            EvdevSteamControllerV2.Initialize();
            EvdevSteamDeck.Initialize();
        }
    }
}
