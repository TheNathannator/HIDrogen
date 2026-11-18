using System;
using System.Text;
using HIDrogen.Imports.Linux;
using HIDrogen.Utilities;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend.Linux
{
    internal struct EvdevLayoutBuilder
    {
        public string name;
        public string displayName;
        public string baseLayout;
        public EvdevDescriptor descriptor;

        public InputControlLayout Build()
        {
            var builder = new InputControlLayout.Builder()
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

            for (int keyIndex = 0; keyIndex < descriptor.supportedKeys.Length; keyIndex++)
            {
                MakeKeyControl(ref builder, ref keyIndex);
            }

            int keyEndBit = NumberUtils.AlignToMultiple(descriptor.supportedKeys.Length, 8);
            // Align for axis values
            int absStartByte = NumberUtils.AlignToMultiple(keyEndBit / 8, sizeof(int));

            for (int absIndex = 0; absIndex < descriptor.supportedAbs.Length; absIndex++)
            {
                MakeAbsControl(ref builder, ref absIndex, absStartByte);
            }

            // TODO: rel controls

            return builder.Build();
        }

        private static readonly (string prefix, string suffix)[] s_KeyNameFixes =
        {
            ("KEY_", "_KEY"),
            ("BTN_", "_BUTTON"),
        };

        private static readonly (string prefix, string suffix)[] s_AbsNameFixes =
        {
            ("ABS_", ""),
        };

        private void MakeKeyControl(ref InputControlLayout.Builder builder, ref int keyIndex)
        {
            if (keyIndex + 3 < descriptor.supportedKeys.Length &&
                IsDpadButton(descriptor.supportedKeys[keyIndex + 0]) &&
                IsDpadButton(descriptor.supportedKeys[keyIndex + 1]) &&
                IsDpadButton(descriptor.supportedKeys[keyIndex + 2]) &&
                IsDpadButton(descriptor.supportedKeys[keyIndex + 3]))
            {
                // Group consecutive d-pad bits into a d-pad control
                MakeDpadControl(ref builder, ref keyIndex);
                return;
            }

            var key = descriptor.supportedKeys[keyIndex];

            builder.AddControl(ToCamelCase(key.ToString()))
                .WithDisplayName(ToDisplayName(key.ToString(), s_KeyNameFixes))
                .WithLayout("Button")
                .WithFormat(InputStateBlock.FormatBit)
                .WithByteOffset(GetKeyByteOffset(keyIndex))
                .WithBitOffset(GetKeyBitOffset(keyIndex));
        }

        private void MakeAbsControl(ref InputControlLayout.Builder builder, ref int absIndex, int absStartByte)
        {
            if (absIndex + 1 < descriptor.supportedAbs.Length &&
                IsHatPair(descriptor.supportedAbs[absIndex + 0], descriptor.supportedAbs[absIndex + 1]))
            {
                // Group consecutive hat abs into a d-pad control
                MakeHatControl(ref builder, ref absIndex, absStartByte);
                return;
            }

            var abs = descriptor.supportedAbs[absIndex];
            var absInfo = descriptor.absInfo[absIndex];

            var control = builder.AddControl(ToCamelCase(abs.ToString()))
                .WithDisplayName(ToDisplayName(abs.ToString(), s_AbsNameFixes))
                .WithLayout("EvdevAxis")
                .WithFormat(InputStateBlock.FormatInt)
                .WithByteOffset(GetAbsByteOffset(absStartByte, absIndex))
                .WithBitOffset(0);

            if (IsAbsY(abs))
            {
                control.WithParameters($"minValue={absInfo.maximum},maxValue={absInfo.minimum},zeroPoint=0");
            }
            else
            {
                control.WithParameters($"minValue={absInfo.minimum},maxValue={absInfo.maximum},zeroPoint=0");
            }
        }

        private void MakeDpadControl(ref InputControlLayout.Builder builder, ref int keyIndex)
        {
            const int keyCount = 4;

            string dpadRoot = "dpad";
            builder.AddControl(dpadRoot)
                .WithDisplayName("D-pad")
                .WithLayout("Dpad")
                .WithFormat(InputStateBlock.FormatBit)
                .WithUsages(CommonUsages.Hatswitch)
                .WithByteOffset(GetKeyByteOffset(keyIndex))
                .WithBitOffset(GetKeyBitOffset(keyIndex))
                .WithSizeInBits(keyCount);

            for (int i = 0; i < keyCount; i++)
            {
                var key = descriptor.supportedKeys[keyIndex + i];

                string name;
                switch (key)
                {
                    case input_key_code.BTN_DPAD_UP: name = $"{dpadRoot}/up"; break;
                    case input_key_code.BTN_DPAD_DOWN: name = $"{dpadRoot}/down"; break;
                    case input_key_code.BTN_DPAD_LEFT: name = $"{dpadRoot}/left"; break;
                    case input_key_code.BTN_DPAD_RIGHT: name = $"{dpadRoot}/right"; break;
                    default: throw new Exception($"{key} is not a d-pad key");
                }

                builder.AddControl(name)
                    .WithLayout("Button")
                    .WithFormat(InputStateBlock.FormatInt)
                    .WithByteOffset(GetKeyByteOffset(i)) // no keyIndex, this is relative to the parent
                    .WithBitOffset(GetKeyBitOffset(i)) // no keyIndex, this is relative to the parent
                    .WithSizeInBits(1);
            }

            keyIndex += keyCount - 1; // - 1 to counteract the increment in the main loop
        }

        private void MakeHatControl(ref InputControlLayout.Builder builder, ref int absIndex, int absStartByte)
        {
            int hatNum = GetHatNum(descriptor.supportedAbs[absIndex]);
            if (hatNum < 0)
            {
                throw new InvalidOperationException("Cannot make a hat control from a non-hat abs.");
            }

            const int absCount = 2;

            string hatRoot = $"hat{hatNum + 1}";
            builder.AddControl(hatRoot)
                .WithDisplayName($"Hat {hatNum + 1}")
                .WithLayout("Dpad")
                .WithByteOffset(GetAbsByteOffset(absStartByte, absIndex))
                .WithSizeInBits(absCount * sizeof(int) * 8)
                .WithUsages(CommonUsages.Hatswitch);

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

        private static readonly StringBuilder s_StringBuilder = new StringBuilder();

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
