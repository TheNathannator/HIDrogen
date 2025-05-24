using System;
using System.Collections.Generic;
using System.Threading;
using HIDrogen.Imports;

namespace HIDrogen.Backend
{
    using static libusb;

    internal class USBBackend : IDisposable
    {
        private libusb_context m_Context;

        private Thread m_WatchThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        private readonly Dictionary<int, IDisposable> m_Devices = new Dictionary<int, IDisposable>();

        private readonly HashSet<int> m_IgnoredDeviceIDs = new HashSet<int>();

        // Cached collections to avoid repeat allocations
        private readonly HashSet<int> m_PresentDeviceIDs = new HashSet<int>();
        private readonly List<int> m_RemovedDeviceIDs = new List<int>();

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
                m_PresentDeviceIDs.Clear();
                m_RemovedDeviceIDs.Clear();

                var result = libusb_get_device_list(m_Context, out var list);
                if (!libusb_checkerror(result, "Failed to get USB device list"))
                {
                    continue;
                }

                foreach (var deviceHandle in list)
                {
                    var device = new libusb_temp_device(deviceHandle, ownsHandle: false);

                    byte bus = libusb_get_bus_number(device);
                    byte port = libusb_get_port_number(device);
                    byte address = libusb_get_device_address(device);
                    int deviceId = (bus << 16) | (port << 8) | address;

                    // The handle can serve as a unique ID,
                    // persisting between libusb_get_device_list calls
                    if (m_IgnoredDeviceIDs.Contains(deviceId))
                    {
                        continue;
                    }

                    if (!m_Devices.ContainsKey(deviceId))
                    {
                        try
                        {
                            ProbeDevice(deviceId, device);
                        }
                        catch (Exception ex)
                        {
                            Logging.Exception("Failed to probe USB device!", ex);
                        }
                    }

                    m_PresentDeviceIDs.Add(deviceId);
                }

                // Check to see if any devices have been removed
                foreach (var deviceID in m_Devices.Keys)
                {
                    if (!m_PresentDeviceIDs.Contains(deviceID))
                    {
                        m_RemovedDeviceIDs.Add(deviceID);
                    }
                }

                // The actual removal must happen as a second step, as trying to
                // enumerate a collection while modifying it will result in problems
                foreach (var removedId in m_RemovedDeviceIDs)
                {
                    m_Devices[removedId].Dispose();
                    m_Devices.Remove(removedId);
                }
            }
        }

        private void ProbeDevice(int deviceId, in libusb_temp_device device)
        {
            var result = libusb_get_device_descriptor(device, out var descriptor);
            if (!libusb_checkerror(result, "Failed to get USB device descriptor"))
            {
                m_IgnoredDeviceIDs.Add(deviceId);
                return;
            }

            if (X360Receiver.Probe(device, descriptor, out var receiver))
            {
                m_Devices.Add(deviceId, receiver);
                return;
            }

            // Device is not interesting, ignore it in future.
            m_IgnoredDeviceIDs.Add(deviceId);
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
