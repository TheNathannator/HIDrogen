#if UNITY_STANDALONE_LINUX
using System.Collections.Generic;

namespace HIDrogen.Backend
{
    internal static class LinuxInputOwnership
    {
        private static readonly HashSet<string> s_Claims = new HashSet<string>();

        internal static bool TryClaim(string path)
        {
            lock (s_Claims)
                return s_Claims.Add(path);
        }

        internal static void Release(string path)
        {
            lock (s_Claims)
                s_Claims.Remove(path);
        }
    }
}
#endif
