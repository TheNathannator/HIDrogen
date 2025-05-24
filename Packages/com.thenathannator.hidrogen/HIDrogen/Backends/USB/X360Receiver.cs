using System;
using System.Collections.Generic;
using HIDrogen.Imports;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// XUSB open specification:
// https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-xusbi/c79474e7-3968-43d1-8d2f-175d47bef43e

namespace HIDrogen.Backend
{
    using static libusb;

    internal class USBQueueContext : IDisposable
    {
        public uint controllerIndex;

        public void Dispose() { }
    }

    internal class X360Receiver : CustomInputBackend<X360WirelessController>
    {
        // Each wireless receiver supports up to 4 controllers.
        private const int kControllerCount = 4;

        private static readonly HashSet<(ushort vid, ushort pid)> s_HardwareIdWhitelist = new HashSet<(ushort, ushort)>()
        {
            // Microsoft
            // 0x0291 and 0x0719 are official product IDs, third-party receivers use others
            (0x045e, 0x0291),
            (0x045e, 0x02a9),
            (0x045e, 0x0719),
        };

        private libusb_device_handle m_Handle;
        private X360WirelessController[] m_Controllers;

        private X360Receiver(libusb_device_handle handle)
        {
            m_Handle = handle;
            // Elements initialized in Probe()
            m_Controllers = new X360WirelessController[kControllerCount];
        }

        protected override void OnDispose()
        {
            for (uint i = 0; i < m_Controllers.Length; i++)
            {
                m_Controllers[i]?.Dispose();
                m_Controllers[i] = null;
            }
            m_Controllers = null;

            m_Handle?.Dispose();
            m_Handle = null;
        }

        public static unsafe bool Probe(
            in libusb_temp_device device,
            in libusb_device_descriptor descriptor,
            out X360Receiver receiver
        )
        {
            receiver = null;

            // Check hardware IDs
            // TODO: Is there a reliable detection method that doesn't rely on hardware IDs?
            if (!s_HardwareIdWhitelist.Contains((descriptor.idVendor, descriptor.idProduct)))
            {
                return false;
            }

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
                var result = libusb_open(device, out var handle);
                if (!libusb_checkerror(result, "Failed to open USB device"))
                {
                    return false;
                }

                receiver = new X360Receiver(handle);

                result = libusb_set_auto_detach_kernel_driver(handle, true);
                libusb_checkerror(result, "Failed to detach USB device kernel driver");

                // Explicitly set configuration
                result = libusb_set_configuration(handle, 0);
                if (result != libusb_error.NOT_SUPPORTED && // Not all platforms support setting the configuration
                    !libusb_checkerror(result, "Failed to set USB device configuration"))
                {
                    return false;
                }

                result = libusb_get_config_descriptor(device, 0, out config);
                if (!libusb_checkerror(result, "Failed to get configuration descriptor"))
                {
                    config = null;
                    return false;
                }

                // Find controller interfaces
                uint controllerCount = 0;
                for (int ifIndex = 0; ifIndex < config->bNumInterfaces && controllerCount < kControllerCount; ifIndex++)
                {
                    ref var iface = ref config->interfaces[ifIndex];

                    if (!ReadControllerDescriptor(iface, out byte inEndpoint, out byte outEndpoint))
                    {
                        continue;
                    }

                    result = libusb_claim_interface(handle, ifIndex);
                    if (!libusb_checkerror(result, "Failed to claim USB interface for X360 receiver"))
                    {
                        return false;
                    }

                    receiver.m_Controllers[controllerCount] = new X360WirelessController(
                        receiver,
                        handle,
                        controllerCount,
                        ifIndex,
                        inEndpoint,
                        outEndpoint
                    );
                    controllerCount++;

                    // Also explicitly set alternate setting
                    result = libusb_set_interface_alt_setting(handle, ifIndex, 0);
                    if (!libusb_checkerror(result, "Failed to set interface alternate setting"))
                    {
                        return false;
                    }
                }

                if (controllerCount == receiver.m_Controllers.Length)
                {
                    success = true;
                }
            }
            finally
            {
                if (config != null)
                {
                    libusb_free_config_descriptor(config);
                }

                if (!success)
                {
                    receiver?.Dispose();
                }
            }

            return success;
        }

        private static unsafe bool ReadControllerDescriptor(
            in libusb_interface iface,
            out byte inEndpoint,
            out byte outEndpoint
        )
        {
            inEndpoint = 0;
            outEndpoint = 0;

            if (iface.num_altsetting != 1)
            {
                return false;
            }

            // Verify classes/protocol
            // Protocol 0x81 is for controller data, and 0x82 is for voice data
            ref var ifDescriptor = ref *iface.altsetting;
            if (ifDescriptor.bInterfaceClass != libusb_class_code.VENDOR_SPEC ||
                ifDescriptor.bInterfaceSubClass != 0x5D ||
                ifDescriptor.bInterfaceProtocol != 0x81
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
                if (bDescriptorType == 0x22 && bcdXUSB == 0x0100)
                {
                    descriptorFound = true;
                    break;
                }

                // For posterity
                // // Process wReports entries
                // int reportPos = 4;
                // while (reportPos < bLength)
                // {
                //     if ((reportPos - bLength) < 2)
                //     {
                //         Logging.Verbose("Encountered corrupted or malformed XUSB wireless interface descriptor");
                //         return false;
                //     }

                //     byte* reportPtr = descPtr + reportPos;

                //     byte endpointType = (byte)((reportPtr[0] & 0xF0) >> 4);
                //     byte reportCount = (byte)(reportPtr[0] & 0x0F);
                //     byte endpointAddress = reportPtr[1];

                //     reportPos += 2;

                //     var reports = new (byte reportId, byte reportLength)[reportCount];
                //     for (int i = 0; i < reportCount; i++, reportPos += 2)
                //     {
                //         if (reportPos >= bLength)
                //         {
                //             Logging.Verbose("Encountered corrupted or malformed XUSB wireless interface descriptor");
                //             return false;
                //         }

                //         byte reportLength = reportPtr[reportPos];
                //         byte reportId = reportPtr[reportPos + 1];
                //         reports[i] = (reportId, reportLength);
                //     }
                
                //     // TODO: Correlate with endpoints
                // }
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

            return true;
        }

        protected override X360WirelessController OnDeviceAdded(InputDevice device, IDisposable _context)
        {
            var context = (USBQueueContext)_context;

            var controller = m_Controllers[context.controllerIndex];
            if (!controller.connected)
            {
                throw new Exception("Device was disconnected while queued for addition");
            }

            controller.SetDevice(device);

            return controller;
        }

        protected override void OnDeviceRemoved(X360WirelessController device)
        {
        }

        protected override unsafe long? OnDeviceCommand(X360WirelessController device, InputDeviceCommand* command)
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

            if (command->type == DualMotorRumbleCommand.Type)
                return device.SetRumble((DualMotorRumbleCommand*)command);

            return null;
        }
    }
}
