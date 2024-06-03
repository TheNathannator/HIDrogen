using System.Diagnostics;
using System.Runtime.InteropServices;

using Debug = UnityEngine.Debug;

namespace HIDrogen
{
    internal static class Logging
    {
        public static void Message(string message)
            => Debug.Log($"[HIDrogen] {message}");

        public static void Warning(string message)
            => Debug.LogWarning($"[HIDrogen] {message}");

        public static void Error(string message)
            => Debug.LogError($"[HIDrogen] {message}");

        public static void InteropError(string message)
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            => InteropError(message, Imports.Libc.errno);

        public static void InteropError(string message, Imports.Errno result)
#else
            => InteropError(message, Marshal.GetLastWin32Error());

        public static void InteropError(string message, int result)
#endif

            => Debug.LogError($"[HIDrogen] {message} ({result})");

        [Conditional("HIDROGEN_VERBOSE_LOGGING")]
        public static void Verbose(string message)
            => Message(message);
    }
}