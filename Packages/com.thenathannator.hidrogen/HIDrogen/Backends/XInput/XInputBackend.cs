#if UNITY_STANDALONE_WIN
using System;
using HIDrogen.Imports.Windows;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend
{
    internal class XInputQueueContext : IDisposable
    {
        public uint userIndex;

        public void Dispose() {}
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

    internal class XInputBackend : CustomInputBackend<XInputBackendDevice>
    {
        public const string InterfaceName = "XInput";
        public static readonly FourCC InputFormat = new FourCC('X', 'I', 'N', 'P');

        private const double kRefreshPeriod = 1.0;
        private double m_LastRefreshTime;

        private readonly XInputBackendDevice[] m_Devices = new XInputBackendDevice[XInput.MaxCount];
        private readonly bool[] m_BannedDevices = new bool[XInput.MaxCount];

        public XInputBackend()
        {
            CheckForNewDevices();
        }

        protected override void OnDispose()
        {
            foreach (var device in m_Devices)
                device?.Dispose();
        }

        protected override void OnUpdate()
        {
            double currentTime = InputState.currentTime;
            if (Math.Abs(currentTime - m_LastRefreshTime) >= kRefreshPeriod)
            {
                m_LastRefreshTime = currentTime;
                CheckForNewDevices();
            }
        }

        private void CheckForNewDevices()
        {
            for (uint i = 0; i < m_Devices.Length; i++)
            {
                if (m_Devices[i] != null)
                    continue;

                var result = XInput.Instance.GetCapabilities(i, XInputCapabilityRequest.Gamepad, out var capabilities);
                if (result != Win32Error.ERROR_SUCCESS)
                {
                    if (result == Win32Error.ERROR_DEVICE_NOT_CONNECTED)
                        m_BannedDevices[i] = false;
                    else
                        Logging.Error($"Failed to get capabilities for XInput user index {i}: 0x{(int)result:X8}");
                    continue;
                }

                // Ignore previously-ignored devices
                if (m_BannedDevices[i])
                    continue;

                if (capabilities.type != XInputDeviceType.Gamepad)
                {
                    Logging.Error($"Unrecognized XInput device type {capabilities.type} on user index {i}!");
                    m_BannedDevices[i] = true;
                    continue;
                }

                // Ignore gamepads, as those will already be handled by the native backend
                if (capabilities.subType == XInputDeviceSubType.Gamepad)
                {
                    m_BannedDevices[i] = true;
                    continue;
                }

                var description = new InputDeviceDescription()
                {
                    interfaceName = InterfaceName,
                    capabilities = JsonUtility.ToJson(new XInputDescriptionCapabilities()
                    {
                        userIndex = i,
                        type = capabilities.type,
                        subType = capabilities.subType,
                        flags = capabilities.flags,
                        gamepad = capabilities.gamepad,
                        vibration = capabilities.vibration,
                    }),
                };

                QueueDeviceAdd(description, new XInputQueueContext()
                {
                    userIndex = i
                });
            }
        }

        protected override XInputBackendDevice OnDeviceAdded(InputDevice device, IDisposable _context)
        {
            var context = (XInputQueueContext)_context;

            if (context.userIndex >= m_Devices.Length)
                throw new Exception($"Attempted to create device for invalid XInput user index {context.userIndex}!");
            if (m_Devices[context.userIndex] != null)
                throw new Exception($"Attempted to create duplicate device for XInput user index {context.userIndex}!");

            var backendDevice = new XInputBackendDevice(this, context.userIndex, device);
            m_Devices[context.userIndex] = backendDevice;
            return backendDevice;
        }

        protected override void OnDeviceRemoved(XInputBackendDevice device)
        {
            m_Devices[device.userIndex] = null;
            device.Dispose();
        }

        protected override unsafe long? OnDeviceCommand(XInputBackendDevice device, InputDeviceCommand* command)
        {
            // TODO
            // System commands
            // if (command->type == EnableDeviceCommand.Type)
            //     return Enable(command);
            // if (command->type == DisableDeviceCommand.Type)
            //     return Disable(command);
            // if (command->type == QueryEnabledStateCommand.Type)
            //     return IsEnabled(command);
            // if (command->type == RequestSyncCommand.Type)
            //     return SyncState(command);
            // if (command->type == RequestResetCommand.Type)
            //     return ResetState(command);
            // if (command->type == QueryCanRunInBackground.Type)
            //     return CanRunInBackground(command);

            // Reports
            if (command->type == DualMotorRumbleCommand.Type)
                return device.SetState((DualMotorRumbleCommand*)command);

            return null;
        }
    }
}
#endif