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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
            {
                Logging.Exception("Failed to uninitialize backends", ex);
            }
        }
#endif

        static partial void PlatformInitialize();
        static partial void PlatformUninitialize();
    }
}