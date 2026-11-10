#if UNITY_STANDALONE_LINUX
using System;
using System.Collections.Generic;
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

        private readonly HashSet<string> m_TrackedDevpaths = new HashSet<string>();

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
            using (var hidRoot = device.get_parent_with_subsystem_devtype(Udev.SubsystemHid, null))
            using (var hidraw = FindSibling(udev, device, Udev.SubsystemHidRaw))
            using (var input = FindSibling(udev, device, Udev.SubsystemInput))
            {
                if (hidraw == null || hidraw.IsInvalid || hidRoot == null || hidRoot.IsInvalid)
                {
                    return false;
                }

                string devpath = hidraw.get_devpath().ToString();
                if (m_TrackedDevpaths.Contains(devpath))
                {
                    // Return true so that this device is not considered by other backends
                    return true;
                }

                // TODO: Query device info, open device, and test for layout availability

                m_TrackedDevpaths.Add(devpath);
                return true;
            }
        }

        public bool ProbeDeviceRemove(udev_context udev, udev_device device)
        {
            using (var hidraw = FindSibling(udev, device, Udev.SubsystemHidRaw))
            {
                if (hidraw == null || hidraw.IsInvalid)
                {
                    return false;
                }

                string devpath = hidraw.get_devpath().ToString();
                m_TrackedDevpaths.Remove(devpath);
                return true;
            }
        }

        private udev_device FindSibling(udev_context udev, udev_device device, string siblingSubsystem)
        {
            if (device.get_subsystem().ToString() == siblingSubsystem)
            {
                // Clone to simplify disposal handling
                return device.add_ref();
            }

            using (var hidRoot = device.get_parent_with_subsystem_devtype(Udev.SubsystemHid, null))
            {
                if (hidRoot == null || hidRoot.IsInvalid)
                {
                    return null;
                }

                return Udev.FindChildBySubsystem(udev, hidRoot, siblingSubsystem);
            }
        }
    }
}
#endif
