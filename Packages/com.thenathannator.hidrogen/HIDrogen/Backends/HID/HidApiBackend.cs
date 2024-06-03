using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using HIDrogen.Imports;
using HIDrogen.LowLevel;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.LowLevel;

#if !UNITY_EDITOR
using UnityEngine;
#endif

using Debug = UnityEngine.Debug;

namespace HIDrogen.Backend
{
    using static HidApi;

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    using static Libc;
    using static Udev;
#endif

    /// <summary>
    /// Provides input through the hidapi library.
    /// </summary>
    internal static class HidApiBackend
    {
        // Keeps track of available devices
        private static readonly ConcurrentDictionary<int, HidApiDevice> s_DeviceLookup = new ConcurrentDictionary<int, HidApiDevice>();

        // Queue for new devices; they must be added on the main thread
        private static readonly ConcurrentBag<hid_device_info> s_AdditionQueue = new ConcurrentBag<hid_device_info>();

        // Workaround for not being able to remove from a collection while enumerating it
        private static readonly List<HidApiDevice> s_RemovalQueue = new List<HidApiDevice>();

        // Processing threads
        private static Thread s_EnumerationThread;
        private static Thread s_ReadingThread;
        private static readonly EventWaitHandle s_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);

        // Input buffers
        // We use a custom buffering implementation because the built-in implementation is not friendly to managed threads,
        // despite what the docs for InputSystem.QueueEvent/QueueStateEvent may claim, so we need to flush events on the main thread.
        private static readonly SlimEventBuffer[] s_InputBuffers = new SlimEventBuffer[2];
        private static readonly object s_BufferLock = new object();
        private static int s_CurrentBuffer = 0;

        internal static unsafe bool Initialize()
        {
            Logging.Verbose("Initializing hidapi backend");

            // Initialize hidapi
            int result = hid_init();
            if (result < 0)
            {
                Logging.InteropError("Failed to initialize hidapi");
                return false;
            }

            // Register events
            InputSystem.onBeforeUpdate += Update;
            InputSystem.onDeviceCommand += DeviceCommand;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += Uninitialize;
            EditorApplication.quitting += Uninitialize;
#else
            Application.quitting += Uninitialize;
#endif

            // Initialize event buffers
            for (int i = 0; i < s_InputBuffers.Length; i++)
            {
                s_InputBuffers[i] = new SlimEventBuffer();
            }

            // Start threads
            s_EnumerationThread = new Thread(DeviceDiscoveryThread) { IsBackground = true };
            s_EnumerationThread.Start();
            // Read thread is started when adding a device

            return true;
        }

        private static unsafe void Uninitialize()
        {
            Logging.Verbose("Uninitializing hidapi backend");

            // Stop threads
            s_ThreadStop.Set();
            s_EnumerationThread?.Join();
            s_EnumerationThread = null;
            s_ReadingThread?.Join();
            s_ReadingThread = null;

            // Unregister events
            InputSystem.onBeforeUpdate -= Update;
            InputSystem.onDeviceCommand -= DeviceCommand;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Uninitialize;
            EditorApplication.quitting -= Uninitialize;
#else
            Application.quitting -= Uninitialize;
#endif

            // Close devices
            foreach (var device in s_DeviceLookup.Values)
            {
                device.RemoveImmediate();
            }

            // Dispose event buffers
            for (int i = 0; i < s_InputBuffers.Length; i++)
            {
                s_InputBuffers[i].Dispose();
            }

            // Free hidapi
            int result = hid_exit();
            if (result < 0)
            {
                Logging.InteropError("Error when freeing hidapi");
            }
        }

        private static void Update()
        {
            FlushEventBuffer();
            HandleRemovalQueue();
            HandleAdditionQueue();
        }

        private static unsafe long? DeviceCommand(InputDevice device, InputDeviceCommand* command)
        {
            if (device == null || device.description.interfaceName != HidApiDevice.InterfaceName)
                return null;
            if (command == null)
                return InputDeviceCommand.GenericFailure;

            if (!s_DeviceLookup.TryGetValue(device.deviceId, out var entry))
            {
                Logging.Warning($"Could not find hidapi device for device {device} (ID {device.deviceId})!");
                return null;
            }

            Logging.Verbose($"Executing command for device {device} (ID {device.deviceId})");
            return entry.ExecuteCommand(command);
        }

        private static void DeviceDiscoveryThread()
        {
            Logging.Verbose("Starting device discovery thread");

            // Initial device enumeration
            EnumerateDevices();

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            // Try using udev to monitor first
            int errorCount = 0;
            const int errorThreshold = 3;
            while (errorCount < errorThreshold && !s_ThreadStop.WaitOne(5000))
            {
                if (!MonitorUdev())
                {
                    errorCount++;
                    Logging.Error($"udev monitoring failed! {(errorCount < errorThreshold ? "Trying again" : "Falling back to periodic re-enumeration of hidapi")}");
                }
            }
#endif

            // Fall back to just periodically enumerating hidapi
            while (!s_ThreadStop.WaitOne(2000))
            {
                EnumerateDevices();
            }
        }

        private static void ReadThread()
        {
            Logging.Verbose("Starting device read thread");

            do
            {
                UpdateDevices();
            }
            while (s_DeviceLookup.Count > 0 && !s_ThreadStop.WaitOne(0));
        }

        private static void EnumerateDevices()
        {
            Logging.Verbose("Enumerating hidapi devices");
            foreach (var info in hid_enumerate())
            {
                if (!s_DeviceLookup.Values.Any((entry) => entry.path == info.path))
                {
                    Logging.Verbose($"Found new device, adding to addition queue. VID/PID: {info.vendorId:X4}:{info.productId:X4}, path: {info.path}");
                    s_AdditionQueue.Add(info);
                }
            }
        }

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        // Returns false if falling back to hidapi polling is necessary, returns true on exit
        private static bool MonitorUdev()
        {
            Logging.Verbose("Monitoring udev for devices");

            // Initialize udev
            var udev = udev_new();
            if (udev == null || udev.IsInvalid)
            {
                Logging.Error($"Failed to initialize udev context: {errno}");
                return false;
            }

            using (udev)
            {
                // Set up device monitor
                var monitor = udev_monitor_new_from_netlink(udev, "udev");
                if (monitor == null || monitor.IsInvalid)
                {
                    Logging.Error($"Failed to initialize device monitor: {errno}");
                    return false;
                }

                // Add filter for hidraw devices
                if (udev_monitor_filter_add_match_subsystem_devtype(monitor, "hidraw", null) < 0)
                {
                    Logging.Error($"Failed to add filter to device monitor: {errno}");
                    return false;
                }

                // Enable monitor
                if (udev_monitor_enable_receiving(monitor) < 0)
                {
                    Logging.Error($"Failed to enable receiving on device monitor: {errno}");
                    return false;
                }

                using (monitor)
                {
                    // Get monitor file descriptor
                    var fd = udev_monitor_get_fd(monitor);
                    if (udev == null || udev.IsInvalid)
                    {
                        Logging.Error($"Failed to get device monitor file descriptor: {errno}");
                        return false;
                    }

                    using (fd)
                    {
                        int errorCount = 0;
                        const int errorThreshold = 5;
                        while (!s_ThreadStop.WaitOne(1000))
                        {
                            // Check if any events are available
                            int result = poll(POLLIN, 0, fd);
                            if (result <= 0) // No events, or an error occured
                            {
                                if (result < 0) // Error
                                {
                                    errorCount++;
                                    Logging.Error($"Error while polling for device monitor events: {errno}");
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
                                Logging.Error($"Failed to get changed device: {errno}");
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
            }

            return true;
        }
#endif

        private static void UpdateDevices()
        {
            foreach (var device in s_DeviceLookup.Values)
            {
                // Update device state
                if (!device.UpdateState())
                {
                    // Queue for removal
                    Logging.Verbose($"Queuing device {device} (ID {device.deviceId}) for removal");
                    s_RemovalQueue.Add(device);
                    continue;
                }
            }

            // Remove devices queued for removal from the main list
            if (s_RemovalQueue.Count > 0)
            {
                foreach (var device in s_RemovalQueue)
                {
                    int deviceId = device.deviceId;
                    s_DeviceLookup.TryRemove(deviceId, out _);
                }
            }
        }

        private static void HandleAdditionQueue()
        {
            while (!s_AdditionQueue.IsEmpty)
            {
                if (s_AdditionQueue.TryTake(out var info) && !s_DeviceLookup.Values.Any((entry) => entry.path == info.path))
                {
                    AddDevice(info);
                }
            }
        }

        private static void HandleRemovalQueue()
        {
            foreach (var device in s_RemovalQueue)
            {
                // Ensure device was removed from the list
                int deviceId = device.deviceId;
                if (s_DeviceLookup.ContainsKey(deviceId))
                    s_DeviceLookup.TryRemove(deviceId, out _);

                device.Remove();
            }
            s_RemovalQueue.Clear();
        }

        private static void AddDevice(hid_device_info info)
        {
            Logging.Verbose($"Adding new device to input system. VID/PID: {info.vendorId:X4}:{info.productId:X4}, path: {info.path}");
            var device = HidApiDevice.TryCreate(info);
            if (device == null || device.deviceId == InputDevice.InvalidDeviceId ||
                !s_DeviceLookup.TryAdd(device.deviceId, device))
                return;

            if (s_ReadingThread == null || !s_ReadingThread.IsAlive)
            {
                s_ReadingThread = new Thread(ReadThread) { IsBackground = true };
                s_ReadingThread.Start();
            }
        }

        internal static unsafe void QueueEvent(InputEventPtr eventPtr)
        {
            lock (s_BufferLock)
            {
                s_InputBuffers[s_CurrentBuffer].AppendEvent(eventPtr);
            }
        }

        private static void FlushEventBuffer()
        {
            SlimEventBuffer buffer;
            lock (s_BufferLock)
            {
                buffer = s_InputBuffers[s_CurrentBuffer];
                s_CurrentBuffer = (s_CurrentBuffer + 1) % s_InputBuffers.Length;
            }

            foreach (var eventPtr in buffer)
            {
                try
                {
                    InputSystem.QueueEvent(eventPtr);
                }
                catch (Exception ex)
                {
                    Logging.Error($"Error when flushing an event: {ex}");
                }
            }
            buffer.Reset();
        }
    }
}