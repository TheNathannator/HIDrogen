using System;
using HIDrogen.Imports.Linux;

namespace HIDrogen.Backend
{
    // Intentionally public and not platform guarded, for other packages to reference.
    [Serializable]
    public struct EvdevDescriptor
    {
        public int vendorId;
        public int productId;

        public input_key_code[] supportedKeys;
        public input_abs_code[] supportedAbs;
        public input_rel_code[] supportedRel;

        public input_absinfo[] absInfo;
    }
}
