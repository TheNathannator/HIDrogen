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

        private Thread m_WatchThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        private readonly Dictionary<USBDeviceLocation, IDisposable> m_Devices = new Dictionary<USBDeviceLocation, IDisposable>();

        private readonly HashSet<USBDeviceLocation> m_IgnoredDevices = new HashSet<USBDeviceLocation>();

        // Cached collections to avoid repeat allocations
        private readonly HashSet<USBDeviceLocation> m_PresentDevices = new HashSet<USBDeviceLocation>();
        private readonly List<USBDeviceLocation> m_RemovedDevices = new List<USBDeviceLocation>();

        public USBBackend()
        {
            var result = libusb_init(out m_Context);
            if (result < 0)
            {
                throw new Exception($"Failed to initialize LibUSB: {libusb_strerror(result)} (0x{result:X8})");
            }

            m_WatchThread = new Thread(WatchForDevices) { IsBackground = true };
            m_WatchThread.Start();
        }

        private void WatchForDevices()
        {
            while (!m_ThreadStop.WaitOne(1000))
            {
                m_PresentDevices.Clear();
                m_RemovedDevices.Clear();

                var result = libusb_get_device_list(m_Context, out var list);
                if (!libusb_checkerror(result, "Failed to get USB device list"))
                {
                    continue;
                }

                foreach (var deviceHandle in list)
                {
                    var device = new libusb_temp_device(deviceHandle, ownsHandle: false);
                    var location = new USBDeviceLocation(
                        libusb_get_bus_number(device),
                        libusb_get_port_number(device),
                        libusb_get_device_address(device)
                    );

                    // The handle can serve as a unique ID,
                    // persisting between libusb_get_device_list calls
                    if (m_IgnoredDevices.Contains(location))
                    {
                        continue;
                    }

                    if (!m_Devices.ContainsKey(location))
                    {
                        try
                        {
                            if (!ProbeDevice(device, location))
                            {
                                m_IgnoredDevices.Add(location);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logging.Exception("Failed to probe USB device!", ex);
                        }
                    }

                    m_PresentDevices.Add(location);
                }

                // Check to see if any devices have been removed
                foreach (var deviceID in m_Devices.Keys)
                {
                    if (!m_PresentDevices.Contains(deviceID))
                    {
                        m_RemovedDevices.Add(deviceID);
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
        }

        private bool ProbeDevice(
            in libusb_temp_device device,
            in USBDeviceLocation location
        )
        {
            var result = libusb_get_device_descriptor(device, out var descriptor);
            if (!libusb_checkerror(result, "Failed to get USB device descriptor"))
            {
                return false;
            }

            Logging.Verbose($"Probing connected USB device. Location {location}, hardware IDs: {descriptor.idVendor:X4}:{descriptor.idProduct:X4})");

            if (X360Receiver.Probe(device, location, descriptor, out var receiver))
            {
                m_Devices.Add(location, receiver);
                return true;
            }

            Logging.Verbose($"Ignoring unrecognized USB device. Location {location}, hardware IDs: {descriptor.idVendor:X4}:{descriptor.idProduct:X4})");
            return false;
        }

        public void Dispose()
        {
            // Stop watching for devices.
            m_ThreadStop?.Set();
            m_WatchThread?.Join();
            m_WatchThread = null;

            m_ThreadStop?.Dispose();
            m_ThreadStop = null;

            foreach (var device in m_Devices.Values)
            {
                device?.Dispose();
            }
            m_Devices.Clear();

            m_Context?.Dispose();
            m_Context = null;
        }
    }
}
