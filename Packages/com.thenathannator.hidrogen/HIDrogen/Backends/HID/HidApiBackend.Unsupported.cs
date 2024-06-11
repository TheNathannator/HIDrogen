#if !(UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
namespace HIDrogen.Backend
{
    internal partial class HidApiBackend
    {
        private void PlatformInitialize() { }
        private void PlatformDispose() { }

        private bool PlatformMonitor() => false;

        internal static unsafe bool PlatformGetDescriptor(string path, out byte[] descriptor)
        {
            descriptor = null;
            return false;
        }

        internal static unsafe bool PlatformGetDescriptor(string path, void* buffer, int bufferLength, out int bytesWritten)
        {
            bytesWritten = default;
            return false;
        }

        internal static unsafe bool PlatformGetDescriptorSize(string path, out int size)
        {
            size = default;
            return false;
        }

        private bool PlatformGetVersionNumber(string path, out ushort version)
        {
            version = default;
            return false;
        }
    }
}
#endif