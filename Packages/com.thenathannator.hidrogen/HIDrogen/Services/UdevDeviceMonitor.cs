#if UNITY_STANDALONE_LINUX
using System;
using System.Collections.Generic;
using System.Threading;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;

// TODO: Avoid allocations from string unmarshalling where reasonable

namespace HIDrogen.Services
{
    internal class UdevDeviceMonitor : ICustomInputService
    {
        private Udev m_Udev;
        private udev_context m_Context;
        private udev_monitor m_Monitor;
        private fd m_MonitorFd;

        private Thread m_EnumerationThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        public UdevDeviceMonitor()
        {
            m_Udev = new Udev();
        }

        public void Dispose()
        {
            StopMonitor();

            m_ThreadStop?.Dispose();
            m_ThreadStop = null;

            m_MonitorFd?.Dispose();
            m_MonitorFd = null;

            m_Monitor?.Dispose();
            m_Monitor = null;

            m_Context?.Dispose();
            m_Context = null;

            m_Udev?.Dispose();
            m_Udev = null;
        }

        public void Start()
        {
            m_Context = m_Udev.context_new();
            if (m_Context == null || m_Context.IsInvalid)
            {
                throw new Exception(Logging.MakeLastErrorMessage("Failed to create udev context"));
            }

            StartMonitor();
            EnumerateDevices();
        }

        public void Stop()
        {
            StopMonitor();
        }

        private void StartMonitor()
        {
            m_Monitor = m_Context.monitor_new_from_netlink("udev");
            if (m_Monitor == null || m_Monitor.IsInvalid)
            {
                throw new Exception(Logging.MakeLastErrorMessage("Failed to create device monitor"));
            }

            // TODO: commented out for investigation
            // if (m_Monitor.filter_add_match_subsystem_devtype("input", null) < 0 ||
            //     m_Monitor.filter_add_match_subsystem_devtype("hidraw", null) < 0)
            // {
            //     m_Monitor.Dispose();
            //     throw new Exception(Logging.MakeLastErrorMessage("Failed to add filters to device monitor"));
            // }

            if (m_Monitor.enable_receiving() < 0)
            {
                m_Monitor.Dispose();
                throw new Exception(Logging.MakeLastErrorMessage("Failed to start device monitor"));
            }

            m_MonitorFd = m_Monitor.get_fd();
            if (m_MonitorFd == null || m_MonitorFd.IsInvalid)
            {
                m_Monitor.Dispose();
                throw new Exception(Logging.MakeLastErrorMessage("Failed to get device monitor file descriptor"));
            }

            m_ThreadStop.Reset();

            m_EnumerationThread = new Thread(MonitorThread) { IsBackground = true };
            m_EnumerationThread.Start();
        }

        private void StopMonitor()
        {
            m_ThreadStop?.Set();

            if (m_EnumerationThread != null && m_EnumerationThread.IsAlive)
            {
                m_EnumerationThread.Join();
                m_EnumerationThread = null;
            }
        }

        private void EnumerateDevices()
        {
            using (var enumerate = m_Context.enumerate_new())
            {
                if (enumerate == null || enumerate.IsInvalid)
                {
                    Logging.LastError("Failed to create device enumerator");
                    return;
                }

                // TODO: commented out for investigation
                // if (enumerate.add_match_subsystem("input") < 0 ||
                //     enumerate.add_match_subsystem("hidraw") < 0)
                // {
                //     Logging.LastError("Failed to add filter to enumerator");
                //     return;
                // }

                if (enumerate.scan_devices() < 0)
                {
                    Logging.LastError("Failed to scan devices for enumeration");
                    return;
                }

                for (var entry = enumerate.get_list_entry(); !entry.IsInvalid; entry = entry.get_next())
                {
                    var syspath = entry.get_name();
                    try
                    {
                        using (var device = m_Context.device_new_from_syspath(syspath.ToPtr()))
                        {
                            if (device == null || device.IsInvalid)
                            {
                                continue;
                            }

                            if (!device.get_is_initialized() || !DeviceAllowed(device))
                            {
                                continue;
                            }

                            OnDeviceAdded(device);
                        }
                    }
                    finally
                    {
                        syspath.Dispose();
                    }
                }
            }
        }

        private void MonitorThread()
        {
            const int errorThreshold = 5;

            var poll = new pollfd(m_MonitorFd, Fcntl.POLLIN);

            int errorCount = 0;
            while (!m_ThreadStop.WaitOne(100) && errorCount < errorThreshold)
            {
                // Wait for events
                int result;
                unsafe { result = Fcntl.poll(&poll, 0); }

                if (result < 0)
                {
                    Logging.LastError($"Error while polling for device monitor events ({errorCount}/{errorThreshold})");
                    errorCount++;
                    continue;
                }

                if (result > 0) // Event received
                {
                    using (var device = m_Monitor.receive_device())
                    {
                        if (device == null || device.IsInvalid)
                        {
                            Logging.LastError("Failed to get changed device");
                            errorCount++;
                            continue;
                        }

                        if (!DeviceAllowed(device))
                        {
                            continue;
                        }

                        switch (device.get_action().ToString())
                        {
                            case "add":
                            {
                                OnDeviceAdded(device);
                                break;
                            }
                            case "remove":
                            {
                                OnDeviceRemoved(device);
                                break;
                            }
                        }
                    }
                }

                errorCount = 0;
            }

            Logging.Verbose("Stopping udev monitor thread");
        }

        private static bool DeviceAllowed(udev_device device)
        {
            switch (device.get_subsystem().ToString())
            {
                // TODO: these are here for debugging
                case "bluetooth":
                case "hid":
                case "usb":
                case "usbmisc":
                {
                    return true;
                }
                case "hidraw":
                {
                    return true;
                }
                case "input":
                {
                    // We only want evdev devices here, not joydev or any other kinds of input devices
                    if (!device.get_sysname().ToString().StartsWith("event"))
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        private static string MakeDeviceInfo(udev_device device)
        {
            return
                $"subsystem: {device.get_subsystem().ToString()}, " +
                $"devtype: {device.get_devtype().ToString()}, " +
                $"driver: {device.get_driver().ToString()}, " +
                $"devnode: {device.get_devnode().ToString()}, " +
                $"sysname: {device.get_sysname().ToString()}, " +
                $"devpath: {device.get_devpath().ToString()}, " +
                $"syspath: {device.get_syspath().ToString()}, " +
                $"sysnum: {device.get_sysnum().ToString()}";
        }

        private void PrintDeviceTree(udev_device device)
        {
            Logging.Verbose("udev device added: " + MakeDeviceInfo(device));

            var parents = new List<udev_device>();
            for (var parent = device.get_parent(); parent != null && !parent.IsInvalid; parent = parent.get_parent())
            {
                parents.Add(parent);
            }

            Logging.Verbose("- parents:");

            for (int i = parents.Count - 1; i >= 0; i--)
            {
                using (var parent = parents[i])
                {
                    Logging.Verbose("  - " + MakeDeviceInfo(parent));
                }
            }
            parents.Clear();

            using (var parent = device.get_parent())
            {
                if (parent != null && !parent.IsInvalid)
                {
                    Logging.Verbose("- siblings:");

                    using (var enumerate = m_Context.enumerate_new())
                    {
                        if (enumerate != null &&
                            !enumerate.IsInvalid &&
                            enumerate.add_match_parent(parent) >= 0 &&
                            enumerate.scan_devices() >= 0)
                        {
                            for (var entry = enumerate.get_list_entry(); !entry.IsInvalid; entry = entry.get_next())
                            {
                                var syspath = entry.get_name();
                                try
                                {
                                    using (var sibling = m_Context.device_new_from_syspath(syspath.ToPtr()))
                                    {
                                        if (sibling != null && !sibling.IsInvalid && sibling.get_parent() == parent)
                                        {
                                            Logging.Verbose("  - " + MakeDeviceInfo(sibling));
                                        }
                                    }
                                }
                                finally
                                {
                                    syspath.Dispose();
                                }
                            }
                        }
                    }
                }
            }

            Logging.Verbose("- children:");

            using (var enumerate = m_Context.enumerate_new())
            {
                if (enumerate != null &&
                    !enumerate.IsInvalid &&
                    enumerate.add_match_parent(device) >= 0 &&
                    enumerate.scan_devices() >= 0)
                {
                    for (var entry = enumerate.get_list_entry(); !entry.IsInvalid; entry = entry.get_next())
                    {
                        var syspath = entry.get_name();
                        try
                        {
                            using (var child = m_Context.device_new_from_syspath(syspath.ToPtr()))
                            {
                                if (child != null && !child.IsInvalid && child != device)
                                {
                                    Logging.Verbose("  - " + MakeDeviceInfo(child));
                                }
                            }
                        }
                        finally
                        {
                            syspath.Dispose();
                        }
                    }
                }
            }
        }

        private void OnDeviceAdded(udev_device device)
        {
            PrintDeviceTree(device);
        }

        private void OnDeviceRemoved(udev_device device)
        {
            Logging.Verbose($"udev device removed: " + MakeDeviceInfo(device));
        }
    }
}
#endif
