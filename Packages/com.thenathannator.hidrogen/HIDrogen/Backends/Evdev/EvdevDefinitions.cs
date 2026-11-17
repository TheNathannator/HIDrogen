using System;
using HIDrogen.Imports.Linux;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend.Linux
{
    // Intentionally public and not platform guarded, for other packages to reference.
    [Serializable]
    public struct EvdevCapabilities
    {
        public string driver;

        public input_bus_type busType;
        public int vendorId;
        public int productId;

        public int xpadSubtype;
        public int santrollerSubtype;
        public input_key_code[] supportedKeys;
        public input_abs_code[] supportedAbs;
        public input_rel_code[] supportedRel;

        public input_absinfo[] absInfo;
    }

    public static class EvdevDefinitions
    {
        public const string InterfaceName = "Evdev";
        public static readonly FourCC InputFormat = new FourCC('E', 'V', 'D', 'V');
    }
}
