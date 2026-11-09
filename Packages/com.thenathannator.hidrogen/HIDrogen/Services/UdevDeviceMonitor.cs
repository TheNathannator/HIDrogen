#if UNITY_STANDALONE_LINUX
using System;
using System.Collections.Generic;
using System.Threading;
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

        private Thread m_EnumerationThread;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        private readonly HashSet<string> m_TrackedDevpaths = new HashSet<string>();

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

        public void Update()
        {
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

                errorCount = 0;
            }

            Logging.Verbose("Stopping udev monitor thread");
        }

        private void OnDeviceAdded(udev_device _device)
        {
#if HIDROGEN_VERBOSE_LOGGING
            Udev.PrintDeviceTree(m_Context, _device);
#endif

            // A general note:
            // Because monitoring is started before the initial enumeration is, it's possible that
            // the same device might come through via both sources if it is connected during enumeration.
            // We also need to handle the same physical device having multiple devnodes under different subsystems,
            // e.g. HID devices will be available under both `input` and `hidraw`.
            // To handle both of these, we track the devpath of all added devices, and bail if it's already been added.

            lock (m_TrackedDevpaths)
            {
                // Subsystems are prioritized in the following order:
                // 1. hidraw
                // 2. input (evdev)
                // TODO: USB

                // hidraw
                using (var hidraw = FindHidRaw(_device))
                {
                    var backend = BackendManager.TryGetBackend<HidRawBackend>();
                    if (TryProbeAdd(hidraw, backend))
                    {
                        return;
                    }

                    // Probing may fail due to the device not being able to be opened,
                    // fall through to evdev
                }

                // input
                using (var evdev = FindEvdev(_device))
                {
                    var backend = BackendManager.TryGetBackend<EvdevBackend>();
                    if (TryProbeAdd(evdev, backend))
                    {
                        return;
                    }
                }
            }
        }

        private void OnDeviceRemoved(udev_device _device)
        {
#if HIDROGEN_VERBOSE_LOGGING
            Logging.Verbose($"udev device removed: " + Udev.MakeDeviceInfo(_device));
#endif

            // See comments in OnDeviceAdded for notes

            lock (m_TrackedDevpaths)
            {
                using (var hidraw = FindHidRaw(_device))
                {
                    var backend = BackendManager.TryGetBackend<HidRawBackend>();
                    if (TryProbeRemove(hidraw, backend))
                    {
                        return;
                    }
                }

                using (var evdev = FindEvdev(_device))
                {
                    var backend = BackendManager.TryGetBackend<EvdevBackend>();
                    if (TryProbeRemove(evdev, backend))
                    {
                        return;
                    }
                }
            }
        }

        private bool TryProbeAdd(udev_device device, IUdevBackend backend)
        {
            if (device != null && !device.IsInvalid)
            {
                string devpath = device.get_devpath().ToString();
                if (!m_TrackedDevpaths.Contains(devpath) && backend != null && backend.ProbeDeviceAdd(m_Context, device))
                {
                    m_TrackedDevpaths.Add(devpath);
                    return true;
                }
            }

            return false;
        }

        private bool TryProbeRemove(udev_device device, IUdevBackend backend)
        {
            if (device != null && !device.IsInvalid)
            {
                string devpath = device.get_devpath().ToString();
                if (m_TrackedDevpaths.Contains(devpath) && backend != null && backend.ProbeDeviceRemove(m_Context, device))
                {
                    m_TrackedDevpaths.Remove(devpath);
                    return true;
                }
            }

            return false;
        }

        private udev_device FindHidRaw(udev_device device)
        {
            if (device.get_subsystem().ToString() == Udev.SubsystemHidRaw)
            {
                // Clone to simplify disposal handling
                return device.add_ref();
            }

            // Search upwards for the root `hid` device
            using (var hidRoot = device.get_parent_with_subsystem_devtype(Udev.SubsystemHid, null))
            {
                if (hidRoot == null || hidRoot.IsInvalid)
                {
                    return null;
                }

                // Then from there, search children to find the corresponding `hidraw` device
                using (var enumerate = m_Context.enumerate_new())
                {
                    if (enumerate == null ||
                        enumerate.IsInvalid ||
                        enumerate.add_match_parent(hidRoot) < 0 ||
                        enumerate.add_match_subsystem(Udev.SubsystemHidRaw) < 0 ||
                        enumerate.scan_devices() < 0)
                    {
                        return null;
                    }

                    var entry = enumerate.get_list_entry();
                    if (entry.IsInvalid)
                    {
                        return null;
                    }

                    var entryPath = entry.get_name();
                    try
                    {
                        var pathPtr = entryPath.ToPtr();
                        if (pathPtr == IntPtr.Zero)
                        {
                            return null;
                        }

                        // Found
                        return m_Context.device_new_from_syspath(pathPtr);
                    }
                    finally
                    {
                        entryPath.Dispose();
                    }
                }
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
