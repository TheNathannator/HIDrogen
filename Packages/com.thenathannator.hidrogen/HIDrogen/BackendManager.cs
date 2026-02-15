using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen
{
    internal class BackendManager : IDisposable
    {
        private readonly List<ICustomInputBackend> m_Backends = new List<ICustomInputBackend>();
        private readonly List<ICustomInputService> m_Services = new List<ICustomInputService>();

        public void Dispose()
        {
            Stop();

            foreach (var backend in m_Backends)
            {
                backend.Dispose();
            }
            m_Backends.Clear();

            foreach (var service in m_Services)
            {
                service.Dispose();
            }
            m_Services.Clear();
        }

        public void AddBackend(ICustomInputBackend backend)
        {
            m_Backends.Add(backend);
        }

        public void AddService(ICustomInputService service)
        {
            m_Services.Add(service);
        }

        public bool TryCreateBackend<T>()
            where T : ICustomInputBackend, new()
        {
            try
            {
                var backend = new T();
                AddBackend(backend);
                return true;
            }
            catch (Exception ex)
            {
                Logging.Exception($"Failed to create {typeof(T).Name} backend", ex);
                return false;
            }
        }

        public bool TryCreateService<T>()
            where T : ICustomInputService, new()
        {
            try
            {
                var service = new T();
                AddService(service);
                return true;
            }
            catch (Exception ex)
            {
                Logging.Exception($"Failed to create {typeof(T).Name} service", ex);
                return false;
            }
        }

        public void RemoveBackend(ICustomInputBackend backend)
        {
            m_Backends.Remove(backend);
        }

        public void RemoveService(ICustomInputService service)
        {
            m_Services.Remove(service);
        }

        public void Start()
        {
            for (int i = 0; i < m_Backends.Count; i++)
            {
                var backend = m_Backends[i];
                try
                {
                    backend.Start();
                }
                catch (Exception ex)
                {
                    backend.Dispose();
                    m_Backends.RemoveAt(i--);
                    Logging.Exception($"Failed to start {backend.GetType()} backend!", ex);
                }
            }

            for (int i = 0; i < m_Services.Count; i++)
            {
                var service = m_Services[i];
                try
                {
                    service.Start();
                }
                catch (Exception ex)
                {
                    service.Dispose();
                    m_Services.RemoveAt(i--);
                    Logging.Exception($"Failed to start {service.GetType()} service!", ex);
                }
            }

            InputSystem.onBeforeUpdate += Update;
            InputSystem.onDeviceChange += OnDeviceChange;
            unsafe { InputSystem.onDeviceCommand += OnDeviceCommand; }
        }

        public void Stop()
        {
            InputSystem.onBeforeUpdate -= Update;
            InputSystem.onDeviceChange -= OnDeviceChange;
            unsafe { InputSystem.onDeviceCommand -= OnDeviceCommand; }

            foreach (var backend in m_Backends)
            {
                backend.Stop();
            }

            foreach (var service in m_Services)
            {
                service.Stop();
            }
        }

        private void Update()
        {
            foreach (var backend in m_Backends)
            {
                backend.Update();
            }
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            foreach (var backend in m_Backends)
            {
                backend.OnDeviceChange(device, change);
            }
        }

        private unsafe long? OnDeviceCommand(InputDevice device, InputDeviceCommand* command)
        {
            if (device == null)
            {
                return null;
            }

            if (command == null)
            {
                return InputDeviceCommand.GenericFailure;
            }

            foreach (var backend in m_Backends)
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