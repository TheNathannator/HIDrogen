using System;
using System.Threading;
using HIDrogen.Imports;
using HIDrogen.LowLevel;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using System.Runtime.InteropServices;
using System.Threading;
using System.Buffers.Binary;

namespace HIDrogen.Backend
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XInputVibration
    {
        public ushort leftMotor;
        public ushort rightMotor;
    }

    internal enum XInputDeviceType : byte
    {
        Gamepad = 1,
    }

    internal enum XInputDeviceSubType : byte
    {
        Unknown = 0,
        Gamepad = 1,
        Wheel = 2,
        ArcadeStick = 3,
        FlightStick = 4,
        DancePad = 5,
        Guitar = 6,
        GuitarAlternate = 7,
        DrumKit = 8,
        StageKit = 9,
        GuitarBass = 11,
        ProKeyboard = 15,
        ArcadePad = 19,
        DJTurntable = 23,
        ProGuitar = 25,
    }

    [Flags]
    internal enum XInputDeviceFlags : ushort
    {
        ForceFeedback = 0x01,
        Wireless = 0x02,
        Voice = 0x04,
        PluginModules = 0x08,
        NoNavigation = 0x10,
    }

    [Flags]
    internal enum XInputButton : ushort
    {
        DpadUp = 0x0001,
        DpadDown = 0x0002,
        DpadLeft = 0x0004,
        DpadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        Guide = 0x0400,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000
    }

    internal enum LEDState : uint
    {
        LED_OFF = 0,
        LED_BLINK = 1,
        LED_1_SWITCH_BLINK = 2,
        LED_2_SWITCH_BLINK = 3,
        LED_3_SWITCH_BLINK = 4,
        LED_4_SWITCH_BLINK = 5,
        LED_1 = 6,
        LED_2 = 7,
        LED_3 = 8,
        LED_4 = 9,
        LED_CYCLE = 10,
        LED_FAST_BLINK = 11,
        LED_SLOW_BLINK = 12,
        LED_FLIPFLOP = 13,
        LED_ALLBLINK = 14,
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XInputGamepad : IInputStateTypeInfo
    {
        public static readonly FourCC Format = new FourCC('X', 'I', 'N', 'P');
        public FourCC format => Format;

        public XInputButton buttons;
        public byte leftTrigger;
        public byte rightTrigger;
        public short leftStickX;
        public short leftStickY;
        public short rightStickX;
        public short rightStickY;
    }

    [Serializable]
    internal struct XInputDescriptionCapabilities
    {
        public uint userIndex;
        public XInputDeviceType type;
        public XInputDeviceSubType subType;
        public XInputDeviceFlags flags;
        public XInputGamepad gamepad;
        public XInputVibration vibration;
    }

    internal class X360Controller : IDisposable
    {
        private readonly X360Receiver m_Backend;
        public const string InterfaceName = "XInput";

        private LibUSBDevice _receiver;
        public InputDevice device { get; set; }
        private Thread m_interruptThread;

        public uint _userIndex { get; }
        private bool connected = false;
        private int interfaceIndex;
        private uint controlEndpoint;

        public X360Controller(X360Receiver backend, LibUSBDevice receiver, uint userIndex)
        {
            m_Backend = backend;
            _receiver = receiver;
            _userIndex = userIndex;

            // The receiver has 8 endpoints,
            // E1: P1 Controller, E2: P1 Voice,
            // E3: P2 Controller, E4: P2 Voice,
            // E5: P3 Controller, E6: P3 Voice,
            // E7: P4 Controller, E8: P4 Voice,
            // for a total of 4 players. 
            interfaceIndex = (int)userIndex * 2;
            controlEndpoint = (uint)interfaceIndex + 1;

            // Claim the control interface.
            receiver.ClaimInterface(interfaceIndex);

            // Create a new thread to wait for and handle input.
            m_interruptThread = new Thread(WaitForInterrupt);
            m_interruptThread.Start();

            // Controllers will automatically send a link control packet
            // when first connected to the receiver. We will now request another
            // in case this class was not listening when that happened.
            SendInquiry();
        }

        public void WaitForInterrupt()
        {
            var payload = new byte[32];

            int consecutiveFailures = 0;

            while (consecutiveFailures < 3)
            {
                // This will block until data is ready.
                var result = _receiver.InterruptTranferIn(controlEndpoint, payload);

                if (result == LibUSBError.ERROR_NO_DEVICE)
                {
                    break;
                }

                if (result != LibUSBError.SUCCESS)
                {
                    consecutiveFailures++;
                    continue;
                }

                // Loop over the first two bytes of the incoming payload.
                switch ((payload[0] << 8) | payload[1])
                {
                    case 0x0001: { 
                        // Device state update.
                        HIDUpdate(payload);
                        break;
                    }

                    case 0x000f: {
                        // The link control packet.
                        AddDevice(payload);
                        connected = true;
                        break;
                    }

                    case 0x0800: {
                        // Controller disconnection
                        if (connected)
                        {
                            m_Backend.QueueDeviceRemove(device);
                            connected = false;
                        }
                        break;
                    }

                    default: {
                        // Many more report types exist that might be useful
                        // to handle (such as battery events)
                        // but we'll ignore all of the them for now.
                        break;
                    }
                }
            }
        }

        public void AddDevice(byte[] bytes)
        {
            // For some reason, my TBRB drumkit and TBRB Hofner Bass
            // report subType values 0x80 higher than expected.
            // TODO: figure out why
            var subType = (XInputDeviceSubType)(bytes[25] & 0x7F);

            var description = new InputDeviceDescription()
            {
                interfaceName = InterfaceName,
                capabilities = JsonUtility.ToJson(new XInputDescriptionCapabilities()
                {
                    userIndex = _userIndex,
                    type = XInputDeviceType.Gamepad,
                    subType = subType,
                    flags = XInputDeviceFlags.Wireless | XInputDeviceFlags.Voice,
                    gamepad = new XInputGamepad(),
                    vibration = new XInputVibration()
                }),
            };

            m_Backend.QueueDeviceAdd(description, new USBQueueContext()
            {
                userIndex = _userIndex
            });

            switch (_userIndex)
            {
                case 0: {
                    SetLED(LEDState.LED_1);
                    break;
                }
                case 1: {
                    SetLED(LEDState.LED_2);
                    break;
                }
                case 2: {
                    SetLED(LEDState.LED_3);
                    break;
                }
                case 3: {
                    SetLED(LEDState.LED_4);
                    break;
                }
            }
        }

        public void SendInquiry()
        {
            var payload = new byte[12];
            payload[0] = 8;
            payload[2] = 0x0f;
            payload[3] = 0xc0;

            _receiver.InterruptTranferOut(controlEndpoint, payload);
        }

        public void PowerDown()
        {
            var payload = new byte[12];
            payload[2] = 8;
            payload[3] = 0xc0;
            _receiver.InterruptTranferOut(controlEndpoint, payload);
        }

        public void SetLED(LEDState ledState)
        {
            var payload = new byte[12];
            payload[2] = 8;
            payload[3] = (byte)((uint)ledState + 0x40);
            _receiver.InterruptTranferOut(controlEndpoint, payload);
        }

        public void SetRumble(byte highFreq, byte lowFreq)
        {
            var payload = new byte[12];
            payload[1] = 1;
            payload[2] = 0x0f;
            payload[3] = 0xc0;
            payload[5] = highFreq;
            payload[6] = lowFreq;

            _receiver.InterruptTranferOut(controlEndpoint, payload);
        }

        public void Dispose()
        {
            if (connected)
            {
                PowerDown();
                m_Backend.QueueDeviceRemove(device);
            }

            _receiver.ReleaseInterface(interfaceIndex);
            connected = false;
        }

        public void HIDUpdate(byte[] bytes)
        {
            XInputGamepad gamepadState;
            gamepadState.buttons = (XInputButton)BitConverter.ToUInt16(bytes, 6);
            gamepadState.leftTrigger = bytes[8]; 
            gamepadState.rightTrigger = bytes[9]; 
            gamepadState.leftStickX = (short)BitConverter.ToInt16(bytes, 10);
            gamepadState.leftStickY = (short)BitConverter.ToInt16(bytes, 12);
            gamepadState.rightStickX = (short)BitConverter.ToInt16(bytes, 14);
            gamepadState.rightStickY = (short)BitConverter.ToInt16(bytes, 16);

            // TODO: find a place for Ext bytes: 18,19,20,21,22,23

            m_Backend.QueueStateEvent(device, ref gamepadState);
        }
    }
}