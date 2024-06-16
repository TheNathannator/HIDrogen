namespace HIDrogen.Backend
{
    internal partial class HidApiBackend
    {
        partial void PlatformInitialize();
        partial void PlatformDispose();
        partial void PlatformMonitor(ref bool success);

        partial void PlatformGetDescriptor(string path, ref byte[] descriptor, ref bool success);
        unsafe partial void PlatformGetDescriptor(string path, void* buffer, int bufferLength,
            ref int bytesWritten, ref bool success);
        partial void PlatformGetDescriptorSize(string path, ref int size, ref bool success);
        partial void PlatformGetVersionNumber(string path, ref ushort version, ref bool success);

        private bool PlatformMonitor()
        {
            bool success = false;
            PlatformMonitor(ref success);
            return success;
        }

        internal bool PlatformGetDescriptor(string path, out byte[] descriptor)
        {
            descriptor = null;
            bool success = false;
            PlatformGetDescriptor(path, ref descriptor, ref success);
            return success;
        }

        internal unsafe bool PlatformGetDescriptor(string path, void* buffer, int bufferLength, out int bytesWritten)
        {
            bytesWritten = 0;
            if (buffer == null)
                return false;

            bool success = false;
            PlatformGetDescriptor(path, buffer, bufferLength, ref bytesWritten, ref success);
            return success;
        }

        internal bool PlatformGetDescriptorSize(string path, out int size)
        {
            size = 0;
            bool success = false;
            PlatformGetDescriptorSize(path, ref size, ref success);
            return success;
        }

        internal bool PlatformGetVersionNumber(string path, out ushort version)
        {
            version = 0;
            bool success = false;
            PlatformGetVersionNumber(path, ref version, ref success);
            return success;
        }
    }
}