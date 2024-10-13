#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
namespace HIDrogen.Imports.Linux
{
    internal static partial class Libc
    {
        public const string LibName = "libc";
    }
}
#endif