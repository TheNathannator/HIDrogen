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

            // Devices
            EvdevJoystick.Initialize();
            EvdevGamepad.Initialize();
        }
    }
}
