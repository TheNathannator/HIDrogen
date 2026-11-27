using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HIDrogen.Imports.Linux;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Devices.LowLevel
{
    // Intentionally public and not platform guarded, for other packages to reference.

    public delegate float EvdevLayoutMatchPredicate(EvdevCapabilities capabilities);

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

        public EvdevDeviceMatcher WithProduct(string product, bool supportRegex = true)
        {
            m_Matcher = m_Matcher.WithProduct(product, supportRegex);
            return this;
        }

        public EvdevDeviceMatcher WithRevision(ushort revision)
        {
            m_Matcher = m_Matcher.WithVersion(revision.ToString());
            return this;
        }

        public EvdevDeviceMatcher WithXusbSubtype(byte xusbSubtype)
        {
            m_Matcher = m_Matcher.WithCapability("xusbSubtype", (int)xusbSubtype);
            return this;
        }

        public EvdevDeviceMatcher WithSantrollerSubtype(byte santrollerSubtype)
        {
            m_Matcher = m_Matcher.WithCapability("santrollerSubtype", (int)santrollerSubtype);
            return this;
        }

        public EvdevDeviceMatcher WithSantrollerCapabilities(byte santrollerCapabilities)
        {
            m_Matcher = m_Matcher.WithCapability("santrollerCapabilities", (int)santrollerCapabilities);
            return this;
        }

        internal InputDeviceMatcher ToMatcher()
        {
            return m_Matcher.WithInterface(EvdevDefinitions.InterfaceName);
        }
    }

    public static class EvdevLayoutFinder
    {
#if UNITY_STANDALONE_LINUX // don't allocate mappings on non-Linux platforms
        private struct RegisteredLayout
        {
            public string layoutName;
            public InputDeviceMatcher matcher;
            public EvdevLayoutMatchPredicate predicate;
            public EvdevDeviceMapping mappings;
            public bool isStatic;
        }

        private static readonly StringBuilder s_StringBuilder = new StringBuilder();

        private static readonly List<RegisteredLayout> s_Registry = new List<RegisteredLayout>();
#endif

        internal static void Initialize()
        {
#if UNITY_STANDALONE_LINUX
            InputSystem.onFindLayoutForDevice += FindDeviceLayout;
#endif
        }

#if UNITY_STANDALONE_LINUX
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

            LayoutMatchDebug($"Finding layout for {description.ToJson()}");
            
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
                LayoutMatchDebug("No hardware IDs, skipping");
                return null;
            }

            int bestMatchIndex = -1;
            float bestMatch = 0;
            for (int i = 0; i < s_Registry.Count; i++)
            {
                var registration = s_Registry[i];

                float match = registration.matcher.MatchPercentage(description);
                if (match <= 0)
                {
                    continue;
                }

                LayoutMatchDebug($"Trying {registration.layoutName}...");

                if (!registration.mappings.IsCompatible(capabilities))
                {
                    // IsCompatible logs a reason alreaady
                    continue;
                }

                if (registration.predicate != null)
                {
                    float predicateMatch = registration.predicate(capabilities);
                    if (predicateMatch < 0)
                    {
                        LayoutMatchDebug("- Predicate doesn't match");
                        continue;
                    }

                    match += predicateMatch;
                }

                if (match > bestMatch)
                {
                    LayoutMatchDebug($"- New best match (score {match})");
                    bestMatch = match;
                    bestMatchIndex = i;
                }
                else
                {
                    LayoutMatchDebug($"- Matches but ranks lower (score {match})");
                }
            }

            string baseLayout = nameof(EvdevJoystick);
            string layoutName = description.interfaceName;
            EvdevDeviceMapping mappings = null;
            if (bestMatchIndex >= 0)
            {
                var registration = s_Registry[bestMatchIndex];
                LayoutMatchDebug($"Using layout {registration.layoutName}");

                if (registration.isStatic)
                {
                    // This layout is statically-mapped, skip dynamic layout creation
                    return registration.layoutName;
                }

                layoutName = registration.layoutName;
                baseLayout = registration.layoutName;
                mappings = registration.mappings;
            }
            else
            {
                LayoutMatchDebug("Falling back to joystick layout");
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

        [Conditional("HIDROGEN_TEST_PROJECT")]
        internal static void LayoutMatchDebug(string message)
        {
            Logging.Verbose(message);
        }
#endif

        public static void RegisterLayout<TDevice>(
            EvdevDeviceMapping mappings,
            EvdevDeviceMatcher matcher,
            EvdevLayoutMatchPredicate predicate = null
        )
            where TDevice : InputDevice
        {
            InputSystem.RegisterLayout<TDevice>();

#if UNITY_STANDALONE_LINUX
            // evdev layouts are mapped based on input codes, not raw bytes,
            // so we have to wait for layout finding so that we can get the evdev capabilities
            s_Registry.Add(new RegisteredLayout()
            {
                layoutName = typeof(TDevice).Name,
                mappings = mappings,
                matcher = matcher.ToMatcher(),
                predicate = predicate,
                isStatic = false,
            });
#endif
        }

        // While this registration will never end up actually using the control mappings outside of
        // compatibility checking, they're still nice to have for self-documentation
        public static void RegisterStaticLayout<TDevice>(
            EvdevDeviceMapping mappings,
            EvdevDeviceMatcher matcher,
            EvdevLayoutMatchPredicate predicate = null
        )
            where TDevice : InputDevice
        {
            InputSystem.RegisterLayout<TDevice>();

#if UNITY_STANDALONE_LINUX
            // While static layouts are not matched based on input codes, we still need to wait for
            // layout finding so we can validate that all expected inputs are available
            s_Registry.Add(new RegisteredLayout()
            {
                layoutName = typeof(TDevice).Name,
                mappings = mappings,
                matcher = matcher.ToMatcher(),
                predicate = predicate,
                isStatic = true,
            });
#endif
        }
    }
}
