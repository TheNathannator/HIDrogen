#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Collections.Concurrent;
using SharpGameInput;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend
{
    internal partial class GameInputBackend : CustomInputBackend<GameInputBackendDevice>
    {
        private IGameInput m_GameInput;

        private readonly ConcurrentDictionary<IGameInputDevice, GameInputBackendDevice> m_DevicesByInstance
            = new ConcurrentDictionary<IGameInputDevice, GameInputBackendDevice>();

        private GameInputCallbackToken m_DeviceCallbackToken;

        public GameInputBackend()
        {
            try
            {
                if (!GameInput.Create(out m_GameInput, out int result))
                    throw new Exception($"Failed to create GameInput instance: 0x{result:X8}");

                if (!m_GameInput.RegisterDeviceCallback(
                    null,
                    GameInputKind.RawDeviceReport,
                    GameInputDeviceStatus.Connected,
                    GameInputEnumerationKind.AsyncEnumeration,
                    null,
                    OnGameInputDeviceStatusChange,
                    out m_DeviceCallbackToken,
                    out result
                ))
                {
                    m_GameInput.Dispose();
                    throw new Exception($"Failed to register GameInput device callback: 0x{result:X8}");
                }
            }
            catch
            {
                m_DeviceCallbackToken?.TryUnregister(1_000_000);
                m_GameInput?.Dispose();
                throw;
            }
        }

        protected override void OnDispose()
        {
            m_DeviceCallbackToken?.Unregister(1_000_000);
            m_DeviceCallbackToken = null;

            foreach (var pair in m_DevicesByInstance)
            {
                pair.Key.Dispose();
                pair.Value.Dispose();
            }
            m_DevicesByInstance.Clear();

            m_GameInput?.Dispose();
            m_GameInput = null;
        }

        private void OnGameInputDeviceStatusChange(
            LightGameInputCallbackToken callbackToken,
            object context,
            LightIGameInputDevice device,
            ulong timestamp,
            GameInputDeviceStatus currentStatus,
            GameInputDeviceStatus previousStatus
        )
        {
            // Ignore if connection status hasn't changed
            if ((currentStatus & GameInputDeviceStatus.Connected) == (previousStatus & GameInputDeviceStatus.Connected))
                return;

            ref readonly var info = ref device.DeviceInfo;

            // We only cover Xbox One devices
            if (info.deviceFamily != GameInputDeviceFamily.XboxOne)
                return;

            // We only support devices with raw reports
            if ((info.supportedInput & GameInputKind.RawDeviceReport) == 0)
                return;

            // Ignore gamepads, as those will already be handled
            if ((info.supportedInput & GameInputKind.Gamepad) != 0)
                return;

            var permaDevice = device.ToComPtr();
            if ((currentStatus & GameInputDeviceStatus.Connected) != 0)
            {
                var description = MakeDescription(permaDevice, info);
                QueueDeviceAdd(description, permaDevice.Duplicate());
            }
            else
            {
                if (!m_DevicesByInstance.TryGetValue(permaDevice, out var backendDevice))
                    return;

                QueueDeviceRemove(backendDevice.device);
            }
        }

        private unsafe InputDeviceDescription MakeDescription(IGameInputDevice device, in GameInputDeviceInfo info)
        {
            var capabilities = new GameInputDeviceCapabilities()
            {
                vendorId = info.vendorId,
                productId = info.productId,
            };

            var description = new InputDeviceDescription()
            {
                interfaceName = GameInputDefinitions.InterfaceName,
                serial = info.deviceId.ToString(),
                version = info.revisionNumber.ToString(),
                capabilities = JsonUtility.ToJson(capabilities)
            };

            if (info.displayName != null)
                description.product = info.displayName->ToString();

            return description;
        }

        protected override GameInputBackendDevice OnDeviceAdded(InputDevice device, IDisposable _context)
        {
            var gipDevice = (IGameInputDevice)_context;

            var backendDevice = new GameInputBackendDevice(this, m_GameInput, gipDevice, device);
            m_DevicesByInstance.TryAdd(gipDevice, backendDevice);
            return backendDevice;
        }

        protected override void OnDeviceRemoved(GameInputBackendDevice device)
        {
            m_DevicesByInstance.TryRemove(device.gipDevice, out _);
            device.Dispose();
        }

        protected override unsafe long? OnDeviceCommand(GameInputBackendDevice device, InputDeviceCommand* command)
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
            if (command->type == GameInputDefinitions.OutputFormat)
                return (long)device.SendMessage(command->payloadPtr, command->payloadSizeInBytes);

            return null;
        }
    }
}
#endif
