using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend.Linux
{
    // Intentionally public and not platform guarded, for other packages to reference.
    public static class EvdevLayoutFinder
    {
        private static readonly StringBuilder s_StringBuilder = new StringBuilder();

        internal static void Initialize()
        {
            EvdevAxis.Initialize();
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

            // TODO: Allow overrides to be registered
            string baseLayout = nameof(EvdevDevice);

            string layoutName = $"{description.interfaceName}::{descriptor.vendorId}::{descriptor.productId}";

            if (!string.IsNullOrEmpty(description.manufacturer))
            {
                layoutName += $"::{SanitizeName(description.manufacturer)}";
            }

            if (!string.IsNullOrEmpty(description.product))
            {
                layoutName += $"::{SanitizeName(description.product)}";
            }

            var matcher = InputDeviceMatcher.FromDeviceDescription(description)
                .WithCapability("busType", (int)descriptor.busType)
                .WithCapability("vendorId", descriptor.vendorId)
                .WithCapability("productId", descriptor.productId);

            var builder = new EvdevLayoutBuilder()
            {
                name = layoutName,
                displayName = description.product,
                baseLayout = baseLayout,
                descriptor = descriptor,
            };
            InputSystem.RegisterLayoutBuilder(
                () => builder.Build(),
                layoutName,
                baseLayout,
                matcher
            );

            return layoutName;
        }

        private static string SanitizeName(string name)
        {
            s_StringBuilder.Clear();

            foreach (char c in name)
            {
                if (char.IsLetter(c) || char.IsDigit(c))
                {
                    s_StringBuilder.Append(c);
                }
            }

            return s_StringBuilder.ToString();
        }
    }
}
