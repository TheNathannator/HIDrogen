#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Globalization;
using HIDrogen.Imports.Linux;
using HIDrogen.Imports.Posix;
using HIDrogen.Imports.Posix.Sys;
using Unity.Collections.LowLevel.Unsafe;

namespace HIDrogen.Backend
{
    internal partial class HidApiBackend
    {
        private Udev m_Udev;

        partial void PlatformInitialize()
        {
            m_Udev = new Udev();
        }

        partial void PlatformDispose()
        {
            m_Udev.Dispose();
        }

        // Returns false if falling back to hidapi polling is necessary, returns true on exit
        partial void PlatformMonitor(ref bool success)
        {
            // Set up device monitor
            var monitor = m_Udev.monitor_new_from_netlink("udev");
            if (monitor == null || monitor.IsInvalid)
            {
                Logging.InteropError("Failed to initialize device monitor");
                success = false;
                return;
            }

            using (monitor)
            {
                // Add filter for hidraw devices
                if (monitor.filter_add_match_subsystem_devtype("hidraw", null) < 0)
                {
                    Logging.InteropError("Failed to add filter to device monitor");
                    success = false;
                    return;
                }

                // Enable monitor
                if (monitor.enable_receiving() < 0)
                {
                    Logging.InteropError("Failed to enable receiving on device monitor");
                    success = false;
                    return;
                }

                // Get monitor file descriptor
                var fd = monitor.get_fd();
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
                        int result = Fcntl.poll(Fcntl.POLLIN, 0, fd);
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
                        var dev = monitor.receive_device();
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
            fd = Fcntl.open(path, Fcntl.O_RDONLY);
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

            if (Ioctl.ioctl(fd, HidRaw.HIDIOCGRDESC, &descBuffer) < 0)
                return false;

            UnsafeUtility.MemCpy(buffer, descBuffer.value, descBuffer.size);
            bytesWritten = descBuffer.size;
            return true;
        }

        private static unsafe bool PlatformGetDescriptorSize(fd fd, out int size)
        {
            int tempSize = default;
            if (Ioctl.ioctl(fd, HidRaw.HIDIOCGRDESCSIZE, &tempSize) < 0)
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
            if (Stat.statx(0, path, 0, 0, out var pathStats) < 0)
            {
                Logging.InteropError("Error getting device type info");
                success = false;
                return;
            }

            var hidrawDevice = m_Udev.device_new_from_devnum(pathStats.DeviceType, pathStats.DeviceNumber);
            if (hidrawDevice == null || hidrawDevice.IsInvalid)
            {
                Logging.InteropError("Failed to get hidraw device instance");
                success = false;
                return;
            }

            // Grab the root parent hid device that both the hidraw and input devices share
            var hidDevice = hidrawDevice.get_parent_with_subsystem_devtype("hid", null);
            if (hidDevice == null || hidDevice.IsInvalid)
            {
                Logging.InteropError("Failed to get HID device instance");
                success = false;
                return;
            }

            using (hidDevice)
            {
                // Find the input device by scanning the parent's children for input devices, and grabbing the first one
                var enumerate = m_Udev.enumerate_new();
                if (enumerate == null || enumerate.IsInvalid)
                {
                    Logging.InteropError("Failed to make udev enumeration");
                    success = false;
                    return;
                }

                using (enumerate)
                {
                    // Scan for devices under the 'input' subsystem
                    if (enumerate.add_match_parent(hidDevice) < 0 ||
                        enumerate.add_match_subsystem("input") < 0 ||
                        enumerate.scan_devices() < 0)
                    {
                        Logging.InteropError("Failed to scan udev devices");
                        success = false;
                        return;
                    }

                    // Get the first device found
                    Udev.ListEntry entry;
                    string entryPath;
                    Udev.Device inputDevice;
                    if ((entry = enumerate.get_list_entry()).IsInvalid ||
                        string.IsNullOrEmpty(entryPath = entry.get_name()) ||
                        (inputDevice = m_Udev.device_new_from_syspath(entryPath)) == null ||
                        inputDevice.IsInvalid)
                    {
                        Logging.InteropError("Failed to get input device instance");
                        success = false;
                        return;
                    }

                    // Grab the version number from the found device
                    using (inputDevice)
                    {
                        string versionStr = inputDevice.get_sysattr_value("id/version");
                        success = ushort.TryParse(versionStr, NumberStyles.HexNumber, null, out version);
                    }
                }
            }
        }
    }
}
#endif