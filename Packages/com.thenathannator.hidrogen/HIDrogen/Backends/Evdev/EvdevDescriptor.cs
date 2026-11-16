using System;

namespace HIDrogen.Backend
{
    // Intentionally public and not platform guarded, for other packages to reference.
    [Serializable]
    public struct EvdevDescriptor
    {
        public int vendorId;
        public int productId;
    }
}
