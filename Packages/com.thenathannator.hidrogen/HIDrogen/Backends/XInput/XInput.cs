#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && UNITY_2022_2_OR_NEWER
using System;
using System.Runtime.InteropServices;
using HIDrogen.Backend;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Imports
{
    using static Win32Error;
    using static Kernel32;

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

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XInputGamepad : IInputStateTypeInfo
    {
        public readonly FourCC format => XInputBackend.InputFormat;

        public XInputButton buttons;
        public byte leftTrigger;
        public byte rightTrigger;
        public short leftStickX;
        public short leftStickY;
        public short rightStickX;
        public short rightStickY;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XInputState
    {
        public uint packetNumber;
        public XInputGamepad gamepad;
    }

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

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct XInputCapabilities
    {
        public XInputDeviceType type;
        public XInputDeviceSubType subType;
        public XInputDeviceFlags flags;
        public XInputGamepad gamepad;
        public XInputVibration vibration;
    }

    internal enum XInputCapabilityRequest : uint
    {
        Gamepad = 1,
    }

    internal class XInput : SafeHandle
    {
        public const uint MaxCount = 4;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate Win32Error XInputGetState(
            uint UserIndex,
            out XInputState State
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate Win32Error XInputSetState(
            uint UserIndex,
            in XInputVibration Vibration
        );

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate Win32Error XInputGetCapabilities(
            uint UserIndex,
            XInputCapabilityRequest Flags,
            out XInputCapabilities Capabilities
        );

        public override bool IsInvalid => handle == IntPtr.Zero;

        private XInputGetState m_GetState;
        private XInputSetState m_SetState;
        private XInputGetCapabilities m_GetCapabilities;

        public XInput()
            : base(IntPtr.Zero, true)
        {
            var libHandle = LoadLibrary("xinput1_4.dll");
            if (libHandle == IntPtr.Zero)
            {
                libHandle = LoadLibrary("xinput1_3.dll");
                if (libHandle == IntPtr.Zero)
                {
                    // We don't attempt for xinput9_1_0.dll here, as that will only report devices as gamepads
                    throw new Exception("Failed to load XInput!");
                }
            }

            static void GetExport<T>(IntPtr lib, ref T field, string name)
                where T : Delegate
            {
                var proc = GetProcAddress(lib, name);
                if (proc == IntPtr.Zero)
                {
                    FreeLibrary(lib);
                    throw new Exception($"Failed to load export {name}!");
                }

                field = Marshal.GetDelegateForFunctionPointer<T>(proc);
            }

            GetExport(libHandle, ref m_GetState, nameof(XInputGetState));
            GetExport(libHandle, ref m_SetState, nameof(XInputSetState));
            GetExport(libHandle, ref m_GetCapabilities, nameof(XInputGetCapabilities));

            SetHandle(libHandle);
        }

        protected override bool ReleaseHandle()
        {
            m_GetState = null;
            m_SetState = null;
            m_GetCapabilities = null;
            return FreeLibrary(handle);
        }

        public Win32Error GetState(
            uint UserIndex,
            out XInputState State
        )
        {
            var getState = m_GetState;
            if (getState != null)
                return getState(UserIndex, out State);

            State = default;
            return ERROR_DEVICE_NOT_CONNECTED;
        }

        public Win32Error SetState(
            uint UserIndex,
            in XInputVibration Vibration
        )
        {
            var setState = m_SetState;
            if (setState != null)
                return setState(UserIndex, Vibration);

            return ERROR_DEVICE_NOT_CONNECTED;
        }

        public Win32Error GetCapabilities(
            uint UserIndex,
            XInputCapabilityRequest Flags,
            out XInputCapabilities Capabilities
        )
        {
            var getCapabilities = m_GetCapabilities;
            if (getCapabilities != null)
                return getCapabilities(UserIndex, Flags, out Capabilities);

            Capabilities = default;
            return ERROR_DEVICE_NOT_CONNECTED;
        }
    }
}
#endif