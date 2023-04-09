using System.Runtime.CompilerServices;

namespace HIDrogen.Utilities
{
    /// <summary>
    /// Helper methods for numeric values.
    /// Copied from UnityEngine.InputSystem.Utilities.
    /// </summary>
    internal static class NumberHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignToMultipleOf(this int number, int alignment)
        {
            var remainder = number % alignment;
            if (remainder == 0)
                return number;

            return number + alignment - remainder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AlignToMultipleOf(this long number, long alignment)
        {
            var remainder = number % alignment;
            if (remainder == 0)
                return number;

            return number + alignment - remainder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint AlignToMultipleOf(this uint number, uint alignment)
        {
            var remainder = number % alignment;
            if (remainder == 0)
                return number;

            return number + alignment - remainder;
        }
    }
}