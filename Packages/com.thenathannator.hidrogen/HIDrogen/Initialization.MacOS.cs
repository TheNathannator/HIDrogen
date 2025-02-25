#if UNITY_STANDALONE_OSX
using HIDrogen.Backend;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HIDrogen
{
    internal static partial class Initialization
    {
        private static USBBackend s_USBBackend;

        static partial void PlatformInitialize()
        {
            s_USBBackend = new USBBackend();
        }

        static partial void PlatformUninitialize()
        {
            s_USBBackend?.Dispose();
        }
    }
}
#endif