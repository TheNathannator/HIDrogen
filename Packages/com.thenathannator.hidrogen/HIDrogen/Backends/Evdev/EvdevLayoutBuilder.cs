using System;
using System.Collections.Generic;
using System.Text;
using HIDrogen.Imports.Linux;
using HIDrogen.Utilities;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend.Linux
{
    public struct EvdevControlMapping
    {
        public string path;
        public string displayName;

        public static implicit operator EvdevControlMapping(string path)
        {
            return new EvdevControlMapping()
            {
                path = path,
            };
        }
    }

    public class EvdevDeviceMapping
    {
        internal Dictionary<input_key_code, EvdevControlMapping> m_Keys;
        internal Dictionary<input_abs_code, EvdevControlMapping> m_Abs;
        internal Dictionary<int, EvdevControlMapping> m_Hats;

        public EvdevDeviceMapping WithKey(input_key_code key, EvdevControlMapping mapping)
        {
            if (m_Keys == null)
            {
                m_Keys = new Dictionary<input_key_code, EvdevControlMapping>();
            }

            if (m_Keys.ContainsKey(key))
            {
                throw new ArgumentException($"A mapping for {key} has already been added.");
            }

            m_Keys.Add(key, mapping);
            return this;
        }

        public EvdevDeviceMapping WithAbs(input_abs_code abs, EvdevControlMapping mapping)
        {
            if (m_Abs == null)
            {
                m_Abs = new Dictionary<input_abs_code, EvdevControlMapping>();
            }

            if (m_Abs.ContainsKey(abs))
            {
                throw new ArgumentException($"A mapping for {abs} has already been added.");
            }

            m_Abs.Add(abs, mapping);
            return this;
        }

        public EvdevDeviceMapping WithHat(int index, EvdevControlMapping mapping)
        {
            if (m_Hats == null)
            {
                m_Hats = new Dictionary<int, EvdevControlMapping>();
            }

            if (m_Hats.ContainsKey(index))
            {
                throw new ArgumentException($"A mapping for hat {index} has already been added.");
            }

            m_Hats.Add(index, mapping);
            return this;
        }
    }

    internal class EvdevLayoutBuilder
    {
        private static readonly StringBuilder s_StringBuilder = new StringBuilder();

        private static readonly (string prefix, string suffix)[] s_KeyDisplayNameFixes =
        {
            ("KEY_", "_KEY"),
            ("BTN_", "_BUTTON"),
        };

        private static readonly (string prefix, string suffix)[] s_AbsDisplayNameFixes =
        {
            ("ABS_", ""),
        };

        public string layoutName;
        public string layoutDisplayName;
        public string baseLayout;
        public EvdevCapabilities descriptor;
        public EvdevDeviceMapping mappings;

        private int m_HatCount;

        public InputControlLayout Build()
        {
            // Builders are persistent, make sure to reset any state here
            m_HatCount = 0;

            var builder = new InputControlLayout.Builder()
            {
                name = layoutName,
                displayName = layoutDisplayName,
                extendsLayout = baseLayout,
                stateFormat = EvdevDefinitions.InputFormat,
            };

            // Controls are laid out in the following order:
            // - Keys
            // - Abs
            // - Rel

            for (int keyIndex = 0; keyIndex < descriptor.supportedKeys.Length; keyIndex++)
            {
                MakeKeyControl(builder, ref keyIndex);
            }

            int keyEndBit = NumberUtils.AlignToMultiple(descriptor.supportedKeys.Length, 8);
            // Align for axis values
            int absStartByte = NumberUtils.AlignToMultiple(keyEndBit / 8, sizeof(int));

            for (int absIndex = 0; absIndex < descriptor.supportedAbs.Length; absIndex++)
            {
                MakeAbsControl(builder, ref absIndex, absStartByte);
            }

            // TODO: rel controls

            return builder.Build();
        }

        internal InputControlLayout.Builder.ControlBuilder AddControl(InputControlLayout.Builder builder, EvdevControlMapping mapping)
        {
            if (string.IsNullOrEmpty(mapping.path))
            {
                throw new InvalidOperationException($"No control path has been specified for this mapping!");
            }

            var control = builder.AddControl(mapping.path);

            if (!string.IsNullOrEmpty(mapping.displayName))
            {
                control = control.WithDisplayName(mapping.displayName);
            }

            return control;
        }

        private void MakeKeyControl(InputControlLayout.Builder builder, ref int keyIndex)
        {
            if (keyIndex + 3 < descriptor.supportedKeys.Length &&
                IsDpadButton(descriptor.supportedKeys[keyIndex + 0]) &&
                IsDpadButton(descriptor.supportedKeys[keyIndex + 1]) &&
                IsDpadButton(descriptor.supportedKeys[keyIndex + 2]) &&
                IsDpadButton(descriptor.supportedKeys[keyIndex + 3]))
            {
                // Group consecutive d-pad bits into a d-pad control
                MakeDpadControl(builder, ref keyIndex);
                return;
            }

            var key = descriptor.supportedKeys[keyIndex];

            if (mappings == null || !mappings.m_Keys.TryGetValue(key, out var mapping))
            {
                mapping = new EvdevControlMapping()
                {
                    path = ToCamelCase(key.ToString()),
                    displayName = ToDisplayName(key.ToString(), s_KeyDisplayNameFixes),
                };
            }

            AddControl(builder, mapping)
                .WithLayout("Button")
                .WithFormat(InputStateBlock.FormatBit)
                .WithByteOffset(GetKeyByteOffset(keyIndex))
                .WithBitOffset(GetKeyBitOffset(keyIndex));
        }

        private void MakeAbsControl(InputControlLayout.Builder builder, ref int absIndex, int absStartByte)
        {
            if (absIndex + 1 < descriptor.supportedAbs.Length &&
                IsHatPair(descriptor.supportedAbs[absIndex + 0], descriptor.supportedAbs[absIndex + 1]))
            {
                // Group consecutive hat abs into a d-pad control
                MakeHatControl(builder, ref absIndex, absStartByte);
                return;
            }

            var abs = descriptor.supportedAbs[absIndex];
            var absInfo = descriptor.absInfo[absIndex];

            string parameters = IsAbsY(abs)
                ? $"minValue={absInfo.maximum},maxValue={absInfo.minimum},zeroPoint=0"
                : $"minValue={absInfo.minimum},maxValue={absInfo.maximum},zeroPoint=0";

            if (mappings == null || !mappings.m_Abs.TryGetValue(abs, out var mapping))
            {
                mapping = new EvdevControlMapping()
                {
                    path = ToCamelCase(abs.ToString()),
                    displayName = ToDisplayName(abs.ToString(), s_AbsDisplayNameFixes),
                };
            }

            AddControl(builder, mapping)
                .WithLayout("EvdevAxis")
                .WithParameters(parameters)
                .WithFormat(InputStateBlock.FormatInt)
                .WithByteOffset(GetAbsByteOffset(absStartByte, absIndex))
                .WithBitOffset(0);
        }

        private InputControlLayout.Builder.ControlBuilder AddHatControl(
            InputControlLayout.Builder builder,
            out string rootPath
        )
        {
            int hatIndex = m_HatCount;
            m_HatCount++;

            if (mappings == null || !mappings.m_Hats.TryGetValue(hatIndex, out var mapping))
            {
                mapping = new EvdevControlMapping()
                {
                    path = $"dpad{hatIndex + 1}",
                    displayName = $"D-pad {hatIndex + 1}",
                };
            }

            rootPath = mapping.path;

            return AddControl(builder, mapping)
                .WithLayout("Dpad")
                .WithUsages(CommonUsages.Hatswitch);
        }

        private void MakeDpadControl(InputControlLayout.Builder builder, ref int keyIndex)
        {
            const int keyCount = 4;

            AddHatControl(builder, out string dpadRoot)
                .WithFormat(InputStateBlock.FormatBit)
                .WithByteOffset(GetKeyByteOffset(keyIndex))
                .WithBitOffset(GetKeyBitOffset(keyIndex))
                .WithSizeInBits(keyCount);

            for (int i = 0; i < keyCount; i++)
            {
                var key = descriptor.supportedKeys[keyIndex + i];

                string path;
                switch (key)
                {
                    case input_key_code.BTN_DPAD_UP: path = $"{dpadRoot}/up"; break;
                    case input_key_code.BTN_DPAD_DOWN: path = $"{dpadRoot}/down"; break;
                    case input_key_code.BTN_DPAD_LEFT: path = $"{dpadRoot}/left"; break;
                    case input_key_code.BTN_DPAD_RIGHT: path = $"{dpadRoot}/right"; break;
                    default: throw new Exception($"{key} is not a d-pad key");
                }

                builder.AddControl(path)
                    .WithLayout("Button")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetKeyByteOffset(i)) // no keyIndex, this is relative to the parent
                    .WithBitOffset(GetKeyBitOffset(i)) // no keyIndex, this is relative to the parent
                    .WithSizeInBits(1);
            }

            keyIndex += keyCount - 1; // - 1 to counteract the increment in the main loop
        }

        private void MakeHatControl(InputControlLayout.Builder builder, ref int absIndex, int absStartByte)
        {
            const int absCount = 2;

            AddHatControl(builder, out string hatRoot)
                .WithByteOffset(GetAbsByteOffset(absStartByte, absIndex))
                .WithSizeInBits(absCount * sizeof(int) * 8);

            for (int i = 0; i < absCount; i++)
            {
                var abs = descriptor.supportedAbs[absIndex + i];
                var absInfo = descriptor.absInfo[absIndex + i];

                // note: Y axis is inverted
                string positive = IsHatX(abs) ? "right" : "down";
                string negative = IsHatX(abs) ? "left" : "up";

                builder.AddControl($"{hatRoot}/{positive}")
                    .WithLayout("DiscreteButton")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0)
                    .WithParameters($"nullValue=0,minValue=-1,maxValue={absInfo.minimum}");

                builder.AddControl($"{hatRoot}/{negative}")
                    .WithLayout("DiscreteButton")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0)
                    .WithParameters($"nullValue=0,minValue=1,maxValue={absInfo.maximum}");
            }

            absIndex += absCount - 1; // - 1 to counteract the increment in the main loop
        }

        private uint GetKeyByteOffset(int keyIndex)
        {
            return (uint)(keyIndex / 8);
        }

        private uint GetKeyBitOffset(int keyIndex)
        {
            return (uint)(keyIndex % 8);
        }

        private uint GetAbsByteOffset(int absStartByte, int absIndex)
        {
            return (uint)(absStartByte + (absIndex * sizeof(int)));
        }

        private static bool IsAbsX(input_abs_code abs)
        {
            return
                abs == input_abs_code.ABS_X ||
                abs == input_abs_code.ABS_RX ||
                abs == input_abs_code.ABS_TILT_X ||
                abs == input_abs_code.ABS_HAT0X ||
                abs == input_abs_code.ABS_HAT1X ||
                abs == input_abs_code.ABS_HAT2X ||
                abs == input_abs_code.ABS_HAT3X;
        }

        private static bool IsAbsY(input_abs_code abs)
        {
            return
                abs == input_abs_code.ABS_Y ||
                abs == input_abs_code.ABS_RY ||
                abs == input_abs_code.ABS_TILT_Y ||
                abs == input_abs_code.ABS_HAT0Y ||
                abs == input_abs_code.ABS_HAT1Y ||
                abs == input_abs_code.ABS_HAT2Y ||
                abs == input_abs_code.ABS_HAT3Y;
        }

        private static bool IsDpadButton(input_key_code key)
        {
            return
                key == input_key_code.BTN_DPAD_UP ||
                key == input_key_code.BTN_DPAD_DOWN ||
                key == input_key_code.BTN_DPAD_LEFT ||
                key == input_key_code.BTN_DPAD_RIGHT;
        }

        private static bool IsHatPair(input_abs_code left, input_abs_code right)
        {
            return
                IsHatX(left) != IsHatX(right) &&
                IsHatY(left) != IsHatY(right) &&
                GetHatNum(left) == GetHatNum(right);
        }

        private static bool IsHatX(input_abs_code abs)
        {
            return
                abs == input_abs_code.ABS_HAT0X ||
                abs == input_abs_code.ABS_HAT1X ||
                abs == input_abs_code.ABS_HAT2X ||
                abs == input_abs_code.ABS_HAT3X;
        }

        private static bool IsHatY(input_abs_code abs)
        {
            return
                abs == input_abs_code.ABS_HAT0Y ||
                abs == input_abs_code.ABS_HAT1Y ||
                abs == input_abs_code.ABS_HAT2Y ||
                abs == input_abs_code.ABS_HAT3Y;
        }

        private static int GetHatNum(input_abs_code abs)
        {
            if (abs == input_abs_code.ABS_HAT0X || abs == input_abs_code.ABS_HAT0Y)
            {
                return 0;
            }

            if (abs == input_abs_code.ABS_HAT1X || abs == input_abs_code.ABS_HAT1Y)
            {
                return 1;
            }

            if (abs == input_abs_code.ABS_HAT2X || abs == input_abs_code.ABS_HAT2Y)
            {
                return 2;
            }

            if (abs == input_abs_code.ABS_HAT3X || abs == input_abs_code.ABS_HAT3Y)
            {
                return 3;
            }

            return -1;
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

        private static string ToDisplayName(string name, (string prefix, string suffix)[] fixes)
        {
            foreach (var (prefix, suffix) in fixes)
            {
                if (name.StartsWith(prefix))
                {
                    name = name.Substring(prefix.Length) + suffix;
                }
            }

            s_StringBuilder.Clear();

            bool upper = true;
            foreach (char c in name)
            {
                if (c == '_')
                {
                    upper = true;
                    s_StringBuilder.Append(' ');
                }
                else if (char.IsNumber(c))
                {
                    upper = true;
                    s_StringBuilder.Append(c);
                    s_StringBuilder.Append(' ');
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
