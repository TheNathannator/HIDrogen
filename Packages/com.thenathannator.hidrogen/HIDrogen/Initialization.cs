using System;
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
    internal static partial class Initialization
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
    }
}