using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HIDrogen.Imports;
using HIDrogen.LowLevel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend
{
    using static HidApi;

    /// <summary>
    /// Provides input through the hidapi library.
    /// </summary>
    internal static class HidApiBackend
    {
        // Keeps track of available devices
        private static readonly ConcurrentDictionary<string, HidApiDevice> s_PathLookup = new ConcurrentDictionary<string, HidApiDevice>();

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
        private const int kInputBufferCount = 2;
        private static readonly SlimEventBuffer[] s_InputBuffers = new SlimEventBuffer[kInputBufferCount];
        private static readonly object s_BufferLock = new object();
        private static int s_CurrentBuffer = 0;

        // Format codes
        public static readonly FourCC InputFormat = new FourCC('H', 'I', 'D');

        internal static unsafe bool Initialize()
        {
            // Initialize hidapi
            int result = hid_init();
            if (result < 0)
            {
                Debug.LogError($"Failed to initialize hidapi!");
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
            for (int i = 0; i < kInputBufferCount; i++)
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
            foreach (var device in s_PathLookup.Values)
            {
                device.Dispose();
            }

            // Dispose event buffers
            // Initialize event buffers
            for (int i = 0; i < kInputBufferCount; i++)
            {
                s_InputBuffers[i].Dispose();
            }

            // Free hidapi
            int result = hid_exit();
            if (result < 0)
            {
                Debug.LogError("Error when freeing hidapi!");
            }
        }

        private static void Update()
        {
            FlushEventBuffer();
            HandleAdditionQueue();
        }

        private static unsafe long? DeviceCommand(InputDevice device, InputDeviceCommand* command)
        {
            // TODO
            return null;
        }

        private static void DeviceDiscoveryThread()
        {
            do
            {
                // Find new devices from hidapi
                EnumerateDevices();
            }
            while (!s_ThreadStop.WaitOne(2000));
        }

        private static void ReadThread()
        {
            do
            {
                UpdateDevices();
            }
            while (s_PathLookup.Count > 0 && !s_ThreadStop.WaitOne(0));
        }

        private static void EnumerateDevices()
        {
            foreach (var info in hid_enumerate())
            {
                // Ignore unsupported devices
                if (!HIDSupport.supportedHIDUsages.Any((usage) =>
                    (int)usage.page == info.usagePage && usage.usage == info.usage))
                    continue;

                if (!s_PathLookup.ContainsKey(info.path))
                {
                    s_AdditionQueue.Add(info);
                }
            }
        }

        private static void UpdateDevices()
        {
            foreach (var device in s_PathLookup.Values)
            {
                // Update device state
                if (!device.UpdateState())
                {
                    // Queue for removal
                    s_RemovalQueue.Add(device);
                    continue;
                }
            }

            // Handle devices queued for removal
            if (s_RemovalQueue.Count > 0)
            {
                HandleRemovalQueue();
            }
        }

        private static void HandleAdditionQueue()
        {
            while (!s_AdditionQueue.IsEmpty)
            {
                if (s_AdditionQueue.TryTake(out var info) && !s_PathLookup.ContainsKey(info.path))
                {
                    AddDevice(info);
                }
            }
        }

        private static void HandleRemovalQueue()
        {
            foreach (var device in s_RemovalQueue)
            {
                if (s_PathLookup.ContainsKey(device.path))
                {
                    device.Dispose();
                    s_PathLookup.TryRemove(device.path, out _);
                }
            }
            s_RemovalQueue.Clear();
        }

        private static void AddDevice(hid_device_info info)
        {
            var device = HidApiDevice.TryCreate(info);
            if (device == null || !s_PathLookup.TryAdd(info.path, device))
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
                s_CurrentBuffer = (s_CurrentBuffer + 1) % kInputBufferCount;
            }

            foreach (var eventPtr in buffer)
            {
                try
                {
                    InputSystem.QueueEvent(eventPtr);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error when flushing an event: {ex}");
                }
            }
            buffer.Reset();
        }
    }
}