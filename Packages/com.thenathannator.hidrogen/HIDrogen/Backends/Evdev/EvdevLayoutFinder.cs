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
            
            EvdevCapabilities descriptor;
            try
            {
                descriptor = JsonUtility.FromJson<EvdevCapabilities>(description.capabilities);
            }
            catch (Exception ex)
            {
                Logging.Exception("Failed to parse capabilities from evdev device", ex);
                return null;
            }

            if (descriptor.vendorId == 0 && descriptor.productId == 0)
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

            string layoutName;
            string baseLayout;
            EvdevDeviceMapping mappings;
            if (bestMatchIndex < 0)
            {
                baseLayout = nameof(EvdevDevice);
                layoutName = description.interfaceName;
                mappings = null;
            }
            else
            {
                var registration = s_Registry[bestMatchIndex];
                layoutName = registration.layoutName;
                baseLayout = registration.layoutName;
                mappings = registration.mappings;

                // Validate that all required mappings are available
                var availableKeys = new HashSet<input_key_code>();
                var availableAbs = new HashSet<input_abs_code>();

                foreach (var key in descriptor.supportedKeys)
                {
                    availableKeys.Add(key);
                }

                foreach (var abs in descriptor.supportedAbs)
                {
                    availableAbs.Add(abs);
                }

                foreach (var key in mappings.m_Keys.Keys)
                {
                    if (!availableKeys.Contains(key))
                    {
                        return null;
                    }
                }

                foreach (var abs in mappings.m_Abs.Keys)
                {
                    if (!availableAbs.Contains(abs))
                    {
                        return null;
                    }
                }

                foreach (var stick in mappings.m_Sticks.Keys)
                {
                    if (!availableAbs.Contains(stick) ||
                        !availableAbs.Contains(stick + 1))
                    {
                        return null;
                    }
                }

                foreach (var hat in mappings.m_Hats.Keys)
                {
                    if (hat < 0 || hat > 3)
                    {
                        return null;
                    }

                    if (availableAbs.Contains(input_abs_code.ABS_HAT0X + (hat * 2)) &&
                        availableAbs.Contains(input_abs_code.ABS_HAT0Y + (hat * 2)))
                    {
                        // present
                        continue;
                    }

                    // Any d-pad keys can stand in for hat 0; missing directions just never activate
                    if (hat == 0 &&
                        (availableKeys.Contains(input_key_code.BTN_DPAD_UP) ||
                        availableKeys.Contains(input_key_code.BTN_DPAD_DOWN) ||
                        availableKeys.Contains(input_key_code.BTN_DPAD_LEFT) ||
                        availableKeys.Contains(input_key_code.BTN_DPAD_RIGHT)))
                    {
                        // present
                        continue;
                    }

                    // missing
                    return null;
                }
            }

            layoutName += $"::{(int)descriptor.busType}-{descriptor.vendorId}-{descriptor.productId}";

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

            var matcher = InputDeviceMatcher.FromDeviceDescription(description)
                .WithCapability("driver", descriptor.driver)
                .WithCapability("busType", (int)descriptor.busType)
                .WithCapability("vendorId", descriptor.vendorId)
                .WithCapability("productId", descriptor.productId)
                .WithCapability("xpadSubtype", descriptor.xpadSubtype)
                .WithCapability("santrollerSubtype", descriptor.santrollerSubtype);

            var builder = new EvdevLayoutBuilder()
            {
                layoutName = layoutName,
                layoutDisplayName = description.product,
                baseLayout = baseLayout,
                descriptor = descriptor,
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
            // so we have to wait for layout finding so that we can get the evdev descriptor
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
