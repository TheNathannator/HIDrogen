#if UNITY_STANDALONE_LINUX
using System;
using HIDrogen.Imports.Linux;
using HIDrogen.Services;
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    using static HidRaw;

    internal class HidRawBackend : CustomInputBackend<HidRawDevice, HidRawBackend.AddContext>,
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

        protected override HidRawDevice OnDeviceAdded(InputDevice device, AddContext context)
        {
            return new HidRawDevice(device);
        }

        protected override void OnDeviceRemoved(HidRawDevice device)
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
