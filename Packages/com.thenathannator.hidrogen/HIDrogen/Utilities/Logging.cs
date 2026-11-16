using System;
using System.Diagnostics;

#if UNITY_STANDALONE_LINUX
using HIDrogen.Imports.Posix;
#else
using System.Runtime.InteropServices;
#endif

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

        public static string MakeErrorMessage(string message, int error)
            => $"{message} (0x{error:X8})";

#if UNITY_STANDALONE_LINUX
        public static string MakeErrnoMessage(string message, int result)
            => MakeErrnoMessage(message, (Errno)Math.Abs(result));
        public static string MakeErrnoMessage(string message, Errno errno)
            => $"{message} ({errno})";
#endif

        public static void Error(string message, int error)
            => Error(MakeErrorMessage(message, error));

#if UNITY_STANDALONE_LINUX
        public static void Errno(string message, int result)
            => Error(MakeErrnoMessage(message, result));
        public static void Errno(string message, Errno errno)
            => Error(MakeErrnoMessage(message, errno));

#endif

        public static string MakeLastErrorMessage(string message)
#if UNITY_STANDALONE_LINUX
            => MakeErrnoMessage(message, Posix.errno);
#else
            => MakeErrorMessage(message, Marshal.GetLastWin32Error());
#endif

        public static void LastError(string message)
            => Debug.LogError($"[HIDrogen] {MakeLastErrorMessage(message)}");

        [Conditional("HIDROGEN_VERBOSE_LOGGING")]
        public static void Verbose(string message)
            => Message(message);

        [Conditional("HIDROGEN_VERBOSE_LOGGING")]
        public static void Verbose(Exception ex)
            => Message(ex.ToString());
    }
}