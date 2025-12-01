#if UNITY_STANDALONE_WIN
using System;
using System.Collections.Concurrent;
using SharpGameInput.v0;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend
{
    internal partial class GameInputBackend : CustomInputBackend<GameInputBackendDevice>
    {
        public const string InterfaceName = "GameInput";
        public static readonly FourCC InputFormat = new FourCC('G', 'I', 'P');
        public static readonly FourCC OutputFormat = new FourCC('G', 'I', 'P', 'O');

        private IGameInput m_GameInput;

        private readonly ConcurrentDictionary<IGameInputDevice, GameInputBackendDevice> m_DevicesByInstance
            = new ConcurrentDictionary<IGameInputDevice, GameInputBackendDevice>();

        private GameInputCallbackToken m_DeviceCallbackToken;

        public GameInputBackend()
        {
            if (!GameInput.Create(out m_GameInput, out int result))
                throw new Exception($"Failed to create GameInput instance: 0x{result:X8}");
        }

        protected override void OnDispose()
        {
            m_GameInput?.Dispose();
            m_GameInput = null;
        }

        protected override void OnStart()
        {
            if (!m_GameInput.RegisterDeviceCallback(
                null,
                GameInputKind.RawDeviceReport,
                GameInputDeviceStatus.Connected,
                GameInputEnumerationKind.AsyncEnumeration,
                null,
                OnGameInputDeviceStatusChange,
                out m_DeviceCallbackToken,
                out int result
            ))
            {
                throw new Exception($"Failed to register GameInput device callback: 0x{result:X8}");
            }
        }

        protected override void OnStop()
        {
            m_DeviceCallbackToken?.Unregister(1_000_000);
            m_DeviceCallbackToken = null;
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

            ref readonly var info = ref device.GetDeviceInfo();

            // We only cover Xbox One devices
            if (info.deviceFamily != GameInputDeviceFamily.XboxOne && info.deviceFamily != GameInputDeviceFamily.Virtual)
                return;

            // We only support devices with raw reports
            if ((info.supportedInput & GameInputKind.RawDeviceReport) == 0)
                return;

#if !HIDROGEN_TEST_PROJECT
            // Ignore gamepads, as those will already be handled
            if ((info.supportedInput & GameInputKind.Gamepad) != 0)
                return;
#endif

            var permaDevice = device.ToComPtr();
            if ((currentStatus & GameInputDeviceStatus.Connected) != 0)
            {
                var description = MakeDescription(permaDevice, info);
                // Device instance is duplicated here, as it is not retained by GameInput after the callback ends
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
                interfaceName = InterfaceName,
                serial = info.deviceId.ToString(),
                version = info.revisionNumber.ToString(),
                capabilities = JsonUtility.ToJson(capabilities)
            };

            if (info.displayName != null)
                description.product = info.displayName->ToString();

            return description;
        }

        protected override GameInputBackendDevice OnDeviceAdded(InputDevice device, IDisposable context)
        {
            var backendDevice = new GameInputBackendDevice(this, m_GameInput, (IGameInputDevice)context, device);
            // We don't use `context` as the key, as it is disposed after this callback
            m_DevicesByInstance.TryAdd(backendDevice.gipDevice, backendDevice);
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
            if (command->type == OutputFormat)
                return device.SendMessage(command->payloadPtr, command->payloadSizeInBytes);

            return null;
        }
    }
}
#endif
