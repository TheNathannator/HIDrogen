using System;
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

        public static void Exception(string message, Exception ex)
        {
            Debug.LogError($"[HIDrogen] {message}");
            Debug.LogException(ex);
        }

        public static string MakeInteropErrorMessage(string message)
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            => $"{message} ({Imports.Libc.errno})";
#else
            => $"{message} (0x{Marshal.GetLastWin32Error():X8})";
#endif

        public static void InteropError(string message)
            => Debug.LogError($"[HIDrogen] {MakeInteropErrorMessage(message)}");

        [Conditional("HIDROGEN_VERBOSE_LOGGING")]
        public static void Verbose(string message)
            => Message(message);
    }
}