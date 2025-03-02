using System;
using HIDrogen.Imports;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Buffers.Binary;

// The Xbox360 Wireless Receiver is a 'Vendor Specific' (0xFF) class device,
// so will not be recognised by unity natively.

namespace HIDrogen.Backend
{
    internal class USBQueueContext : IDisposable
    {
        public uint userIndex;

        public void Dispose() {}
    }

    internal class X360Receiver : CustomInputBackend<X360Controller>
    {
        // Each wireless receiver supports up to 4 controllers.
        private readonly X360Controller[] m_Controllers = new X360Controller[4];

        private LibUSBDevice _usbDevice;

        public X360Receiver(LibUSBDevice usbDevice)
        {
            Logging.Verbose("[X360Receiver] init");

            usbDevice.Open();

            for (uint i = 0; i < 4; i++) 
            {
                m_Controllers[i] = new X360Controller(this, usbDevice, i);
            }

            _usbDevice = usbDevice;
        }

        protected override void OnDispose()
        {
            Logging.Verbose("[X360Receiver] OnDispose");

            for (uint i = 0; i < 4; i++) 
            {
                m_Controllers[i]?.Dispose();
                m_Controllers[i] = null;
            }

            _usbDevice.Dispose();
        }

        public void Dispose()
        {
            OnDispose();
        }

        protected override X360Controller OnDeviceAdded(InputDevice device, IDisposable _context)
        {
            Logging.Verbose("[X360Receiver] OnDeviceAdded");

            var context = (USBQueueContext)_context;

            m_Controllers[context.userIndex].device = device;

            return m_Controllers[context.userIndex];
        }

        protected override void OnDeviceRemoved(X360Controller device)
        {
            Logging.Verbose("[X360Receiver] OnDeviceRemoved");
        }

        protected override unsafe long? OnDeviceCommand(X360Controller device, InputDeviceCommand* command)
        {
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

            // TODO: Untested
            if (command->type == DualMotorRumbleCommand.Type)
            {
                var cmd = (DualMotorRumbleCommand*)command;
                var lowFreq = (byte)(Math.Clamp(cmd->lowFrequencyMotorSpeed, 0f, 1f) * 255f);
                var highFreq = (byte)(Math.Clamp(cmd->highFrequencyMotorSpeed, 0f, 1f) * 255f);
                device.SetRumble(highFreq, lowFreq);
                return InputDeviceCommand.GenericSuccess;
            }

            return null;
        }
    }
}