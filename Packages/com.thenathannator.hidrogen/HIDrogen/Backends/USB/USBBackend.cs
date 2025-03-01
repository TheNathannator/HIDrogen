using System;
using System.Threading;
using System.Collections.Generic;
using HIDrogen.Imports;

namespace HIDrogen.Backend
{
    internal class USBBackend : IDisposable
    {
        private LibUSB _libusb;

        private Dictionary<int, IDisposable> m_Devices = new Dictionary<int, IDisposable>();

        private Thread m_watchThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        public USBBackend()
        {
            Logging.Verbose("Initializing USBBackend");

            try
            {
                _libusb = new LibUSB();
            }
            catch (Exception ex) {
                Logging.Exception("Failed to initialize libusb.", ex);
                return;
            }

            // Create a new thread
            Thread m_watchThread = new Thread(WatchForDevices);

            // Start the thread
            m_watchThread.Start();
        }

        private void WatchForDevices()
        {
            // Pause between GetDeviceList function calls.
            while (!m_ThreadStop.WaitOne(1000))
            {
                var devices = _libusb.GetDeviceList();

                Logging.Verbose($"found {devices.Count} USB devices");

                foreach (var device in devices)
                {
                    if (!m_Devices.ContainsKey(device.Id))
                        InspectDevice(device);
                }

                _libusb.FreeDeviceList();
            }
        }

        private void InspectDevice(LibUSBDevice device) {

            // Microsoft USB Device
            if (device.VendorID == 0x045e)
            {
                // Known Product IDs for this dongle.
                if (device.ProductID == 0x0291 || device.ProductID == 0x02a9 || device.ProductID == 0x0719)
                {
                    Logging.Verbose("Found X360Receiver");
                    device.Open();
                    m_Devices.Add(device.Id, new X360Receiver(device));
                }
            }
        }

        public void Dispose()
        {
            // Stop watching for devices.
            m_ThreadStop?.Set();
            m_watchThread?.Join();
            m_watchThread = null;

            m_ThreadStop?.Dispose();
            m_ThreadStop = null;

            foreach (KeyValuePair<int, IDisposable> kvp in m_Devices)
                kvp.Value.Dispose();

            m_Devices.Clear();

            _libusb.Dispose();
        }
    }
}