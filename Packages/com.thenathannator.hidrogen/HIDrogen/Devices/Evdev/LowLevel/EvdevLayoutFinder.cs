using System;
using System.Collections.Generic;
using System.Text;
using HIDrogen.Imports.Linux;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Devices.LowLevel
{
    // Intentionally public and not platform guarded, for other packages to reference.

    public struct EvdevDeviceMatcher
    {
        private InputDeviceMatcher m_Matcher;

        public EvdevDeviceMatcher WithDriver(string driver)
        {
            m_Matcher = m_Matcher.WithCapability("driver", driver);
            return this;
        }

        public EvdevDeviceMatcher WithBusType(input_bus_type busType)
        {
            m_Matcher = m_Matcher.WithCapability("busType", (int)busType);
            return this;
        }

        public EvdevDeviceMatcher WithHardwareIds(ushort vendorId, ushort productId)
        {
            m_Matcher = m_Matcher.WithCapability("vendorId", (int)vendorId);
            m_Matcher = m_Matcher.WithCapability("productId", (int)productId);
            return this;
        }

        public EvdevDeviceMatcher WithRevision(ushort revision)
        {
            m_Matcher = m_Matcher.WithVersion(revision.ToString());
            return this;
        }

        public EvdevDeviceMatcher WithXusbSubtype(byte xusbSubtype)
        {
            m_Matcher = m_Matcher.WithCapability("xusbSubtype", xusbSubtype);
            return this;
        }

        public EvdevDeviceMatcher WithSantrollerSubtype(byte santrollerSubtype)
        {
            m_Matcher = m_Matcher.WithCapability("santrollerSubtype", santrollerSubtype);
            return this;
        }

        public EvdevDeviceMatcher WithSantrollerCapabilities(byte santrollerCapabilities)
        {
            m_Matcher = m_Matcher.WithCapability("santrollerCapabilities", santrollerCapabilities);
            return this;
        }

        internal InputDeviceMatcher ToMatcher()
        {
            return m_Matcher.WithInterface(EvdevDefinitions.InterfaceName);
        }
    }

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
                var registration = s_Registry[i];
                if (!registration.mappings.IsCompatible(capabilities))
                {
                    continue;
                }

                float match = registration.matcher.MatchPercentage(description);
                if (match > bestMatch)
                {
                    bestMatch = match;
                    bestMatchIndex = i;
                }
            }

            string baseLayout = nameof(EvdevJoystick);
            string layoutName = description.interfaceName;
            EvdevDeviceMapping mappings = null;
            if (bestMatchIndex >= 0)
            {
                var registration = s_Registry[bestMatchIndex];
                layoutName = registration.layoutName;
                baseLayout = registration.layoutName;
                mappings = registration.mappings;
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
            EvdevDeviceMatcher matcher
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
                matcher = matcher.ToMatcher()
            });
        }
    }
}
