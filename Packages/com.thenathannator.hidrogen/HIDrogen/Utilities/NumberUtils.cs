using System.Globalization;

namespace HIDrogen.Utilities
{
    internal static class NumberUtils
    {
        public static int ParseIntegerI32(string str, int defaultValue = 0)
        {
            return TryParseIntegerI32(str, out int value) ? value : defaultValue;
        }

        public static uint ParseIntegerU32(string str, uint defaultValue = 0)
        {
            return TryParseIntegerU32(str, out uint value) ? value : defaultValue;
        }

        public static ulong ParseIntegerU64(string str, ulong defaultValue = 0)
        {
            return TryParseIntegerU64(str, out ulong value) ? value : defaultValue;
        }

        public static bool TryParseIntegerI32(string str, out int value)
        {
            return int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseIntegerU32(string str, out uint value)
        {
            return uint.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseIntegerU64(string str, out ulong value)
        {
            return ulong.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static int ParseHexI32(string str, int defaultValue = 0)
        {
            return TryParseHexI32(str, out int value) ? value : defaultValue;
        }

        public static uint ParseHexU32(string str, uint defaultValue = 0)
        {
            return TryParseHexU32(str, out uint value) ? value : defaultValue;
        }

        public static ulong ParseHexU64(string str, ulong defaultValue = 0)
        {
            return TryParseHexU64(str, out ulong value) ? value : defaultValue;
        }

        public static bool TryParseHexI32(string str, out int value)
        {
            if (str == null)
            {
                value = 0;
                return false;
            }
            // Hex prefix is not accepted by parsing
            if (str.StartsWith("0x"))
            {
                str = str.Substring("0x".Length);
            }

            return int.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseHexU32(string str, out uint value)
        {
            if (str == null)
            {
                value = 0;
                return false;
            }
            // Hex prefix is not accepted by parsing
            if (str.StartsWith("0x"))
            {
                str = str.Substring("0x".Length);
            }

            return uint.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseHexU64(string str, out ulong value)
        {
            if (str == null)
            {
                value = 0;
                return false;
            }
            // Hex prefix is not accepted by parsing
            if (str.StartsWith("0x"))
            {
                str = str.Substring("0x".Length);
            }

            return ulong.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        // Copied from UnityEngine.InputSystem.Utilities
        public static int AlignToMultiple(int number, int alignment)
        {
            var remainder = number % alignment;
            if (remainder == 0)
                return number;

            return number + alignment - remainder;
        }

        // Copied from UnityEngine.InputSystem.Utilities
        public static long AlignToMultiple(long number, long alignment)
        {
            var remainder = number % alignment;
            if (remainder == 0)
                return number;

            return number + alignment - remainder;
        }

        // Copied from UnityEngine.InputSystem.Utilities
        public static uint AlignToMultiple(uint number, uint alignment)
        {
            var remainder = number % alignment;
            if (remainder == 0)
                return number;

            return number + alignment - remainder;
        }
    }
}