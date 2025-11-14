using System;

namespace HIDrogen
{
    /// <summary>
    /// Handles initialization of the package.
    /// </summary>
    internal static partial class Initialization
    {
// Ignore initialization if in editor and current build platform doesn't match editor runtime platform
#if !UNITY_EDITOR || (UNITY_STANDALONE_WIN && UNITY_EDITOR_WIN) || (UNITY_STANDALONE_OSX && UNITY_EDITOR_OSX) || (UNITY_STANDALONE_LINUX && UNITY_EDITOR_LINUX)
        /// <summary>
        /// Initializes everything.
        /// </summary>
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        internal static void Initialize()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Uninitialize;
            UnityEditor.EditorApplication.quitting += Uninitialize;
#else
            UnityEngine.Application.quitting += Uninitialize;
#endif

            try
            {
                Logging.Verbose("Initializing backends");
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
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Uninitialize;
            UnityEditor.EditorApplication.quitting -= Uninitialize;
#else
            UnityEngine.Application.quitting -= Uninitialize;
#endif

            try
            {
                Logging.Verbose("Uninitializing backends");
                PlatformUninitialize();
            }
            catch (Exception ex)
            {
                Logging.Exception("Failed to uninitialize backends", ex);
            }
        }
#endif

        static partial void PlatformInitialize();
        static partial void PlatformUninitialize();

        private static bool TryInitializeBackend<T>(ref T field)
            where T : CustomInputBackend, new()
        {
            try
            {
                field = new T();
                field.Start();
                return true;
            }
            catch (Exception ex)
            {
                Logging.Exception($"Failed to initialize {typeof(T).Name} backend", ex);
                return false;
            }
        }

        private static void TryUninitializeBackend<T>(ref T field)
            where T : CustomInputBackend
        {
            try
            {
                field?.Dispose();
                field = default;
            }
            catch (Exception ex)
            {
                Logging.Exception($"Failed to uninitialize {typeof(T).Name} backend", ex);
            }
        }

        private static bool TryInitializeService<T>(ref T field)
            where T : new()
        {
            try
            {
                field = new T();
                return true;
            }
            catch (Exception ex)
            {
                Logging.Exception($"Failed to initialize {typeof(T).Name} backend", ex);
                return false;
            }
        }

        private static void TryUninitializeService<T>(ref T field)
            where T : IDisposable
        {
            try
            {
                field?.Dispose();
                field = default;
            }
            catch (Exception ex)
            {
                Logging.Exception($"Failed to uninitialize {typeof(T).Name} backend", ex);
            }
        }
    }
}