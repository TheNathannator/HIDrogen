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

        private List<int> ignoredDeviceIDs = new List<int>();

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
            m_watchThread.Start();
        }

        private void WatchForDevices()
        {
            // Pause between GetDeviceList function calls.
            while (!m_ThreadStop.WaitOne(1000))
            {
                var devices = _libusb.GetDeviceList();

                // Inspect each device that hasn't been ignored or connected already.
                foreach (var device in devices)
                    if (!ignoredDeviceIDs.Contains(device.Id) && !m_Devices.ContainsKey(device.Id))
                        InspectDevice(device);

                // Create a list of connected deviceIDs.
                List<int> connectedIDs = devices.ConvertAll<int>(device => device.Id);

                // Remove each existing device not in connectedIDs.
                foreach (int deviceID in new List<int>(m_Devices.Keys))
                    if (!connectedIDs.Contains(deviceID))
                        RemoveDevice(deviceID);

                // Disconnect and remove 
                _libusb.FreeDeviceList();
            }
        }

        private void InspectDevice(LibUSBDevice device) {

            Logging.Verbose("Inspecting new USB Device");

            var descriptor = device.GetDescriptor();

            // Microsoft USB Device
            if (descriptor.idVendor == 0x045e)
            {
                // Known Product IDs for this dongle.
                if (descriptor.idProduct == 0x0291 || descriptor.idProduct == 0x02a9 || descriptor.idProduct == 0x0719)
                {
                    Logging.Verbose("Found X360Receiver");
                    m_Devices.Add(device.Id, new X360Receiver(device));
                    return;
                }
            }

            // Device is not interesting, ignore it in future.
            Logging.Verbose($"Ignoring USB Device (VID 0x{descriptor.idVendor:X4} PID 0x{descriptor.idProduct:X4})");
            ignoredDeviceIDs.Add(device.Id);
        }

        private void RemoveDevice(int deviceID)
        {
            m_Devices[deviceID].Dispose();
            m_Devices.Remove(deviceID);
        }

        public void Dispose()
        {
            // Stop watching for devices.
            m_ThreadStop?.Set();
            m_watchThread?.Join();
            m_watchThread = null;

            m_ThreadStop?.Dispose();
            m_ThreadStop = null;

            foreach (int deviceID in new List<int>(m_Devices.Keys))
                RemoveDevice(deviceID);

            _libusb.Dispose();
        }
    }
}