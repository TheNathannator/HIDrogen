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
    [Flags]
    public enum EvdevAxisOption
    {
        None = 0,

        /// <summary>
        /// The axis rests at its minimum value instead of its midpoint.
        /// </summary>
        Unsigned = 1 << 0,

        /// <summary>
        /// The axis range is inverted (i.e. swap min and max).
        /// </summary>
        Inverted = 1 << 1,
    }

    [Flags]
    public enum EvdevStickOption
    {
        None = 0,

        /// <summary>
        /// The X axis range is inverted (i.e. swap min and max).
        /// </summary>
        InvertX = 1 << 0,

        /// <summary>
        /// The Y axis range is inverted (i.e. swap min and max).
        /// </summary>
        InvertY = 1 << 1,
    }

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
        internal readonly Dictionary<input_key_code, EvdevControlMapping> m_Keys
            = new Dictionary<input_key_code, EvdevControlMapping>();
        internal readonly Dictionary<input_abs_code, (EvdevControlMapping mapping, EvdevAxisOption options)> m_Abs
            = new Dictionary<input_abs_code, (EvdevControlMapping, EvdevAxisOption)>();
        internal readonly Dictionary<int, EvdevControlMapping> m_Hats
            = new Dictionary<int, EvdevControlMapping>();

        internal readonly Dictionary<input_abs_code, EvdevControlMapping> m_AbsButtons
            = new Dictionary<input_abs_code, EvdevControlMapping>();
        internal readonly Dictionary<input_abs_code, (EvdevControlMapping mapping, EvdevStickOption options)> m_Sticks
            = new Dictionary<input_abs_code, (EvdevControlMapping, EvdevStickOption)>();

        private void EnsureAbsUnique(input_abs_code abs)
        {
            if (m_Abs.ContainsKey(abs))
            {
                throw new ArgumentException($"An axis mapping for {abs} has already been added.");
            }

            if (m_AbsButtons.ContainsKey(abs))
            {
                throw new ArgumentException($"A button mapping for {abs} has already been added.");
            }

            if (m_Sticks.ContainsKey(abs))
            {
                throw new ArgumentException($"A stick mapping for {abs} has already been added.");
            }
        }

        public EvdevDeviceMapping WithKey(input_key_code key, EvdevControlMapping mapping)
        {
            if (m_Keys.ContainsKey(key))
            {
                throw new ArgumentException($"A mapping for {key} has already been added.");
            }

            m_Keys.Add(key, mapping);
            return this;
        }

        public EvdevDeviceMapping WithAbs(
            input_abs_code abs,
            EvdevControlMapping mapping,
            EvdevAxisOption options = EvdevAxisOption.None
        )
        {
            EnsureAbsUnique(abs);
            m_Abs.Add(abs, (mapping, options));
            return this;
        }

        public EvdevDeviceMapping WithHat(int index, EvdevControlMapping mapping)
        {
            if (m_Hats.ContainsKey(index))
            {
                throw new ArgumentException($"A mapping for hat {index} has already been added.");
            }

            m_Hats.Add(index, mapping);
            return this;
        }

        public EvdevDeviceMapping WithAbsButton(input_abs_code abs, EvdevControlMapping mapping)
        {
            EnsureAbsUnique(abs);
            m_AbsButtons.Add(abs, mapping);
            return this;
        }

        public EvdevDeviceMapping WithStick(
            input_abs_code abs,
            EvdevControlMapping mapping,
            EvdevStickOption options = EvdevStickOption.None
        )
        {
            EnsureAbsUnique(abs);
            EnsureAbsUnique(abs + 1); // The next abs needs to be available, as they will be paired together
            m_Sticks.Add(abs, (mapping, options));
            m_Sticks.Add(abs + 1, (mapping, options));
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

        private readonly HashSet<input_key_code> m_AvailableKeys = new HashSet<input_key_code>();
        private readonly HashSet<input_abs_code> m_AvailableAbs = new HashSet<input_abs_code>();

        private int m_HatCount;

        public InputControlLayout Build()
        {
            // Builders are persistent, make sure to reset any state here
            m_HatCount = 0;
            m_AvailableKeys.Clear();
            m_AvailableAbs.Clear();

            var builder = new InputControlLayout.Builder()
            {
                name = layoutName,
                displayName = layoutDisplayName,
                extendsLayout = baseLayout,
                stateFormat = EvdevDefinitions.InputFormat,
            };

            foreach (var key in descriptor.supportedKeys)
            {
                m_AvailableKeys.Add(key);
            }

            foreach (var abs in descriptor.supportedAbs)
            {
                m_AvailableAbs.Add(abs);
            }

            // Controls are laid out in the following order:
            // - Keys
            // - Abs
            // - Rel

            for (int keyIndex = 0; keyIndex < descriptor.supportedKeys.Length; keyIndex++)
            {
                var key = descriptor.supportedKeys[keyIndex];
                if (m_AvailableKeys.Contains(key))
                {
                    MakeKeyControl(builder, keyIndex);
                    m_AvailableKeys.Remove(key);
                }
            }

            int keyEndBit = NumberUtils.AlignToMultiple(descriptor.supportedKeys.Length, 8);
            // Align for axis values
            int absStartByte = NumberUtils.AlignToMultiple(keyEndBit / 8, sizeof(int));

            for (int absIndex = 0; absIndex < descriptor.supportedAbs.Length; absIndex++)
            {
                var abs = descriptor.supportedAbs[absIndex];
                if (m_AvailableAbs.Contains(abs))
                {
                    MakeAbsControl(builder, absIndex, absStartByte);
                    m_AvailableAbs.Remove(abs);
                }
            }

            // TODO: rel controls

            return builder.Build();
        }

        private void MakeKeyControl(InputControlLayout.Builder builder, int keyIndex)
        {
            var key = descriptor.supportedKeys[keyIndex];

            if (key == input_key_code.BTN_DPAD_UP &&
                !IsKeyMapped(input_key_code.BTN_DPAD_UP) &&
                !IsKeyMapped(input_key_code.BTN_DPAD_DOWN) &&
                !IsKeyMapped(input_key_code.BTN_DPAD_LEFT) &&
                !IsKeyMapped(input_key_code.BTN_DPAD_RIGHT))
            {
                // Group d-pad buttons
                MakeDpadControl(builder, keyIndex);
                return;
            }

            var mapping = new EvdevControlMapping()
            {
                path = ToCamelCase(key.ToString()),
                displayName = ToDisplayName(key.ToString(), s_KeyDisplayNameFixes),
            };
            if (mappings != null && mappings.m_Keys.TryGetValue(key, out var buttonMapping))
            {
                mapping = buttonMapping;
            }

            AddControl(builder, mapping)
                .WithLayout("Button")
                .WithFormat(InputStateBlock.FormatBit)
                .WithByteOffset(GetKeyByteOffset(keyIndex))
                .WithBitOffset(GetKeyBitOffset(keyIndex));
        }

        private void MakeAbsControl(InputControlLayout.Builder builder, int absIndex, int absStartByte)
        {
            var abs = descriptor.supportedAbs[absIndex];

            if (IsHatX(abs) &&
                !IsAbsMapped(abs) &&
                !IsAbsMapped(abs + 1))
            {
                // Group hat axes
                MakeHatControl(builder, absIndex, absStartByte);
                return;
            }

            if (mappings != null && mappings.m_Sticks.ContainsKey(abs))
            {
                MakeStickControl(builder, absIndex, absStartByte);
                return;
            }

            var absInfo = descriptor.absInfo[absIndex];

            int zero = GetAbsZeroPoint(absInfo);
            int min = GetAbsMin(abs, absInfo);
            int max = GetAbsMax(abs, absInfo);

            string layout = "EvdevAxis";
            string parameters = $"zeroPoint={zero},minValue={min},maxValue={max}";
            var mapping = new EvdevControlMapping()
            {
                path = ToCamelCase(abs.ToString()),
                displayName = ToDisplayName(abs.ToString(), s_AbsDisplayNameFixes),
            };
            if (mappings != null)
            {
                if (mappings.m_Abs.TryGetValue(abs, out var a))
                {
                    if ((a.options & EvdevAxisOption.Inverted) != 0)
                    {
                        (min, max) = (max, min);
                    }

                    // apply this *after* inversion to simplify logic
                    if ((a.options & EvdevAxisOption.Unsigned) != 0)
                    {
                        zero = min;
                    }

                    mapping = a.mapping;
                    parameters = $"zeroPoint={zero},minValue={min},maxValue={max}";
                }
                else if (mappings.m_AbsButtons.TryGetValue(abs, out var buttonMapping))
                {
                    mapping = buttonMapping;
                    layout = "EvdevButton";
                    parameters = $"minValue={min},maxValue={max}";
                }
            }

            AddControl(builder, mapping)
                .WithLayout(layout)
                .WithParameters(parameters)
                .WithFormat(InputStateBlock.FormatInt)
                .WithByteOffset(GetAbsByteOffset(absStartByte, absIndex))
                .WithBitOffset(0);
        }

        private void MakeDpadControl(InputControlLayout.Builder builder, int keyIndex)
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
                    .WithFormat(InputStateBlock.FormatBit)
                    .WithByteOffset(GetKeyByteOffset(i)) // no keyIndex, this is relative to the parent
                    .WithBitOffset(GetKeyBitOffset(i)) // no keyIndex, this is relative to the parent
                    .WithSizeInBits(1);
            }

            // Consume key bits
            m_AvailableKeys.Remove(input_key_code.BTN_DPAD_UP);
            m_AvailableKeys.Remove(input_key_code.BTN_DPAD_DOWN);
            m_AvailableKeys.Remove(input_key_code.BTN_DPAD_LEFT);
            m_AvailableKeys.Remove(input_key_code.BTN_DPAD_RIGHT);

            // Both d-pad buttons and hat 0 may be specified as supported; prefer the buttons over the hat axes
            // (but make sure to respect mapping overrides)
            if (!IsAbsMapped(input_abs_code.ABS_HAT0X))
            {
                m_AvailableAbs.Remove(input_abs_code.ABS_HAT0X);
            }

            if (!IsAbsMapped(input_abs_code.ABS_HAT0Y))
            {
                m_AvailableAbs.Remove(input_abs_code.ABS_HAT0Y);
            }
        }

        private void MakeHatControl(InputControlLayout.Builder builder, int absIndex, int absStartByte)
        {
            const int absCount = 2;

            AddHatControl(builder, out string hatRoot)
                .WithByteOffset(GetAbsByteOffset(absStartByte, absIndex))
                .WithSizeInBits(absCount * sizeof(int) * 8);

            for (int i = 0; i < absCount; i++)
            {
                var abs = descriptor.supportedAbs[absIndex + i];
                var absInfo = descriptor.absInfo[absIndex + i];

                string positive = i == 0 ? "right" : "up";
                string negative = i == 0 ? "left" : "down";

                int zero = GetAbsZeroPoint(absInfo);
                int min = GetAbsMin(abs, absInfo);
                int max = GetAbsMax(abs, absInfo);

                builder.AddControl($"{hatRoot}/{positive}")
                    .WithLayout("EvdevButton")
                    .WithParameters($"minValue={zero},maxValue={max}")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0);

                builder.AddControl($"{hatRoot}/{negative}")
                    .WithLayout("EvdevButton")
                    .WithParameters($"minValue={zero},maxValue={min}")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0);
            }

            // Consume abs bits
            m_AvailableAbs.Remove(descriptor.supportedAbs[absIndex]);
            m_AvailableAbs.Remove(descriptor.supportedAbs[absIndex + 1]);
        }

        private void MakeStickControl(InputControlLayout.Builder builder, int absIndex, int absStartByte)
        {
            const int absCount = 2;

            var (mapping, options) = mappings.m_Sticks[descriptor.supportedAbs[absIndex]];

            string stickRoot = mapping.path;
            AddControl(builder, mapping)
                .WithLayout("Stick")
                .WithByteOffset(GetAbsByteOffset(absStartByte, absIndex))
                .WithSizeInBits(absCount * sizeof(int) * 8);

            for (int i = 0; i < 2; i++)
            {
                var abs = descriptor.supportedAbs[absIndex + i];
                var absInfo = descriptor.absInfo[absIndex + i];

                string axis = i == 0 ? "x" : "y";
                string positive = i == 0 ? "right" : "up";
                string negative = i == 0 ? "left" : "down";

                int zero = GetAbsZeroPoint(absInfo);
                int min = GetAbsMin(abs, absInfo);
                int max = GetAbsMax(abs, absInfo);

                bool inverted = i == 0
                    ? (options & EvdevStickOption.InvertX) != 0
                    : (options & EvdevStickOption.InvertY) != 0;

                if (inverted)
                {
                    (min, max) = (max, min);
                }

                builder.AddControl($"{stickRoot}/{axis}")
                    .WithLayout("EvdevAxis")
                    .WithParameters($"zeroPoint={zero},minValue={min},maxValue={max}")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0);

                builder.AddControl($"{stickRoot}/{positive}")
                    .WithLayout("EvdevButton")
                    .WithParameters($"minValue={zero},maxValue={max}")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0);

                builder.AddControl($"{stickRoot}/{negative}")
                    .WithLayout("EvdevButton")
                    .WithParameters($"minValue={zero},maxValue={min}")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0);
            }

            // Consume abs bits
            m_AvailableAbs.Remove(descriptor.supportedAbs[absIndex]);
            m_AvailableAbs.Remove(descriptor.supportedAbs[absIndex + 1]);
        }

        private InputControlLayout.Builder.ControlBuilder AddControl(InputControlLayout.Builder builder, EvdevControlMapping mapping)
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

        private bool IsKeyMapped(input_key_code key)
        {
            if (!m_AvailableKeys.Contains(key))
            {
                return true;
            }

            if (mappings == null)
            {
                return false;
            }

            return mappings.m_Keys.ContainsKey(key);
        }

        private bool IsAbsMapped(input_abs_code abs)
        {
            if (!m_AvailableAbs.Contains(abs))
            {
                return true;
            }

            if (mappings == null)
            {
                return false;
            }

            return mappings.m_Abs.ContainsKey(abs) ||
                mappings.m_AbsButtons.ContainsKey(abs) ||
                mappings.m_Sticks.ContainsKey(abs);
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

        private int GetAbsZeroPoint(input_absinfo absInfo)
        {
            return absInfo.minimum + (absInfo.maximum - absInfo.minimum) / 2;
        }

        private int GetAbsMin(input_abs_code abs, input_absinfo absInfo)
        {
            return IsAbsY(abs) ? absInfo.maximum : absInfo.minimum;
        }

        private int GetAbsMax(input_abs_code abs, input_absinfo absInfo)
        {
            return IsAbsY(abs) ? absInfo.minimum : absInfo.maximum;
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
