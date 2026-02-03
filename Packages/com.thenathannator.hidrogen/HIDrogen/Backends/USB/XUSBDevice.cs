using System;
using System.Collections.Generic;
using System.Threading;
using HIDrogen.Imports;
using HIDrogen.Imports.Windows;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend
{
    using static libusb;

    internal class XUSBController : IDisposable
    {

        private const int kMaxInputLength = 32;
        private const int kMaxOutputLength = 32;

        private const int kOutputReportLength = 12;

        private const byte kOutputControllerRequest = 0;

        private readonly byte m_InEndpoint;
        private readonly byte m_OutEndpoint;
        private readonly int m_IfIndex;

        private readonly uint m_ControllerIndex;

        private libusb_device_handle m_Handle;

        private XUSBBackend m_Backend;
        private InputDevice m_Device;

        private Thread m_Thread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);
        

        public XUSBController(libusb_device_handle handle, uint controllerIndex, int ifIndex, byte inEndpoint, byte outEndpoint, XUSBBackend backend, byte subType)
        {
            m_InEndpoint = inEndpoint;
            m_OutEndpoint = outEndpoint;
            m_IfIndex = ifIndex;
            m_Handle = handle;
            m_Backend = backend;
            m_ControllerIndex = controllerIndex;

            Logging.Verbose("claimed");
            Logging.Verbose($"controllerIndex {controllerIndex}");

           var cap = new XInputCapabilities()
            {
                type = XInputDeviceType.Gamepad,
                subType = (XInputDeviceSubType)subType,
                flags = XInputDeviceFlags.Wireless,
                gamepad = new XInputGamepad()
                {
                    buttons = (XInputButton)0xFFFF,
                    leftTrigger = 0xFF,
                    rightTrigger = 0xFF,
                    leftStickX = unchecked((short)0xFFC0),
                    leftStickY = unchecked((short)0xFFC0),
                    rightStickX = unchecked((short)0xFFC0),
                    rightStickY = unchecked((short)0xFFC0),
                },
                vibration = new XInputVibration()
                {
                    leftMotor = 0xFF,
                    rightMotor = 0xFF,
                },
            };

            var description = new InputDeviceDescription()
            {
                interfaceName = XInputBackend.InterfaceName,
                // User index is not included since this backend will not match up with XInput
                capabilities = JsonUtility.ToJson(cap),
            };

            m_Backend.QueueDeviceAdd(description, new USBQueueContext()
            {
                controllerIndex = controllerIndex
            });
   
            // Create a new thread to wait for and handle input.
            m_Thread = new Thread(PollThread) { IsBackground = true };
            m_Thread.Start();
        }

        public void SetDevice(InputDevice device)
        {
            m_Device = device;

            switch (m_ControllerIndex)
            {
                case 0: SetLED(X360LedState.Player1Blink); break;
                case 1: SetLED(X360LedState.Player2Blink); break;
                case 2: SetLED(X360LedState.Player3Blink); break;
                case 3: SetLED(X360LedState.Player4Blink); break;
            }
        }

        private void PollThread()
        {
            var payload = new byte[kMaxInputLength];

            const int retryThreshold = 3;
            int errorCount = 0;

            while (!m_ThreadStop.WaitOne(0))
            {
                // Read incoming reports
                var result = libusb_interrupt_transfer(m_Handle, m_InEndpoint, payload, out int actualLength, 100);
                switch (result)
                {
                    case libusb_error.SUCCESS:
                    {
                        break;
                    }
                    case libusb_error.TIMEOUT:
                    {
                        continue;
                    }
                    case libusb_error.NO_DEVICE:
                    {
                        return;
                    }

                    default:
                    {
                        errorCount++;
                        if (errorCount >= retryThreshold)
                        {
                            libusb_logerror(result, "Failed to receive XUSB transfer");
                            return;
                        }

                        else
                        {
                            libusb_logerror(result, $"Failed to receive XUSB transfer (attempt {errorCount})");
                        }
                        continue;
                    }
                }

                errorCount = 0;
                if (actualLength < 1)
                {
                    continue;
                }

                ProcessControllerReport(payload);
            }
        }

        private void ProcessControllerReport(byte[] payload)
        {
            byte reportType = payload[0];
            switch (reportType)
            {
                // 0x00
                // Default game pad data input report
                case 0x00:
                {
                    ProcessControllerInputReport(payload);
                    break;
                }

                // LED state report
                case 0x01:
                {
                    break;
                }

                // Motor operation mode report
                case 0x02:
                {
                    break;
                }
                
                // Current rumble level settings report
                case 0x03:
                {
                    break;
                }

                // Battery charge state report
                case 0x04:
                {
                    break;
                }

                // Device connection report
                case 0x08:
                {
                    break;
                }

                default:
                {
                    Logging.Verbose($"Unrecognized X360 controller report type {reportType}");
                    break;
                }
            }
        }

        private void ProcessControllerInputReport(byte[] payload)
        {
            if (m_Device != null)
            {
                // string hexString = BitConverter.ToString(payload).Replace("-", string.Empty);
                // Logging.Verbose($"data: {hexString}");
            
                m_Backend.QueueStateEvent(m_Device, XInputGamepad.Format, payload, 2, 18);
            }
        }

        private unsafe void SendOutputRequest(byte[] payload, int payloadLength)
        {
            var result = libusb_interrupt_transfer(m_Handle, m_OutEndpoint, payload, out _, 5);
            libusb_checkerror(result, "Failed to send XUSB output request");
        }

        // private unsafe void WriteOutputHeader(byte* payload, int payloadLength, byte requestId, byte requestField)
        // {
        //     // safety check
        //     if (payloadLength < 4)
        //     {
        //         throw new ArgumentOutOfRangeException(nameof(payloadLength));
        //     }

        //     payload[2] = (byte)((requestId & 0b0011_1100) >> 2);
        //     payload[3] = (byte)(((requestId & 0b0000_0011) << 6) | (requestField & 0b0011_1111));
        // }

        public unsafe long SetRumble(DualMotorRumbleCommand* command)
        {
            // byte lowFreq = (byte)(Mathf.Clamp(command->lowFrequencyMotorSpeed, 0f, 1f) * 255f);
            // byte highFreq = (byte)(Mathf.Clamp(command->highFrequencyMotorSpeed, 0f, 1f) * 255f);
            // SetRumble(highFreq, lowFreq);
            return InputDeviceCommand.GenericSuccess;
        }

        public unsafe void SetLED(X360LedState ledState)
        {
            var payload = new byte[3];
            payload[0] = 0x01;
            payload[1] = 0x03;
            payload[2] = (byte)ledState;

            string hexString = BitConverter.ToString(payload).Replace("-", string.Empty);
            Logging.Verbose($"sent data: {hexString}");

            SendOutputRequest(payload, 3);
        }

        public void Dispose() {
            m_ThreadStop?.Set();

            m_Thread?.Join();
            m_Thread = null;

            m_ThreadStop?.Dispose();
            m_ThreadStop = null;

            if (m_Device != null)
            {
                m_Backend.QueueDeviceRemove(m_Device);
            }

            var result = libusb_release_interface(m_Handle, m_IfIndex);
            libusb_checkerror(result, "Failed to claim USB interface for XUSB receiver");

            m_Handle?.Dispose();
            m_Handle = null;
        }
    }

    internal class XUSBBackend : CustomInputBackend<XUSBController>
    {
        private XUSBController[] m_Controllers = new XUSBController[4];

        private uint m_ControllerCount = 0;

        protected override void OnDispose()
        {
            for (uint i = 0; i < m_Controllers.Length; i++)
            {
                m_Controllers[i]?.Dispose();
                m_Controllers[i] = null;
            }

            m_Controllers = null;
        }

        public unsafe bool Probe(
            in libusb_temp_device device,
            libusb_device_handle handle,
            in libusb_device_descriptor descriptor,
            out XUSBController controller
        )
        {
            controller = null;

            // Verify classes/protocol
            if (descriptor.bDeviceClass != libusb_class_code.VENDOR_SPEC ||
                descriptor.bDeviceSubClass != 0xFF ||
                descriptor.bDeviceProtocol != 0xFF
            )
            {
                return false;
            }

            // Only one configuration is expected
            if (descriptor.bNumConfigurations != 1)
            {
                return false;
            }

            bool success = false;
            libusb_config_descriptor* config = null;
            try
            {
                var result = libusb_set_auto_detach_kernel_driver(handle, true);
                libusb_checkerror(result, "Failed to detach USB device kernel driver");

                result = libusb_get_configuration(handle, out int configuration);
                if (libusb_checkerror(result, "Failed to get USB device configuration") && configuration != 1)
                {
                    // Explicitly set configuration
                    result = libusb_set_configuration(handle, 1);
                    if (result != libusb_error.NOT_SUPPORTED && // Not all platforms support setting the configuration
                        !libusb_checkerror(result, "Failed to set USB device configuration"))
                    {
                        return false;
                    }
                }

                result = libusb_get_active_config_descriptor(device, out config);
                if (!libusb_checkerror(result, "Failed to get configuration descriptor"))
                {
                    config = null;
                    return false;
                }

                // Find controller interface
                byte ifIndex = 0;
                ref var iface = ref config->interfaces[ifIndex];

                if (!ReadControllerDescriptor(iface, out byte inEndpoint, out byte outEndpoint, out byte subType))
                {
                    return false;
                }

                result = libusb_claim_interface(handle, ifIndex);
                if (!libusb_checkerror(result, "Failed to claim USB interface for XUSB Device"))
                {

                    return false;
                }

                controller = new XUSBController(
                    handle,
                    m_ControllerCount,
                    ifIndex,
                    inEndpoint,
                    outEndpoint,
                    this,
                    subType
                );

                m_Controllers[m_ControllerCount] = controller;

                m_ControllerCount++;

                // Also explicitly set alternate setting
                result = libusb_set_interface_alt_setting(handle, ifIndex, 0);
                if (!libusb_checkerror(result, "Failed to set interface alternate setting"))
                {
                    return false;
                }

                success = true;
            }
            finally
            {
                if (config != null)
                {
                    libusb_free_config_descriptor(config);
                }

                if (!success)
                {
                    controller?.Dispose();
                }
            }

            return success;
        }

        private static unsafe bool ReadControllerDescriptor(
            in libusb_interface iface,
            out byte inEndpoint,
            out byte outEndpoint,
            out byte subType
        )
        {
            inEndpoint = 0;
            outEndpoint = 0;
            subType = 0;

            if (iface.num_altsetting != 1)
            {
                return false;
            }

            // Verify classes/protocol
            // Protocol 0x01 is for controller data, and 0x02 is for voice data
            ref var ifDescriptor = ref *iface.altsetting;
            if (ifDescriptor.bInterfaceClass != libusb_class_code.VENDOR_SPEC ||
                ifDescriptor.bInterfaceSubClass != 0x5D ||
                ifDescriptor.bInterfaceProtocol != 0x01
            )
            {
                return false;
            }

            // Find XUSB interface descriptor
            // It is not processed, as the data contained within it is unnecessary;
            // finding it is simply an extra check to verify this is the interface we're looking for
            bool descriptorFound = false;
            for (int extraPos = 0; extraPos < ifDescriptor.extra_length; extraPos += ifDescriptor.extra[extraPos])
            {
                byte* descPtr = ifDescriptor.extra + extraPos;
                byte bLength = descPtr[0];
                if (bLength < 4)
                {
                    continue;
                }

                byte bDescriptorType = descPtr[1];
                ushort bcdXUSB = (ushort)((descPtr[3] << 8) | descPtr[2]);

                subType = descPtr[4];
                Logging.Verbose($"SubType: {subType}");
                Logging.Verbose($"bDescriptorType {bDescriptorType}");
                Logging.Verbose($"bcdXUSB {bcdXUSB}");

                if (bDescriptorType == 0x21 && bcdXUSB == 0x0110)
                {
                    descriptorFound = true;
                    break;
                }
            }

            if (!descriptorFound)
            {
                return false;
            }

            // Find controller endpoints
            // There are only two endpoints on the controller data interfaces
            if (ifDescriptor.bNumEndpoints != 2)
            {
                return false;
            }

            for (int cfIndex = 0; cfIndex < ifDescriptor.bNumEndpoints; cfIndex++)
            {
                ref var epDescriptor = ref ifDescriptor.endpoints[cfIndex];
                if ((epDescriptor.bEndpointAddress & 0x80) == (byte)libusb_endpoint_direction.IN)
                {
                    if (inEndpoint != 0)
                    {
                        Logging.Verbose("Encountered duplicate inbound endpoint on XUSB wireless interface");
                        return false;
                    }

                    inEndpoint = epDescriptor.bEndpointAddress;
                }
                else
                {
                    if (outEndpoint != 0)
                    {
                        Logging.Verbose("Encountered duplicate outbound endpoint on XUSB wireless interface");
                        return false;
                    }

                    outEndpoint = epDescriptor.bEndpointAddress;
                }
            }

            Logging.Verbose($"outEndpoint {outEndpoint}");
            Logging.Verbose($"inEndpoint {inEndpoint}");

            return true;
        }
        

        protected override XUSBController OnDeviceAdded(InputDevice device, IDisposable _context)
        {
            var context = (USBQueueContext)_context;

            var controller = m_Controllers[context.controllerIndex];

            controller.SetDevice(device);

            return controller;
        }

        protected override void OnDeviceRemoved(XUSBController device)
        {
        }

        protected override unsafe long? OnDeviceCommand(XUSBController device, InputDeviceCommand* command)
        {
            if (command->type == DualMotorRumbleCommand.Type)
                return device.SetRumble((DualMotorRumbleCommand*)command);

            return null;
        }
    }
}