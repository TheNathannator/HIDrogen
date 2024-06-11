#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using HIDrogen.Imports;
using Unity.Collections.LowLevel.Unsafe;

namespace HIDrogen.Backend
{
    using static Libc;
    using static HidRaw;
    using static Udev;

    internal partial class HidApiBackend
    {
        private udev m_Udev;

        private void PlatformInitialize()
        {
            // Initialize udev
            m_Udev = udev_new();
            if (m_Udev == null || m_Udev.IsInvalid)
            {
                Logging.InteropError("Failed to initialize udev");
                throw new Exception("Failed to initialize udev!");
            }
        }

        private void PlatformDispose()
        {
            m_Udev.Dispose();
        }

        // Returns false if falling back to hidapi polling is necessary, returns true on exit
        private bool PlatformMonitor()
        {
            // Set up device monitor
            var monitor = udev_monitor_new_from_netlink(m_Udev, "udev");
            if (monitor == null || monitor.IsInvalid)
            {
                Logging.InteropError("Failed to initialize device monitor");
                return false;
            }

            using (monitor)
            {
                // Add filter for hidraw devices
                if (udev_monitor_filter_add_match_subsystem_devtype(monitor, "hidraw", null) < 0)
                {
                    Logging.InteropError("Failed to add filter to device monitor");
                    return false;
                }

                // Enable monitor
                if (udev_monitor_enable_receiving(monitor) < 0)
                {
                    Logging.InteropError("Failed to enable receiving on device monitor");
                    return false;
                }

                // Get monitor file descriptor
                var fd = udev_monitor_get_fd(monitor);
                if (fd == null || fd.IsInvalid)
                {
                    Logging.InteropError("Failed to get device monitor file descriptor");
                    return false;
                }

                using (fd)
                {
                    int errorCount = 0;
                    const int errorThreshold = 5;
                    while (!m_ThreadStop.WaitOne(1000))
                    {
                        // Check if any events are available
                        int result = poll(POLLIN, 0, fd);
                        if (result <= 0) // No events, or an error occured
                        {
                            if (result < 0) // Error
                            {
                                errorCount++;
                                Logging.InteropError("Error while polling for device monitor events");
                                if (errorCount >= errorThreshold)
                                {
                                    Logging.Error($"Error threshold reached, stopping udev monitoring");
                                    return false;
                                }
                                continue;
                            }
                            errorCount = 0;
                            continue;
                        }
                        errorCount = 0;

                        // Get device to clear it from the event buffer
                        var dev = udev_monitor_receive_device(monitor);
                        if (dev == null || dev.IsInvalid)
                        {
                            Logging.InteropError("Failed to get changed device");
                            continue;
                        }

                        using (dev)
                        {
                            // Re-enumerate devices from hidapi, as we need the info it gives
                            EnumerateDevices();
                        }
                    }
                }
            }

            return true;
        }

        private static bool PlatformOpenHandle(string path, out fd fd)
        {
            fd = open(path, O_RDONLY);
            return fd != null && !fd.IsInvalid;
        }

        internal static unsafe bool PlatformGetDescriptor(string path, out byte[] descriptor)
        {
            descriptor = null;
            if (!PlatformOpenHandle(path, out var fd))
                return false;

            using (fd)
            {
                if (!PlatformGetDescriptorSize(fd, out int descriptorSize))
                    return false;

                descriptor = new byte[descriptorSize];
                fixed (byte* ptr = descriptor)
                {
                    return PlatformGetDescriptor(fd, ptr, descriptor.Length, out _);
                }
            }
        }

        internal static unsafe bool PlatformGetDescriptor(string path, void* buffer, int bufferLength, out int bytesWritten)
        {
            bytesWritten = 0;
            if (buffer == null)
                return false;

            if (!PlatformOpenHandle(path, out var fd))
                return false;

            using (fd)
            {
                return PlatformGetDescriptor(fd, buffer, bufferLength, out bytesWritten);
            }
        }

        internal static unsafe bool PlatformGetDescriptorSize(string path, out int size)
        {
            size = default;
            if (!PlatformOpenHandle(path, out var fd))
                return false;

            using (fd)
            {
                return PlatformGetDescriptorSize(fd, out size);
            }
        }

        private static unsafe bool PlatformGetDescriptor(fd fd, void* buffer, int bufferLength, out int bytesWritten)
        {
            bytesWritten = 0;
            hidraw_report_descriptor descBuffer = default;
            if (!PlatformGetDescriptorSize(fd, out descBuffer.size) || bufferLength < descBuffer.size)
                return false;

            if (ioctl(fd, HIDIOCGRDESC, &descBuffer) < 0)
                return false;

            UnsafeUtility.MemCpy(buffer, descBuffer.value, descBuffer.size);
            bytesWritten = descBuffer.size;
            return true;
        }

        private static unsafe bool PlatformGetDescriptorSize(fd fd, out int size)
        {
            int tempSize = default;
            if (ioctl(fd, HIDIOCGRDESCSIZE, &tempSize) < 0)
            {
                size = default;
                return false;
            }

            size = tempSize;
            return true;
        }

        private bool PlatformGetVersionNumber(string path, out ushort version)
        {
            version = default;

            // Bluetooth devices won't have this filled in so we need to do it ourselves.
            // Use statx to grab the device type and number, and then construct a udev device
            if (statx(0, path, 0, 0, out var pathStats) < 0)
            {
                Logging.InteropError("Error getting device type info");
                return false;
            }

            var hidrawDevice = udev_device_new_from_devnum(m_Udev, pathStats.DeviceType, pathStats.DeviceNumber);
            if (hidrawDevice == null || hidrawDevice.IsInvalid)
            {
                Logging.InteropError("Failed to get hidraw device instance");
                return false;
            }

            // Grab the root parent hid device that both the hidraw and input devices share
            var hidDevice = udev_device_get_parent_with_subsystem_devtype(hidrawDevice, "hid", null);
            if (hidDevice == null || hidDevice.IsInvalid)
            {
                Logging.InteropError("Failed to get HID device instance");
                return false;
            }

            using (hidDevice)
            {
                // Find the input device by scanning the parent's children for input devices, and grabbing the first one
                var enumerate = udev_enumerate_new(m_Udev);
                if (enumerate == null || enumerate.IsInvalid)
                {
                    Logging.InteropError("Failed to make udev enumeration");
                    return false;
                }

                using (enumerate)
                {
                    // Scan for devices under the 'input' subsystem
                    if (udev_enumerate_add_match_parent(enumerate, hidDevice) < 0 ||
                        udev_enumerate_add_match_subsystem(enumerate, "input") < 0 ||
                        udev_enumerate_scan_devices(enumerate) < 0)
                    {
                        Logging.InteropError("Failed to scan udev devices");
                        return false;
                    }

                    // Get the first device found
                    IntPtr entry;
                    string entryPath;
                    udev_device inputDevice;
                    if ((entry = udev_enumerate_get_list_entry(enumerate)) == IntPtr.Zero ||
                        string.IsNullOrEmpty(entryPath = udev_list_entry_get_name(entry)) ||
                        (inputDevice = udev_device_new_from_syspath(m_Udev, entryPath)) == null ||
                        inputDevice.IsInvalid)
                    {
                        Logging.InteropError("Failed to get input device instance");
                        return false;
                    }

                    // Grab the version number from the found device
                    using (inputDevice)
                    {
                        string versionStr = udev_device_get_sysattr_value(inputDevice, "id/version");
                        version = Convert.ToUInt16(versionStr, 16);
                        return true;
                    }
                }
            }
        }
    }
}
#endif