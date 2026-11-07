#if UNITY_STANDALONE_LINUX
using System;
using HIDrogen.Imports.Linux;
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    using static Evdev;

    internal class EvdevDevice : IDisposable
    {
        public EvdevDevice(InputDevice device)
        {
        }

        public void Dispose()
        {
        }
    }
}
#endif