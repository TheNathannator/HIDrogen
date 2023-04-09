using HIDrogen.Backend;
using UnityEditor;
using UnityEngine;

namespace HIDrogen
{
    /// <summary>
    /// Handles initialization of the package.
    /// </summary>
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    internal static class Initialization
    {
#if UNITY_EDITOR
        static Initialization()
        {
            Initialize();
        }
#endif

        /// <summary>
        /// Initializes everything.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Initialize()
        {
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            if (HidApiBackend.Initialize())
            {
                LinuxShim.Initialize();
            }
#endif
        }
    }
}