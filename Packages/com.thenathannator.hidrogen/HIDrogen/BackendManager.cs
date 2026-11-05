using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen
{
    internal static class BackendManager
    {
        private static readonly List<ICustomInputBackend> s_Backends = new List<ICustomInputBackend>();
        private static readonly List<ICustomInputService> s_Services = new List<ICustomInputService>();

        public static bool TryCreateBackend<T>()
            where T : ICustomInputBackend, new()
        {
            foreach (var existing in s_Backends)
            {
                if (existing is T)
                {
                    Logging.Error($"{typeof(T).Name} already exists!");
                    return false;
                }
            }

            try
            {
                var backend = new T();
                s_Backends.Add(backend);
                return true;
            }
            catch (Exception ex)
            {
                Logging.Exception($"Failed to create {typeof(T).Name}", ex);
                return false;
            }
        }

        public static bool TryCreateService<T>()
            where T : ICustomInputService, new()
        {
            foreach (var existing in s_Services)
            {
                if (existing is T)
                {
                    Logging.Error($"{typeof(T).Name} already exists!");
                    return false;
                }
            }

            try
            {
                var service = new T();
                s_Services.Add(service);
                return true;
            }
            catch (Exception ex)
            {
                Logging.Exception($"Failed to create {typeof(T).Name}", ex);
                return false;
            }
        }

        public static T TryGetBackend<T>()
            where T : ICustomInputBackend
        {
            foreach (var backend in s_Backends)
            {
                if (backend is T target)
                {
                    return target;
                }
            }

            return default;
        }

        public static T TryGetService<T>()
            where T : ICustomInputService
        {
            foreach (var service in s_Services)
            {
                if (service is T target)
                {
                    return target;
                }
            }

            return default;
        }

        public static void Start()
        {
            for (int i = 0; i < s_Backends.Count; i++)
            {
                var backend = s_Backends[i];
                try
                {
                    backend.Start();
                }
                catch (Exception ex)
                {
                    backend.Dispose();
                    s_Backends.RemoveAt(i--);
                    Logging.Exception($"Failed to start {backend.GetType().Name}!", ex);
                }
            }

            for (int i = 0; i < s_Services.Count; i++)
            {
                var service = s_Services[i];
                try
                {
                    service.Start();
                }
                catch (Exception ex)
                {
                    service.Dispose();
                    s_Services.RemoveAt(i--);
                    Logging.Exception($"Failed to start {service.GetType().Name}!", ex);
                }
            }

            InputSystem.onBeforeUpdate += Update;
            InputSystem.onDeviceChange += OnDeviceChange;
            unsafe { InputSystem.onDeviceCommand += OnDeviceCommand; }
        }

        public static void Stop()
        {
            InputSystem.onBeforeUpdate -= Update;
            InputSystem.onDeviceChange -= OnDeviceChange;
            unsafe { InputSystem.onDeviceCommand -= OnDeviceCommand; }

            foreach (var backend in s_Backends)
            {
                backend.Stop();
            }

            foreach (var service in s_Services)
            {
                service.Stop();
            }
        }

        public static void Uninitialize()
        {
            Stop();

            foreach (var backend in s_Backends)
            {
                backend.Dispose();
            }
            s_Backends.Clear();

            foreach (var service in s_Services)
            {
                service.Dispose();
            }
            s_Services.Clear();
        }

        private static void Update()
        {
            foreach (var backend in s_Backends)
            {
                backend.Update();
            }
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            foreach (var backend in s_Backends)
            {
                backend.OnDeviceChange(device, change);
            }
        }

        private static unsafe long? OnDeviceCommand(InputDevice device, InputDeviceCommand* command)
        {
            if (device == null)
            {
                return null;
            }

            if (command == null)
            {
                return InputDeviceCommand.GenericFailure;
            }

            foreach (var backend in s_Backends)
            {
                var result = backend.OnDeviceCommand(device, command);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}