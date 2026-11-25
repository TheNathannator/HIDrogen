using System;
using HIDrogen.Imports.Linux;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Devices.LowLevel
{
    // Intentionally public and not platform guarded, for other packages to reference.
    [Serializable]
    public struct EvdevCapabilities
    {
        public string driver;

        public input_bus_type busType;
        public int vendorId;
        public int productId;

        // TODO: Not the biggest fan of stuffing device-specific data in here
        // But, there's also not really any harm in doing so... just feels hacky
        public byte xusbSubtype;
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
                .WithCapability("xusbSubtype", (int)xusbSubtype)
                .WithCapability("santrollerSubtype", (int)santrollerSubtype)
                .WithCapability("santrollerCapabilities", (int)santrollerCapabilities);

            // Supported inputs are not matched against for simplicity
        }
    }

    public static class EvdevDefinitions
    {
        public const string InterfaceName = "Evdev";
        public static readonly FourCC InputFormat = new FourCC('E', 'V', 'D', 'V');

        public const string DriverHidGeneric = "hid-generic";
        public const string DriverPlaystation = "playstation";
        public const string DriverSony = "sony";
        public const string DriverWiimote = "wiimote";
        public const string DriverXpad = "xpad";
    }
}
