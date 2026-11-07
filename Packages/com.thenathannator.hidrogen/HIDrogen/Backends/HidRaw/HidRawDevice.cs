#if UNITY_STANDALONE_LINUX
using System;
using UnityEngine.InputSystem;

namespace HIDrogen.Backend
{
    internal class HidRawDevice : IDisposable
    {
        public HidRawDevice(InputDevice device)
        {
        }

        public void Dispose()
        {
        }
    }
}
#endif
