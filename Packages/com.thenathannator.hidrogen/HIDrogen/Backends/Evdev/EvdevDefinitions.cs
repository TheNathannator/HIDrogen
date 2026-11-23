using System;
using System.Runtime.InteropServices;
using HIDrogen.Imports.Linux;
using UnityEngine.InputSystem.Layouts;
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

        // not the biggest fan of stuffing device-specific data in here,
        // but there's also not really any harm in doing so
        public byte santrollerSubtype;
        public byte santrollerCapabilities;

        public input_key_code[] supportedKeys;
        public input_abs_code[] supportedAbs;
        public input_rel_code[] supportedRel;

        public input_absinfo[] absInfo;

        internal InputDeviceMatcher ApplyToMatcher(InputDeviceMatcher matcher)
        {
            return matcher
                .WithCapability("driver", driver)
                .WithCapability("busType", (int)busType)
                .WithCapability("vendorId", vendorId)
                .WithCapability("productId", productId)
                .WithCapability("santrollerSubtype", santrollerSubtype)
                .WithCapability("santrollerCapabilities", santrollerCapabilities);

            // Supported inputs are not matched against for simplicity
        }
    }

    public static class EvdevDefinitions
    {
        public const string InterfaceName = "Evdev";
        public static readonly FourCC InputFormat = new FourCC('E', 'V', 'D', 'V');
    }

#if UNITY_STANDALONE_LINUX
    // Non-public definitions from this point forward

    // Definitions for USB device fixups

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct Santroller2VendorDescriptor
    {
        public usb_descriptor_header header;
        public fixed byte signature[4]; // "SANT"
        public byte bVersion; // 2
        public byte bSubtype;
        public byte bCapabilities;

        public bool IsValid =>
            signature[0] == 'S' &&
            signature[1] == 'A' &&
            signature[2] == 'N' &&
            signature[3] == 'T' &&
            bVersion == 2;
    }
#endif
}
