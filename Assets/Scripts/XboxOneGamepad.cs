using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.TestProject
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XboxOneGamepadState : IInputStateTypeInfo
    {
        public FourCC format => GameInputDefinitions.InputFormat;

        public byte reportId;

        [InputControl(name = "start", layout = "Button", format = "BIT", bit = 2, displayName = "Menu")]
        [InputControl(name = "select", layout = "Button", format = "BIT", bit = 3, displayName = "View")]

        [InputControl(name = "buttonSouth", layout = "Button", format = "BIT", bit = 4, displayName = "A")]
        [InputControl(name = "buttonEast", layout = "Button", format = "BIT", bit = 5, displayName = "B")]
        [InputControl(name = "buttonWest", layout = "Button", format = "BIT", bit = 6, displayName = "X")]
        [InputControl(name = "buttonNorth", layout = "Button", format = "BIT", bit = 7, displayName = "Y")]

        [InputControl(name = "dpad", layout = "Dpad", format = "BIT", sizeInBits = 4, bit = 8)]
        [InputControl(name = "dpad/up", bit = 8)]
        [InputControl(name = "dpad/down", bit = 9)]
        [InputControl(name = "dpad/left", bit = 10)]
        [InputControl(name = "dpad/right", bit = 11)]

        [InputControl(name = "leftShoulder", layout = "Button", format = "BIT", bit = 12, displayName = "Left Shoulder")]
        [InputControl(name = "rightShoulder", layout = "Button", format = "BIT", bit = 13, displayName = "Right Shoulder")]
        [InputControl(name = "leftStickPress", layout = "Button", format = "BIT", bit = 14, displayName = "Left Stick Press")]
        [InputControl(name = "rightStickPress", layout = "Button", format = "BIT", bit = 15, displayName = "Right Stick Press")]
        public ushort buttons;

        [InputControl(name = "leftTrigger", layout = "Button", format = "BIT", sizeInBits = 10, displayName = "Left Trigger")]
        public ushort leftTrigger;

        [InputControl(name = "rightTrigger", layout = "Button", format = "BIT", sizeInBits = 10, displayName = "Right Trigger")]
        public ushort rightTrigger;

        [InputControl(name = "leftStick", layout = "Stick", format = "VC2S", displayName = "Left Stick")]
        [InputControl(name = "leftStick/x", format = "SHRT", parameters = "clamp=false,invert=false,normalize=false")]
        [InputControl(name = "leftStick/left", format = "SHRT")]
        [InputControl(name = "leftStick/right", format = "SHRT")]

        // These must be up here, otherwise a stack overflow occurs while the layout is being constructed
        [InputControl(name = "leftStick/y", format = "SHRT", offset = 2, parameters = "clamp=false,invert=false,normalize=false")]
        [InputControl(name = "leftStick/up", format = "SHRT", offset = 2)]
        [InputControl(name = "leftStick/down", format = "SHRT", offset = 2)]
        public short leftStickX;
        public short leftStickY;

        [InputControl(name = "rightStick", layout = "Stick", format = "VC2S", displayName = "Right Stick")]
        [InputControl(name = "rightStick/x", format = "SHRT", parameters = "clamp=false,invert=false,normalize=false")]
        [InputControl(name = "rightStick/left", format = "SHRT")]
        [InputControl(name = "rightStick/right", format = "SHRT")]

        // These must be up here, otherwise a stack overflow occurs while the layout is being constructed
        [InputControl(name = "rightStick/y", format = "SHRT", offset = 2, parameters = "clamp=false,invert=false,normalize=false")]
        [InputControl(name = "rightStick/up", format = "SHRT", offset = 2)]
        [InputControl(name = "rightStick/down", format = "SHRT", offset = 2)]
        public short rightStickX;
        public short rightStickY;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = kSize)]
    internal struct XboxOneGamepadVibration : IInputDeviceCommandInfo
    {
        public enum Flags : byte
        {
            RightRumble = 0x01,
            LeftRumble = 0x02,
            RightTrigger = 0x04,
            LeftTrigger = 0x08,
        }

        private const byte kReportId = 0x09;

        internal const int kSize = InputDeviceCommand.BaseCommandSize + sizeof(byte) * 10;

        public FourCC typeStatic => GameInputDefinitions.OutputFormat;

        public InputDeviceCommand baseCommand;
        private byte m_ReportId;

        private byte unknown;
        public Flags flags;

        public byte leftTrigger;
        public byte rightTrigger;
        public byte leftRumble;
        public byte rightRumble;

        public byte duration;
        public byte delay;
        public byte repeat;

        public XboxOneGamepadVibration(byte leftRumble, byte rightRumble)
        {
            baseCommand = new InputDeviceCommand(GameInputDefinitions.OutputFormat, kSize);
            m_ReportId = kReportId;

            unknown = 0;
            flags = Flags.LeftRumble | Flags.RightRumble;

            this.leftRumble = leftRumble;
            this.rightRumble = rightRumble;
            leftTrigger = 0;
            rightTrigger = 0;

            duration = 0xFF;
            delay = 0;
            repeat = 0xEB;
        }

        public XboxOneGamepadVibration(byte leftRumble, byte rightRumble, byte leftTrigger, byte rightTrigger)
        {
            baseCommand = new InputDeviceCommand(GameInputDefinitions.OutputFormat, kSize);
            m_ReportId = kReportId;

            unknown = 0;
            flags = Flags.LeftRumble | Flags.RightRumble | Flags.LeftTrigger | Flags.RightTrigger;

            this.leftRumble = leftRumble;
            this.rightRumble = rightRumble;
            this.leftTrigger = leftTrigger;
            this.rightTrigger = rightTrigger;

            duration = 0xFF;
            delay = 0;
            repeat = 0xEB;
        }
    }

    [InputControlLayout(stateType = typeof(XboxOneGamepadState))]
    internal class XboxOneGamepad : Gamepad, IInputStateCallbackReceiver
    {
        /// <summary>
        /// The current <see cref="XboxOneGamepad"/>.
        /// </summary>
        public static new XboxOneGamepad current { get; private set; }

        /// <summary>
        /// A collection of all <see cref="XboxOneGamepad"/>s currently connected to the system.
        /// </summary>
        public new static IReadOnlyList<XboxOneGamepad> all => s_AllDevices;
        private static readonly List<XboxOneGamepad> s_AllDevices = new List<XboxOneGamepad>();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        internal static void Initialize()
        {
            // IDs cross-referenced from xinputhid.inf and
            // https://github.com/libsdl-org/SDL/blob/b6e6c73541867733985edc7edd776c64fc89a64a/src/joystick/usb_ids.h#L132-L141
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x02D1);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x02DD);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x02E0);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x02E3);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x02EA);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x02FD);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x02FF);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x0B00);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x0B05);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x0B12);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x0B13);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x0B20);
            GameInputLayoutFinder.RegisterLayout<XboxOneGamepad>(0x045E, 0x0B22);
        }

        void IInputStateCallbackReceiver.OnNextUpdate() {}

        unsafe void IInputStateCallbackReceiver.OnStateEvent(InputEventPtr eventPtr)
        {
            var stateEvent = StateEvent.From(eventPtr);
            if (stateEvent->stateFormat != GameInputDefinitions.InputFormat ||
                stateEvent->stateSizeInBytes < sizeof(XboxOneGamepadState))
                return;

            XboxOneGamepadState* state = (XboxOneGamepadState*)stateEvent->state;
            if (state->reportId != 0x20)
                return;

            InputState.Change(this, eventPtr);
        }

        bool IInputStateCallbackReceiver.GetStateOffsetForEvent(InputControl control, InputEventPtr eventPtr, ref uint offset)
        {
            offset = 0;
            return true;
        }

        /// <inheritdoc/>
        public override void MakeCurrent()
        {
            base.MakeCurrent();
            current = this;
        }

        protected override void OnAdded()
        {
            base.OnAdded();
            s_AllDevices.Add(this);
        }

        protected override void OnRemoved()
        {
            base.OnRemoved();
            s_AllDevices.Remove(this);
            if (current == this)
                current = null;
        }
    }
}