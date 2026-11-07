#if UNITY_STANDALONE_LINUX
using System;
using HIDrogen.Imports.Linux;
using HIDrogen.Services;
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    using static Evdev;

    /// <summary>
    /// Provides input through the hidapi library.
    /// </summary>
    internal partial class EvdevBackend : CustomInputBackend<EvdevDevice, EvdevBackend.AddContext>,
        IUdevBackend
    {
        internal struct AddContext : IDisposable
        {
            public void Dispose()
            {
            }
        }

        protected override void OnDispose()
        {
        }

        protected override EvdevDevice OnDeviceAdded(InputDevice device, AddContext context)
        {
            return new EvdevDevice(device);
        }

        protected override void OnDeviceRemoved(EvdevDevice device)
        {
            device.Dispose();
        }

        public bool ProbeDeviceAdd(udev_context udev, udev_device device)
        {
            return false;
        }

        public bool ProbeDeviceRemove(udev_context udev, udev_device device)
        {
            return false;
        }
    }
}
#endif
