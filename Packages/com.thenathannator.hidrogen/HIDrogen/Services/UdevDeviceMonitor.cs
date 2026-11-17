#if UNITY_STANDALONE_LINUX
using System;
using HIDrogen.Backend.Linux;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;

// TODO: Avoid allocations from string unmarshalling where reasonable

namespace HIDrogen.Services.Linux
{
    internal interface IUdevBackend
    {
        bool ProbeDeviceAdd(udev_context udev, udev_device device);
        void ProbeDeviceRemove(udev_context udev, udev_device device);
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

            int result;
            if ((result = m_Monitor.filter_add_match_subsystem_devtype(Udev.SubsystemInput, null)) < 0 ||
                (result = m_Monitor.filter_add_match_subsystem_devtype(Udev.SubsystemHidRaw, null)) < 0)
            {
                m_Monitor.Dispose();
                throw new Exception(Logging.MakeErrnoMessage("Failed to add filters to device monitor", result));
            }

            result = m_Monitor.enable_receiving();
            if (result < 0)
            {
                m_Monitor.Dispose();
                throw new Exception(Logging.MakeErrnoMessage("Failed to start device monitor", result));
            }

            m_MonitorFd = m_Monitor.get_fd();
            if (m_MonitorFd == null || m_MonitorFd.IsInvalid)
            {
                m_Monitor.Dispose();
                throw new Exception(m_MonitorFd != null
                    ? Logging.MakeErrnoMessage("Failed to get device monitor file descriptor", m_MonitorFd.DangerousGetFileDescriptor())
                    : Logging.MakeLastErrorMessage("Failed to get device monitor file descriptor")
                );
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

                int result;
                if ((result = enumerate.add_match_subsystem(Udev.SubsystemInput)) < 0 ||
                    (result = enumerate.add_match_subsystem(Udev.SubsystemHidRaw)) < 0)
                {
                    Logging.Errno("Failed to add filter to enumerator", result);
                    return;
                }

                result = enumerate.scan_devices();
                if (result < 0)
                {
                    Logging.Errno("Failed to scan devices for enumeration", result);
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
        }

        private void OnDeviceAdded(udev_device device)
        {
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
            // BackendManager.TryGetBackend<UsbManager>()?.ProbeDeviceRemove(m_Context, device); // TODO
            BackendManager.TryGetBackend<HidRawBackend>()?.ProbeDeviceRemove(m_Context, device);
            BackendManager.TryGetBackend<EvdevBackend>()?.ProbeDeviceRemove(m_Context, device);
        }
    }
}
#endif
