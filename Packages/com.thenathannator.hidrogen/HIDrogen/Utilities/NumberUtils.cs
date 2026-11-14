namespace HIDrogen.Utilities
{
    internal static class NumberUtils
    {
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