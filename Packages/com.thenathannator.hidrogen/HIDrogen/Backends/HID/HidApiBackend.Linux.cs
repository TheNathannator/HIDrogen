#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Globalization;
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

        partial void PlatformInitialize()
        {
            // Initialize udev
            m_Udev = udev_new();
            if (m_Udev == null || m_Udev.IsInvalid)
            {
                Logging.InteropError("Failed to initialize udev");
                throw new Exception("Failed to initialize udev!");
            }
        }

        partial void PlatformDispose()
        {
            m_Udev.Dispose();
        }

        // Returns false if falling back to hidapi polling is necessary, returns true on exit
        partial void PlatformMonitor(ref bool success)
        {
            // Set up device monitor
            var monitor = udev_monitor_new_from_netlink(m_Udev, "udev");
            if (monitor == null || monitor.IsInvalid)
            {
                Logging.InteropError("Failed to initialize device monitor");
                success = false;
                return;
            }

            using (monitor)
            {
                // Add filter for hidraw devices
                if (udev_monitor_filter_add_match_subsystem_devtype(monitor, "hidraw", null) < 0)
                {
                    Logging.InteropError("Failed to add filter to device monitor");
                    success = false;
                    return;
                }

                // Enable monitor
                if (udev_monitor_enable_receiving(monitor) < 0)
                {
                    Logging.InteropError("Failed to enable receiving on device monitor");
                    success = false;
                    return;
                }

                // Get monitor file descriptor
                var fd = udev_monitor_get_fd(monitor);
                if (fd == null || fd.IsInvalid)
                {
                    Logging.InteropError("Failed to get device monitor file descriptor");
                    success = false;
                    return;
                }

                using (fd)
                {
                    int errorCount = 0;
                    const int errorThreshold = 5;
                    while (!m_ThreadStop.WaitOne(1000))
                    {
                        // Check if any events are available
                        int result = poll(POLLIN, 0, fd);
                        if (result < 0) // Error
                        {
                            errorCount++;
                            Logging.InteropError("Error while polling for device monitor events");
                            if (errorCount >= errorThreshold)
                            {
                                Logging.Error($"Error threshold reached, stopping udev monitoring");
                                success = false;
                                return;
                            }
                            continue;
                        }

                        errorCount = 0;
                        if (result == 0) // No events
                            continue;

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

            success = true;
        }

        private static bool PlatformOpenHandle(string path, out fd fd)
        {
            fd = open(path, O_RDONLY);
            return fd != null && !fd.IsInvalid;
        }

        partial void PlatformGetDescriptor(string path, ref byte[] descriptor, ref bool success)
        {
            if (!PlatformOpenHandle(path, out var fd))
            {
                success = false;
                return;
            }

            using (fd)
            {
                if (!PlatformGetDescriptorSize(fd, out int descriptorSize))
                {
                    success = false;
                    return;
                }

                descriptor = new byte[descriptorSize];
                unsafe
                {
                    fixed (byte* ptr = descriptor)
                        success = PlatformGetDescriptor(fd, ptr, descriptor.Length, out _);
                }
            }
        }

        unsafe partial void PlatformGetDescriptor(string path, void* buffer, int bufferLength,
            ref int bytesWritten, ref bool success)
        {
            if (!PlatformOpenHandle(path, out var fd))
            {
                success = false;
                return;
            }

            using (fd)
            {
                success = PlatformGetDescriptor(fd, buffer, bufferLength, out bytesWritten);
            }
        }

        partial void PlatformGetDescriptorSize(string path, ref int size, ref bool success)
        {
            if (!PlatformOpenHandle(path, out var fd))
            {
                success = false;
                return;
            }

            using (fd)
            {
                success = PlatformGetDescriptorSize(fd, out size);
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

        partial void PlatformGetVersionNumber(string path, ref ushort version, ref bool success)
        {
            version = default;

            // Bluetooth devices won't have this filled in so we need to do it ourselves.
            // Use statx to grab the device type and number, and then construct a udev device
            if (statx(0, path, 0, 0, out var pathStats) < 0)
            {
                Logging.InteropError("Error getting device type info");
                success = false;
                return;
            }

            var hidrawDevice = udev_device_new_from_devnum(m_Udev, pathStats.DeviceType, pathStats.DeviceNumber);
            if (hidrawDevice == null || hidrawDevice.IsInvalid)
            {
                Logging.InteropError("Failed to get hidraw device instance");
                success = false;
                return;
            }

            // Grab the root parent hid device that both the hidraw and input devices share
            var hidDevice = udev_device_get_parent_with_subsystem_devtype(hidrawDevice, "hid", null);
            if (hidDevice == null || hidDevice.IsInvalid)
            {
                Logging.InteropError("Failed to get HID device instance");
                success = false;
                return;
            }

            using (hidDevice)
            {
                // Find the input device by scanning the parent's children for input devices, and grabbing the first one
                var enumerate = udev_enumerate_new(m_Udev);
                if (enumerate == null || enumerate.IsInvalid)
                {
                    Logging.InteropError("Failed to make udev enumeration");
                    success = false;
                    return;
                }

                using (enumerate)
                {
                    // Scan for devices under the 'input' subsystem
                    if (udev_enumerate_add_match_parent(enumerate, hidDevice) < 0 ||
                        udev_enumerate_add_match_subsystem(enumerate, "input") < 0 ||
                        udev_enumerate_scan_devices(enumerate) < 0)
                    {
                        Logging.InteropError("Failed to scan udev devices");
                        success = false;
                        return;
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
                        success = false;
                        return;
                    }

                    // Grab the version number from the found device
                    using (inputDevice)
                    {
                        string versionStr = udev_device_get_sysattr_value(inputDevice, "id/version");
                        success = ushort.TryParse(versionStr, NumberStyles.HexNumber, null, out version);
                    }
                }
            }
        }
    }
}
#endif