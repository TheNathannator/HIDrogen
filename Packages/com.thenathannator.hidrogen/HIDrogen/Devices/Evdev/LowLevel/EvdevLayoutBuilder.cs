using System;
using System.Collections.Generic;
using System.Text;
using HIDrogen.Controls;
using HIDrogen.Imports.Linux;
using HIDrogen.Utilities;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Devices.LowLevel
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
    public enum EvdevButtonAxisOption
    {
        None = 0,

        /// <summary>
        /// The axis range is inverted (i.e. swap min and max).
        /// </summary>
        Inverted = 1 << 0,
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
        internal readonly SortedDictionary<input_key_code, EvdevControlMapping> m_Keys
            = new SortedDictionary<input_key_code, EvdevControlMapping>();
        internal readonly SortedDictionary<input_abs_code, (EvdevControlMapping mapping, EvdevAxisOption options)> m_Abs
            = new SortedDictionary<input_abs_code, (EvdevControlMapping, EvdevAxisOption)>();
        internal readonly SortedDictionary<int, EvdevControlMapping> m_Hats
            = new SortedDictionary<int, EvdevControlMapping>();

        internal readonly SortedDictionary<input_abs_code, (EvdevControlMapping mapping, EvdevButtonAxisOption options)> m_AbsButtons
            = new SortedDictionary<input_abs_code, (EvdevControlMapping, EvdevButtonAxisOption)>();
        internal readonly SortedDictionary<input_abs_code, (EvdevControlMapping mapping, EvdevStickOption options)> m_AbsSticks
            = new SortedDictionary<input_abs_code, (EvdevControlMapping, EvdevStickOption)>();

        public EvdevDeviceMapping()
        {
        }

        private EvdevDeviceMapping(EvdevDeviceMapping other)
        {
            m_Keys = new SortedDictionary<input_key_code, EvdevControlMapping>(other.m_Keys);
            m_Abs = new SortedDictionary<input_abs_code, (EvdevControlMapping, EvdevAxisOption)>(other.m_Abs);
            m_Hats = new SortedDictionary<int, EvdevControlMapping>(other.m_Hats);
            m_AbsButtons = new SortedDictionary<input_abs_code, (EvdevControlMapping, EvdevButtonAxisOption)>(other.m_AbsButtons);
            m_AbsSticks = new SortedDictionary<input_abs_code, (EvdevControlMapping, EvdevStickOption)>(other.m_AbsSticks);
        }

        public EvdevDeviceMapping Clone()
        {
            return new EvdevDeviceMapping(this);
        }

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

            if (m_AbsSticks.ContainsKey(abs))
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

        public EvdevDeviceMapping WithAbsButton(
            input_abs_code abs,
            EvdevControlMapping mapping,
            EvdevButtonAxisOption options = EvdevButtonAxisOption.None
        )
        {
            EnsureAbsUnique(abs);
            m_AbsButtons.Add(abs, (mapping, options));
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
            m_AbsSticks.Add(abs, (mapping, options));
            m_AbsSticks.Add(abs + 1, (mapping, options));
            return this;
        }

        public bool IsCompatible(EvdevCapabilities capabilities)
        {
            // Both the supported inputs and the desired mappings are guaranteed to be sorted,
            // which we can take advantage of to avoid needless allocations of a hash set,
            // at the cost of less convenient checking of whether a mapping is supported.

            bool FindValue<T>(T target, T[] values, ref int index)
            {
                // enums are not IComparable<TSelf>...
                var comparer = Comparer<T>.Default;

                while (index < values.Length && comparer.Compare(values[index], target) < 0)
                {
                    index++;
                }

                return comparer.Compare(values[index], target) == 0;
            }

            int keyIndex = 0;
            foreach (var key in m_Keys.Keys)
            {
                if (!FindValue(key, capabilities.supportedKeys, ref keyIndex))
                {
                    return false;
                }
            }

            int absIndex = 0;
            foreach (var abs in m_Abs.Keys)
            {
                if (!FindValue(abs, capabilities.supportedAbs, ref absIndex))
                {
                    return false;
                }
            }

            keyIndex = 0;
            absIndex = 0;
            foreach (int hat in m_Hats.Keys)
            {
                if (hat < 0 || hat > 3)
                {
                    // invalid hat
                    return false;
                }

                var absX = input_abs_code.ABS_HAT0X + (hat * 2);
                var absY = input_abs_code.ABS_HAT0Y + (hat * 2);

                if (FindValue(absX, capabilities.supportedAbs, ref absIndex) &&
                    FindValue(absY, capabilities.supportedAbs, ref absIndex))
                {
                    // present
                    continue;
                }

                if (hat == 0 &&
                    // Not all d-pad keys may be available, but we still
                    // make a hat from them so long as any of them are available
                    (FindValue(input_key_code.BTN_DPAD_UP, capabilities.supportedKeys, ref keyIndex) ||
                    FindValue(input_key_code.BTN_DPAD_DOWN, capabilities.supportedKeys, ref keyIndex) ||
                    FindValue(input_key_code.BTN_DPAD_LEFT, capabilities.supportedKeys, ref keyIndex) ||
                    FindValue(input_key_code.BTN_DPAD_RIGHT, capabilities.supportedKeys, ref keyIndex))
                )
                {
                    // present
                    continue;
                }

                // missing
                return false;
            }

            absIndex = 0;
            foreach (var abs in m_AbsButtons.Keys)
            {
                if (!FindValue(abs, capabilities.supportedAbs, ref absIndex))
                {
                    return false;
                }
            }

            absIndex = 0;
            foreach (var abs in m_AbsSticks.Keys)
            {
                if (!FindValue(abs, capabilities.supportedAbs, ref absIndex))
                {
                    return false;
                }
            }

            return true;
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
        public EvdevCapabilities capabilities;
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

            foreach (var key in capabilities.supportedKeys)
            {
                m_AvailableKeys.Add(key);
            }

            foreach (var abs in capabilities.supportedAbs)
            {
                m_AvailableAbs.Add(abs);
            }

            // Controls are laid out in the following order:
            // - Keys
            // - Abs
            // - Rel

            for (int keyIndex = 0; keyIndex < capabilities.supportedKeys.Length; keyIndex++)
            {
                var key = capabilities.supportedKeys[keyIndex];
                if (m_AvailableKeys.Contains(key))
                {
                    MakeKeyControl(builder, keyIndex);
                    m_AvailableKeys.Remove(key);
                }
            }

            int keyEndBit = NumberUtils.AlignToMultiple(capabilities.supportedKeys.Length, 8);
            // Align for axis values
            int absStartByte = NumberUtils.AlignToMultiple(keyEndBit / 8, sizeof(int));

            for (int absIndex = 0; absIndex < capabilities.supportedAbs.Length; absIndex++)
            {
                var abs = capabilities.supportedAbs[absIndex];
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
            var key = capabilities.supportedKeys[keyIndex];

            // D-pad buttons are grouped together regardless of if they're all available,
            // but only when encountering the first one that isn't explicitly mapped
            if (IsDpadKey(key) && IsKeyAvailable(key))
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
            var abs = capabilities.supportedAbs[absIndex];

            if (IsHatX(abs) &&
                // Only group abs hats if both axes are available
                IsAbsAvailable(abs) &&
                IsAbsAvailable(abs + 1))
            {
                // Group hat axes
                MakeHatControl(builder, absIndex, absStartByte);
                return;
            }

            if (mappings != null && mappings.m_AbsSticks.ContainsKey(abs))
            {
                MakeStickControl(builder, absIndex, absStartByte);
                return;
            }

            var absInfo = capabilities.absInfo[absIndex];

            int zero = GetAbsZeroPoint(absInfo);
            int min = absInfo.minimum;
            int max = absInfo.maximum;

            // Invert Y axes, except for hats since their non-hat usages are typically not inverted
            if (IsAxisY(abs) /* || IsHatY(abs) */)
            {
                (min, max) = (max, min);
            }

            string layout = IntegerAxisControl.LayoutName;
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
                else if (mappings.m_AbsButtons.TryGetValue(abs, out var b))
                {
                    if ((b.options & EvdevButtonAxisOption.Inverted) != 0)
                    {
                        (min, max) = (max, min);
                    }

                    mapping = b.mapping;
                    layout = IntegerButtonControl.LayoutName;
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
            const int totalKeyCount = 4;

            uint keyCount = 0;
            for (int i = keyIndex; i < capabilities.supportedKeys.Length; i++)
            {
                // D-pad keys are sequential; bail when we see something else
                if (!IsDpadKey(capabilities.supportedKeys[i]))
                {
                    break;
                }

                keyCount++;
            }

            AddHatControl(builder, out string dpadRoot)
                .WithFormat(InputStateBlock.FormatBit)
                .WithByteOffset(GetKeyByteOffset(keyIndex))
                .WithBitOffset(GetKeyBitOffset(keyIndex))
                .WithSizeInBits(keyCount);

            for (int d = 0; d < totalKeyCount; d++)
            {
                var dpadKey = input_key_code.BTN_DPAD_UP + d;

                int dpadKeyIndex = -1;
                if (IsKeyAvailable(dpadKey))
                {
                    for (int i = keyIndex; i < capabilities.supportedKeys.Length; i++)
                    {
                        var key = capabilities.supportedKeys[i];
                        if (!IsDpadKey(key))
                        {
                            break;
                        }

                        if (key == dpadKey)
                        {
                            dpadKeyIndex = i;
                            break;
                        }
                    }
                }

                string path;
                switch (dpadKey)
                {
                    case input_key_code.BTN_DPAD_UP: path = $"{dpadRoot}/up"; break;
                    case input_key_code.BTN_DPAD_DOWN: path = $"{dpadRoot}/down"; break;
                    case input_key_code.BTN_DPAD_LEFT: path = $"{dpadRoot}/left"; break;
                    case input_key_code.BTN_DPAD_RIGHT: path = $"{dpadRoot}/right"; break;
                    default: throw new Exception($"{dpadKey} is not a d-pad key");
                }

                if (dpadKeyIndex >= 0)
                {
                    builder.AddControl(path)
                        .WithLayout("Button")
                        .WithFormat(InputStateBlock.FormatBit)
                        // Byte offset is relative to the parent control
                        .WithByteOffset(GetKeyByteOffset(dpadKeyIndex) - GetKeyByteOffset(keyIndex))
                        // Bit offset is not, however
                        .WithBitOffset(GetKeyBitOffset(dpadKeyIndex))
                        .WithSizeInBits(1);

                    // Consume key bit
                    m_AvailableKeys.Remove(dpadKey);
                }
                else
                {
                    // Not all 4 d-pad directions are guaranteed to be present from evdev, but
                    // d-pad controls will always have all 4 directions regardless of if we specify
                    // them here. The input system does not provide a way of making a control
                    // "dead" or inactive, so we need a workaround to emulate such behavior.
                    //
                    // We set the scale of the button control to 0 to effectively disable it, such
                    // that it always reports 0. Additionally, although this isn't *strictly*
                    // necessary to my knowledge, we ensure the position of the control is within
                    // the bounds of the parent d-pad control by moving it to the start; otherwise
                    // the default bit offsets supplied by the d-pad control will be used instead.
                    builder.AddControl(path)
                        .WithLayout("Button")
                        .WithParameters("scale,scaleFactor=0")
                        .WithFormat(InputStateBlock.FormatBit)
                        .WithByteOffset(0)
                        .WithBitOffset(GetKeyBitOffset(keyIndex))
                        .WithSizeInBits(1);
                }
            }

            // Both d-pad buttons and hat 0 may be specified as supported at the same time;
            // prefer the buttons over the hat axes (but make sure to respect mapping overrides)
            if (IsAbsAvailable(input_abs_code.ABS_HAT0X))
            {
                m_AvailableAbs.Remove(input_abs_code.ABS_HAT0X);
            }

            if (IsAbsAvailable(input_abs_code.ABS_HAT0Y))
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
                var abs = capabilities.supportedAbs[absIndex + i];
                var absInfo = capabilities.absInfo[absIndex + i];

                string positive = i == 0 ? "right" : "up";
                string negative = i == 0 ? "left" : "down";

                int zero = GetAbsZeroPoint(absInfo);
                int min = absInfo.minimum;
                int max = absInfo.maximum;

                // Y axis is inverted
                if (IsHatY(abs))
                {
                    (min, max) = (max, min);
                }

                builder.AddControl($"{hatRoot}/{positive}")
                    .WithLayout(IntegerButtonControl.LayoutName)
                    .WithParameters($"minValue={zero},maxValue={max}")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0);

                builder.AddControl($"{hatRoot}/{negative}")
                    .WithLayout(IntegerButtonControl.LayoutName)
                    .WithParameters($"minValue={zero},maxValue={min}")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0);
            }

            // Consume abs bits
            m_AvailableAbs.Remove(capabilities.supportedAbs[absIndex]);
            m_AvailableAbs.Remove(capabilities.supportedAbs[absIndex + 1]);
        }

        private void MakeStickControl(InputControlLayout.Builder builder, int absIndex, int absStartByte)
        {
            const int absCount = 2;

            var (mapping, options) = mappings.m_AbsSticks[capabilities.supportedAbs[absIndex]];

            string stickRoot = mapping.path;
            AddControl(builder, mapping)
                .WithLayout("Stick")
                .WithByteOffset(GetAbsByteOffset(absStartByte, absIndex))
                .WithSizeInBits(absCount * sizeof(int) * 8);

            for (int i = 0; i < 2; i++)
            {
                var abs = capabilities.supportedAbs[absIndex + i];
                var absInfo = capabilities.absInfo[absIndex + i];

                string axis = i == 0 ? "x" : "y";
                string positive = i == 0 ? "right" : "up";
                string negative = i == 0 ? "left" : "down";

                int zero = GetAbsZeroPoint(absInfo);
                int min = absInfo.minimum;
                int max = absInfo.maximum;

                // Invert Y axes naturally
                bool naturallyInverted = IsAxisY(abs) || IsHatY(abs);

                bool configuredInverted = i == 0
                    ? (options & EvdevStickOption.InvertX) != 0
                    : (options & EvdevStickOption.InvertY) != 0;

                if (naturallyInverted ^ configuredInverted)
                {
                    (min, max) = (max, min);
                }

                builder.AddControl($"{stickRoot}/{axis}")
                    .WithLayout(IntegerAxisControl.LayoutName)
                    .WithParameters($"zeroPoint={zero},minValue={min},maxValue={max}")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0);

                builder.AddControl($"{stickRoot}/{positive}")
                    .WithLayout(IntegerButtonControl.LayoutName)
                    .WithParameters($"minValue={zero},maxValue={max}")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0);

                builder.AddControl($"{stickRoot}/{negative}")
                    .WithLayout(IntegerButtonControl.LayoutName)
                    .WithParameters($"minValue={zero},maxValue={min}")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetAbsByteOffset(0, i)) // no absStartByte or absIndex, this is relative to the parent
                    .WithBitOffset(0);
            }

            // Consume abs bits
            m_AvailableAbs.Remove(capabilities.supportedAbs[absIndex]);
            m_AvailableAbs.Remove(capabilities.supportedAbs[absIndex + 1]);
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

        private bool IsKeyAvailable(input_key_code key)
        {
            if (mappings != null && mappings.m_Keys.ContainsKey(key))
            {
                return false;
            }

            return m_AvailableKeys.Contains(key);
        }

        private bool IsAbsAvailable(input_abs_code abs)
        {
            if (mappings != null &&
                (mappings.m_Abs.ContainsKey(abs) ||
                mappings.m_AbsButtons.ContainsKey(abs) ||
                mappings.m_AbsSticks.ContainsKey(abs)))
            {
                return false;
            }

            return m_AvailableAbs.Contains(abs);
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

        private static bool IsDpadKey(input_key_code key)
        {
            return
                key == input_key_code.BTN_DPAD_UP ||
                key == input_key_code.BTN_DPAD_DOWN ||
                key == input_key_code.BTN_DPAD_LEFT ||
                key == input_key_code.BTN_DPAD_RIGHT;
        }

        private static bool IsAxisX(input_abs_code abs)
        {
            return
                abs == input_abs_code.ABS_X ||
                abs == input_abs_code.ABS_RX ||
                abs == input_abs_code.ABS_TILT_X;
        }

        private static bool IsAxisY(input_abs_code abs)
        {
            return
                abs == input_abs_code.ABS_Y ||
                abs == input_abs_code.ABS_RY ||
                abs == input_abs_code.ABS_TILT_Y;
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
