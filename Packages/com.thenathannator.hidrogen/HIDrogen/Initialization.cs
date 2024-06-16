using System;
using UnityEditor;

#if !UNITY_EDITOR
using UnityEngine;
#endif

namespace HIDrogen
{
    /// <summary>
    /// Handles initialization of the package.
    /// </summary>
    internal static partial class Initialization
    {
        /// <summary>
        /// Initializes everything.
        /// </summary>
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        internal static void Initialize()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += Uninitialize;
            EditorApplication.quitting += Uninitialize;
#else
            Application.quitting += Uninitialize;
#endif

            try
            {
                PlatformInitialize();
            }
            catch (Exception ex)
            {
                Logging.Exception("Failed to initialize backends", ex);
            }
        }

        internal static void Uninitialize()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Uninitialize;
            EditorApplication.quitting -= Uninitialize;
#else
            Application.quitting -= Uninitialize;
#endif

            try
            {
                PlatformUninitialize();
            }
            catch (Exception ex)
            {
                Logging.Exception("Failed to uninitialize backends", ex);
            }
        }

        static partial void PlatformInitialize();
        static partial void PlatformUninitialize();
    }
}