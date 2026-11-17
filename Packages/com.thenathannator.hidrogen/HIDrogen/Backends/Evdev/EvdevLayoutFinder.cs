using System;
using System.Text;
using HIDrogen.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend.Linux
{
    // Intentionally public and not platform guarded, for other packages to reference.
    public static class EvdevLayoutFinder
    {
        internal static void Initialize()
        {
            EvdevAxis.Initialize();
            EvdevDevice.Initialize();
            InputSystem.onFindLayoutForDevice += FindDeviceLayout;
        }

        private static readonly StringBuilder s_StringBuilder = new StringBuilder();

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
            
            EvdevDescriptor descriptor;
            try
            {
                descriptor = JsonUtility.FromJson<EvdevDescriptor>(description.capabilities);
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
                .WithCapability("vendorId", descriptor.vendorId)
                .WithCapability("productId", descriptor.productId);

            var builder = new Builder()
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

        private struct Builder
        {
            public string name;
            public string displayName;
            public string baseLayout;
            public EvdevDescriptor descriptor;

            public InputControlLayout Build()
            {
                var builder = new InputControlLayout.Builder
                {
                    name = name,
                    displayName = displayName,
                    extendsLayout = baseLayout,
                    stateFormat = EvdevDefinitions.InputFormat,
                };

                // Controls are laid out in the following order:
                // - Keys
                // - Abs
                // - Rel

                for (int i = 0; i < descriptor.supportedKeys.Length; i++)
                {
                    var key = descriptor.supportedKeys[i];
                    int byteOffset = i / 8;
                    int bitOffset = i % 8;

                    string name = ToCamelCase(key.ToString());

                    builder.AddControl(name)
                        .WithLayout("Button")
                        .WithByteOffset((uint)byteOffset)
                        .WithBitOffset((uint)bitOffset)
                        .WithFormat(InputStateBlock.FormatBit);
                }

                int keyEndBit = NumberUtils.AlignToMultiple(descriptor.supportedKeys.Length, 8);
                // Align for axis values
                int keyEndByte = NumberUtils.AlignToMultiple(keyEndBit / 8, sizeof(int));

                for (int i = 0; i < descriptor.supportedAbs.Length; i++)
                {
                    var abs = descriptor.supportedAbs[i];
                    var absInfo = descriptor.absInfo[i];

                    int byteOffset = keyEndByte + (i * sizeof(int));

                    string name = ToCamelCase(abs.ToString());

                    builder.AddControl(name)
                        .WithLayout("EvdevAxis")
                        .WithByteOffset((uint)byteOffset)
                        .WithFormat(InputStateBlock.FormatInt)
                        .WithParameters($"minValue={absInfo.minimum},maxValue={absInfo.maximum},zeroPoint=0");
                }

                // TODO: rel controls

                return builder.Build();
            }
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

        private static string ToCamelCase(string name)
        {
            s_StringBuilder.Clear();

            bool upper = false;
            foreach (char c in name)
            {
                if (c == '_')
                {
                    upper = true;
                    // skip underscores
                }
                else if (char.IsNumber(c))
                {
                    upper = true;
                    s_StringBuilder.Append(c);
                }
                else if (char.IsLetter(c))
                {
                    s_StringBuilder.Append(upper ? char.ToUpper(c) : char.ToLower(c));
                    upper = false;
                }
                else
                {
                    s_StringBuilder.Append(c);
                }
            }

            return s_StringBuilder.ToString();
        }
    }
}
