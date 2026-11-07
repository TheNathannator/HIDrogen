#if UNITY_STANDALONE_LINUX
using System;
using HIDrogen.Imports.Linux;
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    using static Evdev;

    /// <summary>
    /// Provides input through the hidapi library.
    /// </summary>
    internal partial class EvdevBackend : CustomInputBackend<EvdevDevice, EvdevBackend.AddContext>
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
    }
}
#endif
