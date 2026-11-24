using System;
using System.Collections.Generic;
using System.Text;
using HIDrogen.Imports.Linux;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend.Linux
{
    // Intentionally public and not platform guarded, for other packages to reference.
    public static class EvdevLayoutFinder
    {
        private struct RegisteredLayout
        {
            public string layoutName;
            public InputDeviceMatcher matcher;
            public EvdevDeviceMapping mappings;
        }

        private static readonly StringBuilder s_StringBuilder = new StringBuilder();

        private static readonly List<RegisteredLayout> s_Registry = new List<RegisteredLayout>();

        internal static void Initialize()
        {
            EvdevAxisControl.Initialize();
            EvdevButtonControl.Initialize();
            EvdevDevice.Initialize();
            EvdevGamepad.Initialize();

            InputSystem.onFindLayoutForDevice += FindDeviceLayout;
        }

        internal static string FindDeviceLayout(
            ref InputDeviceDescription description,
            string matchedLayout,
            InputDeviceExecuteCommandDelegate commandDelegate
        )
        {
            if (description.interfaceName != EvdevDefinitions.InterfaceName ||
                !string.IsNullOrEmpty(matchedLayout))
            {
                return null;
            }
            
            EvdevCapabilities capabilities;
            try
            {
                capabilities = JsonUtility.FromJson<EvdevCapabilities>(description.capabilities);
            }
            catch (Exception ex)
            {
                Logging.Exception("Failed to parse capabilities from evdev device", ex);
                return null;
            }

            if (capabilities.vendorId == 0 && capabilities.productId == 0)
            {
                return null;
            }

            int bestMatchIndex = -1;
            float bestMatch = 0;
            for (int i = 0; i < s_Registry.Count; i++)
            {
                float match = s_Registry[i].matcher.MatchPercentage(description);
                if (match > bestMatch)
                {
                    bestMatch = match;
                    bestMatchIndex = i;
                }
            }

            string baseLayout = nameof(EvdevDevice);
            string layoutName = description.interfaceName;
            EvdevDeviceMapping mappings = null;
            if (bestMatchIndex >= 0)
            {
                var registration = s_Registry[bestMatchIndex];
                if (registration.mappings.IsCompatible(capabilities))
                {
                    layoutName = registration.layoutName;
                    baseLayout = registration.layoutName;
                    mappings = registration.mappings;
                }
            }

            layoutName += $"::{(int)capabilities.busType}-{capabilities.vendorId}-{capabilities.productId}";

            if (!string.IsNullOrEmpty(description.manufacturer))
            {
                layoutName += $"::{SanitizeName(description.manufacturer)}";
            }

            if (!string.IsNullOrEmpty(description.product))
            {
                layoutName += $"::{SanitizeName(description.product)}";
            }

            string SanitizeName(string name)
            {
                s_StringBuilder.Clear();

                foreach (char c in name)
                {
                    if (char.IsLetter(c) || char.IsDigit(c))
                    {
                        s_StringBuilder.Append(c);
                    }
                }

                if (s_StringBuilder.Length < 1)
                {
                    return "Empty";
                }

                return s_StringBuilder.ToString();
            }

            var matcher = InputDeviceMatcher.FromDeviceDescription(description);
            matcher = capabilities.ApplyToMatcher(matcher);

            var builder = new EvdevLayoutBuilder()
            {
                layoutName = layoutName,
                layoutDisplayName = description.product,
                baseLayout = baseLayout,
                capabilities = capabilities,
                mappings = mappings,
            };
            InputSystem.RegisterLayoutBuilder(
                () => builder.Build(),
                layoutName,
                baseLayout,
                matcher
            );

            return layoutName;
        }

        public static void RegisterLayout<TDevice>(
            EvdevDeviceMapping mappings,
            InputDeviceMatcher matcher
        )
            where TDevice : InputDevice
        {
            InputSystem.RegisterLayout<TDevice>();

            // evdev layouts are mapped based on input codes, not raw bytes,
            // so we have to wait for layout finding so that we can get the evdev capabilities
            s_Registry.Add(new RegisteredLayout()
            {
                layoutName = typeof(TDevice).Name,
                mappings = mappings,
                matcher = matcher
            });
        }

        public static void RegisterLayout<TDevice>(
            EvdevDeviceMapping mapping,
            ushort vendorId,
            ushort productId
        )
            where TDevice : InputDevice
        {
            RegisterLayout<TDevice>(mapping, GetMatcher(vendorId, productId));
        }

        public static void RegisterLayout<TDevice>(
            EvdevDeviceMapping mapping,
            ushort vendorId,
            ushort productId,
            ushort revision
        )
            where TDevice : InputDevice
        {
            RegisterLayout<TDevice>(mapping, GetMatcher(vendorId, productId, revision));
        }

        public static void RegisterLayout<TDevice>(
            EvdevDeviceMapping mapping,
            input_bus_type busType,
            ushort vendorId,
            ushort productId,
            ushort revision
        )
            where TDevice : InputDevice
        {
            RegisterLayout<TDevice>(mapping, GetMatcher(busType, vendorId, productId, revision));
        }

        private static InputDeviceMatcher GetMatcher(
            ushort vendorId,
            ushort productId
        )
        {
            return new InputDeviceMatcher()
                .WithInterface(EvdevDefinitions.InterfaceName)
                .WithCapability("vendorId", (int)vendorId)
                .WithCapability("productId", (int)productId);
        }

        private static InputDeviceMatcher GetMatcher(
            ushort vendorId,
            ushort productId,
            ushort revision
        )
        {
            return new InputDeviceMatcher()
                .WithInterface(EvdevDefinitions.InterfaceName)
                .WithVersion(revision.ToString())
                .WithCapability("vendorId", (int)vendorId)
                .WithCapability("productId", (int)productId);
        }

        private static InputDeviceMatcher GetMatcher(
            input_bus_type busType,
            ushort vendorId,
            ushort productId,
            ushort revision
        )
        {
            return new InputDeviceMatcher()
                .WithInterface(EvdevDefinitions.InterfaceName)
                .WithVersion(revision.ToString())
                .WithCapability("busType", (int)busType)
                .WithCapability("vendorId", (int)vendorId)
                .WithCapability("productId", (int)productId);
        }
    }
}
