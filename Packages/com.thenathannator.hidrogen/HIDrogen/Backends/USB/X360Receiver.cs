using System;
using HIDrogen.LowLevel;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using LibUSBWrapper;

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

        private LibUSBWrapper.LibUSBDevice _usbDevice;

        public X360Receiver(LibUSBWrapper.LibUSBDevice usbDevice)
        {
            Debug.Log("[X360Receiver] init");

            for (uint i = 0; i < 4; i++) 
            {
                m_Controllers[i] = new X360Controller(this, usbDevice, i);
            }

            _usbDevice = usbDevice;
        }

        protected override void OnDispose()
        {
            Debug.Log("[X360Receiver] OnDispose");

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
            Debug.Log("[X360Receiver] OnDeviceAdded");

            var context = (USBQueueContext)_context;

            m_Controllers[context.userIndex].device = device;

            return m_Controllers[context.userIndex];
        }

        protected override void OnDeviceRemoved(X360Controller device)
        {
            Debug.Log("[X360Receiver] OnDeviceRemoved");
        }

        protected override unsafe long? OnDeviceCommand(X360Controller device, InputDeviceCommand* command)
        {
            Debug.Log($"[X360Receiver] OnDeviceCommand {command->type}");

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
                var lowFreq = BitConverter.GetBytes(cmd->lowFrequencyMotorSpeed)[0];
                var highFreq = BitConverter.GetBytes(cmd->highFrequencyMotorSpeed)[0];
                device.SetRumble(highFreq, lowFreq);
                return null;
            }

            return null;
        }
    }
}