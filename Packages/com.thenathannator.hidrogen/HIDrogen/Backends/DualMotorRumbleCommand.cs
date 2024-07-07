using System.Runtime.InteropServices;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend
{
    // Copied from the input system
    // dunno why this isn't public lol
    [StructLayout(LayoutKind.Explicit, Size = kSize)]
    internal struct DualMotorRumbleCommand : IInputDeviceCommandInfo
    {
        public static FourCC Type => new FourCC('R', 'M', 'B', 'L');
        public FourCC typeStatic => Type;

        internal const int kSize = InputDeviceCommand.BaseCommandSize + sizeof(float) * 2;

        [FieldOffset(0)]
        public InputDeviceCommand baseCommand;

        [FieldOffset(InputDeviceCommand.BaseCommandSize)]
        public float lowFrequencyMotorSpeed;

        [FieldOffset(InputDeviceCommand.BaseCommandSize + sizeof(float))]
        public float highFrequencyMotorSpeed;

        public static DualMotorRumbleCommand Create(float lowFrequency, float highFrequency)
            => new DualMotorRumbleCommand()
        {
            baseCommand = new InputDeviceCommand(Type, kSize),
            lowFrequencyMotorSpeed = lowFrequency,
            highFrequencyMotorSpeed = highFrequency
        };
    }
}
