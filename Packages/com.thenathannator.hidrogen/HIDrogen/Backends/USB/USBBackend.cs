using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using LibUSBWrapper;

namespace HIDrogen.Backend
{
    internal class USBBackend : IDisposable
    {
        private LibUSBWrapper.LibUSB _libusb;

        private List<IDisposable> m_Devices = new List<IDisposable>();

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public USBBackend()
        {
            Debug.Log("[USBBackend] init");

            try
            {
                _libusb = new LibUSBWrapper.LibUSB();
            }
            catch (Exception ex) {
                Debug.Log($"Failed to init libusb. {ex}");
                return;
            }

            // Create a task to watch 
            var token = cancellationTokenSource.Token;

            Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    GetDevices();
                    Thread.Sleep(1000);
                }
            }, token);
        }

        void GetDevices()
        {
            // We're only searching for one device at the moment.
            if (m_Devices.Count == 1) return;

            var devices = _libusb.GetDeviceList();

            Debug.Log($"found {devices.Count} devices");

            foreach (var device in devices)
            {
                InspectDevice(device);
            }

            _libusb.FreeDeviceList();
        }

        private void InspectDevice(LibUSBWrapper.LibUSBDevice device) {

            // Microsoft USB Device
            if (device.VendorID == 0x045e)
            {
                // Known Product IDs for this dongle.
                if (device.ProductID == 0x0291 || device.ProductID == 0x02a9 || device.ProductID == 0x0719)
                {
                    Debug.Log("Found X360Receiver");
                    device.Open();
                    m_Devices.Add(new X360Receiver(device));
                }
            }
        }

        public void Dispose()
        {
            Debug.Log("[USBBackend] disposing");

            // Stop watching for devices.
            cancellationTokenSource.Cancel();

            foreach (var device in m_Devices)
                device.Dispose();

            _libusb.Dispose();
        }
    }
}