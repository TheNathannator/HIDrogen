using System;
using System.Collections.Generic;
using System.Threading;
using HIDrogen.Imports;

namespace HIDrogen.Backend
{
    using static libusb;

    internal struct USBDeviceLocation
    {
        public byte bus;
        public byte port;
        public byte address;

        public int deviceId => (bus << 16) | (port << 8) | address;

        public USBDeviceLocation(byte bus, byte port, byte address)
        {
            this.bus = bus;
            this.port = port;
            this.address = address;
        }

        public override string ToString() => $"{bus:X2}:{port:X2}:{address:X2}";
        public override int GetHashCode() => (bus, port, address).GetHashCode();
    }

    internal class USBBackend : IDisposable
    {
        private libusb_context m_Context;

        private Thread m_DeviceThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        private readonly Dictionary<USBDeviceLocation, IDisposable> m_Devices = new Dictionary<USBDeviceLocation, IDisposable>();
        private readonly Dictionary<USBDeviceLocation, int> m_DeviceAttempts = new Dictionary<USBDeviceLocation, int>();

        private readonly HashSet<USBDeviceLocation> m_IgnoredDevices = new HashSet<USBDeviceLocation>();

        // Cached collections to avoid repeat allocations
        private readonly HashSet<USBDeviceLocation> m_PresentDevices = new HashSet<USBDeviceLocation>();
        private readonly List<USBDeviceLocation> m_RemovedDevices = new List<USBDeviceLocation>();

        public USBBackend()
        {
            var result = libusb_init(out m_Context);
            if (result != libusb_error.SUCCESS)
            {
                throw new Exception($"Failed to initialize libusb: {libusb_strerror(result)} (0x{result:X8})");
            }

            m_DeviceThread = new Thread(DeviceThread) { IsBackground = true };
            m_DeviceThread.Start();
        }

        public void Dispose()
        {
            if (m_Context == null)
            {
                return;
            }

            // Stop threads
            m_ThreadStop?.Set();
            Interlocked.Exchange(ref m_StopEvents, 1);
            libusb_interrupt_event_handler(m_Context);

            m_DeviceThread?.Join();
            m_DeviceThread = null;

            m_ThreadStop?.Dispose();
            m_ThreadStop = null;

            // Clear devices
            foreach (var device in m_Devices.Values)
            {
                device?.Dispose();
            }
            m_Devices.Clear();

            // Uninitialize libusb
            m_Context?.Dispose();
            m_Context = null;
        }

        private void DeviceThread()
        {
            while (!m_ThreadStop.WaitOne(1000))
            {
                PollDevices();
            }
        }

        private void PollDevices()
        {
            m_PresentDevices.Clear();
            m_RemovedDevices.Clear();

            var result = libusb_get_device_list(m_Context, out var list);
            if (!libusb_checkerror(result, "Failed to get USB device list"))
            {
                return;
            }

            // Check for new devices
            foreach (var deviceHandle in list)
            {
                var device = new libusb_temp_device(deviceHandle, ownsHandle: false);
                var location = new USBDeviceLocation(
                    libusb_get_bus_number(device),
                    libusb_get_port_number(device),
                    libusb_get_device_address(device)
                );

                if (m_IgnoredDevices.Contains(location))
                {
                    continue;
                }

                if (!m_Devices.ContainsKey(location))
                {
                    try
                    {
                        ProbeDevice(device, location);
                    }
                    catch (Exception ex)
                    {
                        Logging.Exception("Failed to probe USB device!", ex);
                    }
                }

                m_PresentDevices.Add(location);
            }

            // Check to see if any devices have been removed
            foreach (var location in m_Devices.Keys)
            {
                if (!m_PresentDevices.Contains(location))
                {
                    m_RemovedDevices.Add(location);
                }
            }

            // The actual removal must happen as a second step, as trying to
            // modify a collection while enumerating it will result in problems
            foreach (var removed in m_RemovedDevices)
            {
                Logging.Verbose($"Removing disconnected device at location {removed}");
                m_Devices[removed].Dispose();
                m_Devices.Remove(removed);
            }
        }

        private void ProbeDevice(
            in libusb_temp_device device,
            in USBDeviceLocation location
        )
        {
            var result = libusb_get_device_descriptor(device, out var descriptor);
            if (!libusb_checkerror(result, "Failed to get USB device descriptor"))
            {
                OnProbeFailure(location);
                return;
            }

            bool success = false;
            libusb_device_handle handle = null;
            try
            {
                result = libusb_open(device, out handle);
                if (result == libusb_error.NOT_SUPPORTED || result == libusb_error.NOT_FOUND)
                {
                    Logging.Verbose($"Ignoring inaccessible USB device. Location {location}, hardware IDs: {descriptor.idVendor:X4}:{descriptor.idProduct:X4}");
                    m_IgnoredDevices.Add(location);
                    return;
                }
                else if (result != libusb_error.SUCCESS)
                {
                    libusb_logerror(result, $"Failed to open USB device (location {location}, hardware IDs: {descriptor.idVendor:X4}:{descriptor.idProduct:X4})");
                    OnProbeFailure(location);
                    return;
                }

                if (X360Receiver.Probe(device, handle, descriptor, out var receiver))
                {
                    Logging.Verbose($"Found Xbox 360 receiver. Location: {location}, hardware IDs: {descriptor.idVendor:X4}:{descriptor.idProduct:X4}");
                    m_Devices.Add(location, receiver);
                    success = true;
                    return;
                }

                Logging.Verbose($"Ignoring unrecognized USB device. Location {location}, hardware IDs: {descriptor.idVendor:X4}:{descriptor.idProduct:X4}");
                m_IgnoredDevices.Add(location);
            }
            finally
            {
                if (!success)
                {
                    handle?.Dispose();
                }
            }
        }

        private void OnProbeFailure(in USBDeviceLocation location)
        {
            if (!m_DeviceAttempts.TryGetValue(location, out int attempts))
            {
                attempts = 0;
            }

            m_DeviceAttempts[location] = ++attempts;

            if (attempts >= 3)
            {
                Logging.Verbose($"Failed too many times to probe device at location {location}, ignoring it.");
                m_IgnoredDevices.Add(location);
            }
        }
    }
}
