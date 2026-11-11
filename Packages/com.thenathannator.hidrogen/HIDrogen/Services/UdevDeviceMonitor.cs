#if UNITY_STANDALONE_LINUX
using System;
using HIDrogen.Backend;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;

// TODO: Avoid allocations from string unmarshalling where reasonable

namespace HIDrogen.Services
{
    internal interface IUdevBackend
    {
        bool ProbeDeviceAdd(udev_context udev, udev_device device);
        bool ProbeDeviceRemove(udev_context udev, udev_device device);
    }

    internal class UdevDeviceMonitor : ICustomInputService
    {
        private Udev m_Udev;
        private udev_context m_Context;
        private udev_monitor m_Monitor;
        private fd m_MonitorFd;

        public UdevDeviceMonitor()
        {
            m_Udev = new Udev();
        }

        public void Dispose()
        {
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

            m_Context?.Dispose();
            m_Context = null;
        }

        public void Update()
        {
            PollMonitor();
        }

        private void StartMonitor()
        {
            m_Monitor = m_Context.monitor_new_from_netlink("udev");
            if (m_Monitor == null || m_Monitor.IsInvalid)
            {
                throw new Exception(Logging.MakeLastErrorMessage("Failed to create device monitor"));
            }

            if (m_Monitor.filter_add_match_subsystem_devtype(Udev.SubsystemInput, null) < 0 ||
                m_Monitor.filter_add_match_subsystem_devtype(Udev.SubsystemHidRaw, null) < 0)
            {
                m_Monitor.Dispose();
                throw new Exception(Logging.MakeLastErrorMessage("Failed to add filters to device monitor"));
            }

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
        }

        private void StopMonitor()
        {
            m_MonitorFd?.Dispose();
            m_MonitorFd = null;

            m_Monitor?.Dispose();
            m_Monitor = null;
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

                if (enumerate.add_match_subsystem(Udev.SubsystemInput) < 0 ||
                    enumerate.add_match_subsystem(Udev.SubsystemHidRaw) < 0)
                {
                    Logging.LastError("Failed to add filter to enumerator");
                    return;
                }

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

                            if (!device.get_is_initialized())
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

        private void PollMonitor()
        {
            if (m_Monitor == null || m_MonitorFd == null)
            {
                return;
            }

            while (true)
            {
                // Wait for events
                var poll = new pollfd(m_MonitorFd, Fcntl.POLLIN);
                int result = Fcntl.poll(ref poll, 0);
                if (result < 0)
                {
                    Logging.LastError($"Error while polling for device monitor events");
                    break;
                }

                if (result == 0)
                {
                    // No more events to receive
                    break;
                }

                using (var device = m_Monitor.receive_device())
                {
                    if (device == null || device.IsInvalid)
                    {
                        Logging.LastError("Failed to get changed device");
                        continue;
                    }

                    switch (device.get_action().ToString())
                    {
                        case Udev.EventAdded:
                        {
                            OnDeviceAdded(device);
                            break;
                        }
                        case Udev.EventRemoved:
                        {
                            OnDeviceRemoved(device);
                            break;
                        }
                    }
                }
            }

            Logging.Verbose("Stopping udev monitor thread");
        }

        private void OnDeviceAdded(udev_device device)
        {
#if HIDROGEN_VERBOSE_LOGGING
            Udev.PrintDeviceTree(m_Context, device);
#endif

            // Subsystems are prioritized in the following order:
            // 1. USB (TODO)
            // 2. hidraw
            // 3. input (evdev)
            // Probing for one subsystem may fail due to things such as device permission issues,
            // so we try each in sequence until one succeeds

            // TODO
            // var usb = BackendManager.TryGetBackend<UsbManager>();
            // if (usb.ProbeDeviceAdd(m_Context, device))
            // {
            //     return;
            // }

            var hidraw = BackendManager.TryGetBackend<HidRawBackend>();
            if (hidraw != null && hidraw.ProbeDeviceAdd(m_Context, device))
            {
                return;
            }

            var evdev = BackendManager.TryGetBackend<EvdevBackend>();
            if (evdev != null && evdev.ProbeDeviceAdd(m_Context, device))
            {
                return;
            }
        }

        private void OnDeviceRemoved(udev_device device)
        {
#if HIDROGEN_VERBOSE_LOGGING
            Logging.Verbose($"udev device removed: " + Udev.MakeDeviceInfo(device));
#endif

            // TODO
            // var usb = BackendManager.TryGetBackend<UsbManager>();
            // if (usb.ProbeDeviceRemove(m_Context, device))
            // {
            //     return;
            // }

            var hidraw = BackendManager.TryGetBackend<HidRawBackend>();
            if (hidraw.ProbeDeviceRemove(m_Context, device))
            {
                return;
            }

            var evdev = BackendManager.TryGetBackend<EvdevBackend>();
            if (evdev.ProbeDeviceRemove(m_Context, device))
            {
                return;
            }
        }

        private udev_device FindEvdev(udev_device device)
        {
            if (device.get_subsystem().ToString() == Udev.SubsystemInput)
            {
                // We only want evdev devices here, not joydev or any other kinds of input devices
                if (!device.get_sysname().ToString().StartsWith("event"))
                {
                    return null;
                }

                // Clone to simplify disposal handling
                return device.add_ref();
            }

            // Don't try to search for the `input` node, let monitoring/enumeration give it to us instead
            return null;
        }
    }
}
#endif
