using System;
using System.Diagnostics;
using System.Threading;
using HIDrogen.Imports;
using HIDrogen.Imports.Windows;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

// XUSB open specification:
// https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-xusbi/c79474e7-3968-43d1-8d2f-175d47bef43e

namespace HIDrogen.Backend
{
    using static libusb;

    internal enum X360LedState : byte
    {
        Off = 0,

        Blink = 1,

        Player1Blink = 2,
        Player2Blink = 3,
        Player3Blink = 4,
        Player4Blink = 5,

        Player1 = 6,
        Player2 = 7,
        Player3 = 8,
        Player4 = 9,

        Cycle = 10,
        BlinkFast = 11,
        BlinkSlow = 12,
        FlipFlop = 13,
        BlinkAll = 14,
    }

    internal class X360WirelessController : IDisposable
    {
        private enum ConnectionState
        {
            /// <summary>
            /// Controller is disconnected.
            /// </summary>
            Disconnected,

            /// <summary>
            /// Waiting for link control packet.
            /// </summary>
            LinkControl,

            /// <summary>
            /// Waiting for capabilities packet.
            /// </summary>
            Capabilities,

            /// <summary>
            /// Waiting for input system device addition.
            /// </summary>
            DeviceAddition,

            /// <summary>
            /// Controller is connected and initialized.
            /// </summary>
            Connected,
        }

        private const int kMaxInputLength = 32;
        private const int kMaxOutputLength = 32;

        private const int kOutputReportLength = 12;

        private const byte kOutputControllerRequest = 0;
        // private const byte kOutputVoiceRequest = 1; // not needed
        private const byte kOutputConnectionRequest = 8;

        private static readonly XInputCapabilities s_DefaultCapabilities = new XInputCapabilities()
        {
            type = XInputDeviceType.Gamepad,
            subType = XInputDeviceSubType.Gamepad,
            flags = XInputDeviceFlags.Wireless | XInputDeviceFlags.Voice,
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

        private X360Receiver m_Receiver;
        private libusb_device_handle m_Handle;

        private Thread m_Thread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        private readonly int m_InterfaceIndex;
        private readonly byte m_InEndpoint;
        private readonly byte m_OutEndpoint;

        private XInputCapabilities m_Capabilities = s_DefaultCapabilities;
        private InputDevice m_Device;
        private readonly uint m_ControllerIndex;


        private ConnectionState m_ConnectionState;
        private readonly Stopwatch m_StateTimer = new Stopwatch();

        public bool connected => m_ConnectionState != ConnectionState.Disconnected;

        public X360WirelessController(
            X360Receiver receiver,
            libusb_device_handle handle,
            uint controllerIndex,
            int interfaceIndex,
            byte inEndpoint,
            byte outEndpoint
        )
        {
            m_Receiver = receiver;
            m_Handle = handle;

            m_ControllerIndex = controllerIndex;
            m_InterfaceIndex = interfaceIndex;
            m_InEndpoint = inEndpoint;
            m_OutEndpoint = outEndpoint;

            // Start initial state timer
            SetConnectionState(ConnectionState.Disconnected);

            // Create a new thread to wait for and handle input.
            m_Thread = new Thread(PollThread) { IsBackground = true };
            m_Thread.Start();
        }

        public void Dispose()
        {
            m_ThreadStop?.Set();

            m_Thread?.Join();
            m_Thread = null;

            m_ThreadStop?.Dispose();
            m_ThreadStop = null;

            if (m_Device != null)
            {
                m_Receiver?.QueueDeviceRemove(m_Device);
                m_Device = null;
            }

            m_ConnectionState = ConnectionState.Disconnected;
            m_StateTimer.Stop();

            if (m_Handle != null)
            {
                PowerDown();

                var result = libusb_release_interface(m_Handle, m_InterfaceIndex);
                libusb_checkerror(result, "Failed to release USB device interface");

                m_Handle = null;
            }

            m_Receiver = null;
        }

        public void SetDevice(InputDevice device)
        {
            if (m_ConnectionState != ConnectionState.DeviceAddition)
            {
                return;
            }

            m_Device = device;
            SetConnectionState(ConnectionState.Connected);
        }

        private void QueueForAddition()
        {
            var description = new InputDeviceDescription()
            {
                interfaceName = XInputBackend.InterfaceName,
                // User index is not included since this backend will not match up with XInput
                capabilities = JsonUtility.ToJson(m_Capabilities),
            };

            m_Receiver.QueueDeviceAdd(description, new USBQueueContext()
            {
                controllerIndex = m_ControllerIndex
            });

            SetConnectionState(ConnectionState.DeviceAddition);
        }

        private void SetConnectionState(ConnectionState state)
        {
            m_ConnectionState = state;
            m_StateTimer.Restart();
        }

        private void PollThread()
        {
            var payload = new byte[kMaxInputLength];

            const int retryThreshold = 3;
            int errorCount = 0;

            while (!m_ThreadStop.WaitOne(1))
            {
                // Service state timers
                switch (m_ConnectionState)
                {
                    case ConnectionState.Disconnected:
                    {
                        // Periodically request connection status for disconnected controllers
                        if (!m_StateTimer.IsRunning || m_StateTimer.ElapsedMilliseconds >= 5000)
                        {
                            m_StateTimer.Restart();
                            RequestConnectionStatus();
                        }
                        break;
                    }
                    case ConnectionState.Capabilities:
                    {
                        // for whatever reason, the latency between requesting capabilities
                        // and receiving them is *really* high
                        if (m_StateTimer.IsRunning && m_StateTimer.ElapsedMilliseconds >= 250)
                        {
                            m_StateTimer.Stop();
                            Logging.Verbose($"Controller index {m_ControllerIndex} timed out on capabilities");
                            QueueForAddition();
                        }
                        break;
                    }
                }

                // Read incoming reports
                var result = libusb_interrupt_transfer(m_Handle, m_InEndpoint, payload, out int actualLength, 5);
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
                            libusb_logerror(result, "Failed to receive X360 transfer");
                            return;
                        }
#if HIDROGEN_VERBOSE_LOGGING
                        else
                        {
                            libusb_logerror(result, $"Failed to receive X360 transfer (attempt {errorCount})");
                        }
#endif
                        continue;
                    }
                }

                errorCount = 0;
                if (actualLength < 1)
                {
                    continue;
                }

                byte reportId = payload[0];
                switch (reportId)
                {
                    case 0x00: // Controller data and status
                    {
                        ProcessControllerReport(payload);
                        break;
                    }
                    case 0x01: // Voice data and status
                    {
                        // Ignored
                        break;
                    }
                    case 0x08: // Wireless connection status
                    {
                        ProcessConnectionStatusReport(payload);
                        break;
                    }
                    default:
                    {
                        Logging.Verbose($"Unrecognized X360 wireless report ID {reportId}");
                        break;
                    }
                }
            }
        }

        // For posterity/future use
        private (byte reportId, ushort reportField) ReadInputHeader(byte[] payload)
        {
            byte reportId = (byte)((payload[3] & 0xF0) >> 4);
            ushort reportField = (ushort)(((payload[3] & 0x0F) << 8) | payload[4]);
            return (reportId, reportField);
        }

        #region 0x00 - Controller data/status reports

        private void ProcessControllerReport(byte[] payload)
        {
            byte reportType = payload[1];
            switch (reportType)
            {
                case 0x00: // Header-only report
                {
                    // Ignored
                    break;
                }
                case 0x01: // Input data
                {
                    ProcessControllerInputReport(payload);
                    break;
                }
                case 0x02: // Plug-in module data
                {
                    // Ignored
                    break;
                }
                case 0x03: // Input data + plug-in module data
                {
                    // The plug-in module data occupies the 5 reserved bytes at the end
                    // of the normal controller input report, no extra processing is needed
                    ProcessControllerInputReport(payload);
                    break;
                }
                case 0x04: // Security transport
                {
                    // Ignored
                    break;
                }
                case 0x05: // UART/generic
                {
                    ProcessUART(payload);
                    break;
                }
                case 0x09: // Battery product ID
                {
                    // Ignored
                    break;
                }
                case 0x0A: // Battery copyright string
                {
                    // Ignored
                    break;
                }
                case 0x0F: // Link control
                {
                    ProcessLinkControl(payload);
                    break;
                }
                case 0xF8: // Controller RSSI
                {
                    // Ignored
                    break;
                }
                case 0xF9: // Voice RSSI
                {
                    // Ignored
                    break;
                }
                default:
                {
                    Logging.Verbose($"Unrecognized X360 wireless controller report type {reportType}");
                    break;
                }
            }
        }

        private void ProcessControllerInputReport(byte[] payload)
        {
            byte reportSubType = payload[5];
            switch (reportSubType)
            {
                case 0x02: // Force feedback
                {
                    // Ignored
                    break;
                }
                case 0x13: // Controller input data
                {
                    if (m_Device != null)
                    {
                        m_Receiver.QueueStateEvent(m_Device, XInputGamepad.Format, payload, 6, 18);
                    }
                    break;
                }
                default:
                {
                    Logging.Verbose($"Unrecognized X360 wireless controller report format {reportSubType}");
                    break;
                }
            }
        }

        private void ProcessUART(byte[] payload)
        {
            byte reportSubType = payload[5];
            switch (reportSubType)
            {
                // This seems to be used for reporting a block of device capabilities.
                // It's not documented in the XUSB specification, .
                case 0x12:
                {
                    ProcessCapabilities(payload);
                    break;
                }
            }
        }

        private void ProcessLinkControl(byte[] payload)
        {
            if (m_ConnectionState != ConnectionState.LinkControl)
            {
                return;
            }

            // byte linkId = payload[5];
            // uint hostId = payload[6..10];
            // uint deviceId = payload[10..14];
            // ushort deviceType = payload[14..16];
            // ushort deviceState = payload[16..18];
            // ushort protocolVersion = payload[18..20];
            // ushort securityLevel = payload[20..22];
            // ushort vendorId = payload[22..24];
            // ushort subType = payload[24..26];

            // Some wireless devices report their subtype with the 0x80 bit toggled for some reason
            var subType = (XInputDeviceSubType)(payload[25] & 0x7F);

            m_Capabilities.subType = subType;

            // Set player LED
            switch (m_ControllerIndex)
            {
                case 0: SetLED(X360LedState.Player1Blink); break;
                case 1: SetLED(X360LedState.Player2Blink); break;
                case 2: SetLED(X360LedState.Player3Blink); break;
                case 3: SetLED(X360LedState.Player4Blink); break;
            }

            // Request additional capabilities data and wait for it to be received
            RequestCapabilities();
            SetConnectionState(ConnectionState.Capabilities);
        }

        private void ProcessCapabilities(byte[] payload)
        {
            if (m_ConnectionState != ConnectionState.Capabilities)
            {
                return;
            }

            m_Capabilities.gamepad = new XInputGamepad()
            {
                buttons = (XInputButton)((payload[7] << 8) | payload[6]),
                leftTrigger = payload[8],
                rightTrigger = payload[9],
                leftStickX = (short)((payload[11] << 8) | payload[10]),
                leftStickY = (short)((payload[13] << 8) | payload[12]),
                rightStickX = (short)((payload[15] << 8) | payload[14]),
                rightStickY = (short)((payload[17] << 8) | payload[16]),
            };
            m_Capabilities.vibration = new XInputVibration()
            {
                leftMotor = payload[18],
                rightMotor = payload[19],
            };

            // 9 bytes leftover: payload[20..29], purpose unknown

            // Full capabilities data has been received by this point, queue for addition
            QueueForAddition();
        }

        #endregion

        #region 0x08 - Device connection status

        private void ProcessConnectionStatusReport(byte[] payload)
        {
            byte connectionFlags = payload[1];
            bool isConnected = (connectionFlags & 0x80) != 0;
            // bool voiceConnected = (connectionFlags & 0x40) != 0;

            if (isConnected == connected)
            {
                return;
            }

            if (isConnected && m_ConnectionState == ConnectionState.Disconnected)
            {
                // Wait until link control packet is received before adding,
                // as it contains needed device type information
                SetConnectionState(ConnectionState.LinkControl);
            }
            else if (!isConnected && m_ConnectionState != ConnectionState.Disconnected)
            {
                if (m_Device != null)
                {
                    m_Receiver.QueueDeviceRemove(m_Device);
                }

                m_Capabilities = s_DefaultCapabilities;
                SetConnectionState(ConnectionState.Disconnected);
            }
        }

        #endregion

        #region Output reports

        private unsafe void SendOutputRequest(byte* payload, int payloadLength)
        {
            if (payloadLength > kMaxOutputLength)
            {
                throw new ArgumentOutOfRangeException(nameof(payloadLength));
            }

            // TODO: Use asynchronous API for this
            var result = libusb_interrupt_transfer(m_Handle, m_OutEndpoint, payload, payloadLength, out _, 10);
            libusb_checkerror(result, "Failed to send X360 output request");
        }

        private unsafe void WriteOutputHeader(byte* payload, int payloadLength, byte requestId, byte requestField)
        {
            // safety check
            if (payloadLength < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(payloadLength));
            }

            payload[2] = (byte)((requestId & 0b0011_1100) >> 2);
            payload[3] = (byte)(((requestId & 0b0000_0011) << 6) | (requestField & 0b0011_1111));
        }

        // stackalloc doesn't initialize the bytes it returns
        private unsafe void ClearRemaining(byte* payload, int startIndex, int payloadLength)
        {
            for (int i = startIndex; i < payloadLength; i++)
            {
                payload[i] = 0;
            }
        }

        private unsafe void RequestConnectionStatus()
        {
            const int LENGTH = kOutputReportLength;

            byte* payload = stackalloc byte[LENGTH];
            payload[0] = kOutputConnectionRequest;
            payload[1] = 0x00; // request for controller link

            // payload[2] = 0x0f;
            // payload[3] = 0xc0;
            WriteOutputHeader(payload, LENGTH, 0x3F, 0);

            ClearRemaining(payload, 4, LENGTH);
            SendOutputRequest(payload, LENGTH);
        }

        private unsafe void RequestCapabilities()
        {
            const int LENGTH = kOutputReportLength;

            byte* payload = stackalloc byte[LENGTH];
            payload[0] = kOutputControllerRequest;
            payload[1] = 0x00; // no data

            // payload[2] = 0x02;
            // payload[3] = 0x80;
            WriteOutputHeader(payload, LENGTH, 0x0A, 0);

            ClearRemaining(payload, 4, LENGTH);
            SendOutputRequest(payload, LENGTH);
        }

        public unsafe void SetRumble(byte highFreq, byte lowFreq)
        {
            const int LENGTH = kOutputReportLength;

            byte* payload = stackalloc byte[LENGTH];
            payload[0] = kOutputControllerRequest;
            payload[1] = 0x01; // set rumble

            // payload[2] = 0x0f;
            // payload[3] = 0xc0;
            WriteOutputHeader(payload, LENGTH, 0x3F, 0);

            payload[4] = 0x00; // dual motor
            payload[5] = highFreq;
            payload[6] = lowFreq;

            ClearRemaining(payload, 7, LENGTH);
            SendOutputRequest(payload, LENGTH);
        }

        public unsafe void SetLED(X360LedState ledState)
        {
            const int LENGTH = kOutputReportLength;

            byte* payload = stackalloc byte[LENGTH];
            payload[0] = kOutputControllerRequest;
            payload[1] = 0x00; // no data

            // payload[2] = 0x08;
            // payload[3] = (byte)(0x40 | ledState);
            WriteOutputHeader(payload, LENGTH, 0x21, (byte)ledState);

            ClearRemaining(payload, 4, LENGTH);
            SendOutputRequest(payload, LENGTH);
        }

        public unsafe void PowerDown()
        {
            const int LENGTH = kOutputReportLength;

            byte* payload = stackalloc byte[LENGTH];
            payload[0] = kOutputControllerRequest;
            payload[1] = 0x00; // no data

            // payload[2] = 0x08;
            // payload[3] = 0xc0;
            WriteOutputHeader(payload, LENGTH, 0x23, 0);

            ClearRemaining(payload, 4, LENGTH);
            SendOutputRequest(payload, LENGTH);
        }

        #endregion

        #region Device commands

        public unsafe long SetRumble(DualMotorRumbleCommand* command)
        {
            byte lowFreq = (byte)(Mathf.Clamp(command->lowFrequencyMotorSpeed, 0f, 1f) * 255f);
            byte highFreq = (byte)(Mathf.Clamp(command->highFrequencyMotorSpeed, 0f, 1f) * 255f);
            SetRumble(highFreq, lowFreq);
            return InputDeviceCommand.GenericSuccess;
        }

        #endregion
    }
}
