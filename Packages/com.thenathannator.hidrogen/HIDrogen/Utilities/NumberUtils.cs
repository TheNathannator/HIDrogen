using System.Globalization;

namespace HIDrogen.Utilities
{
    internal static class NumberUtils
    {
        public static int ParseHexInt32(string str, int defaultValue = 0)
        {
            if (!TryParseHexInt32(str, out int value))
            {
                value = defaultValue;
            }

            return value;
        }

        public static uint ParseHexUInt32(string str, uint defaultValue = 0)
        {
            if (!TryParseHexUInt32(str, out uint value))
            {
                value = defaultValue;
            }

            return value;
        }

        public static ulong ParseHexUInt64(string str, ulong defaultValue = 0)
        {
            if (!TryParseHexUInt64(str, out ulong value))
            {
                value = defaultValue;
            }

            return value;
        }

        public static bool TryParseHexInt32(string str, out int value)
        {
            return int.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseHexUInt32(string str, out uint value)
        {
            return uint.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseHexUInt64(string str, out ulong value)
        {
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