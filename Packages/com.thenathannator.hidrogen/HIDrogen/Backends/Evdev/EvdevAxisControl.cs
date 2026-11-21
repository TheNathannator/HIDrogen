using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend.Linux
{
    // Not platform guarded
    // Freestanding copy of PlasticBand-Unity's IntegerAxisControl, for simplicity
    internal class EvdevAxisControl : AxisControl
    {
        internal static void Initialize()
        {
            InputSystem.RegisterLayout<EvdevAxisControl>("EvdevAxis");
        }

        public int minValue = 0;
        public int maxValue = 0;
        public int zeroPoint = 0;

        public bool hasNullValue;
        public int nullValue;

        /// <summary>
        /// If set, the value is a two's complement number this many bits wide, and is sign-extended before
        /// it's normalized (for drivers that pass raw signed fields through as unsigned values).
        /// </summary>
        public int signBits;

        private float m_LastValue;

        protected override void FinishSetup()
        {
            base.FinishSetup();

            // For whatever reason, 'sbyte' is not checked for in IsIntegerFormat
            if (!stateBlock.format.IsIntegerFormat() && stateBlock.format != InputStateBlock.FormatSByte)
                throw new NotSupportedException($"Non-integer format '{stateBlock.format}' is not supported on '{this}'");

            if (maxValue == 0 && minValue == 0 && zeroPoint == 0)
                throw new NotSupportedException($"Range parameters have not been set on '{this}'! Please set {nameof(minValue)}, {nameof(maxValue)}, and {nameof(zeroPoint)}.");

            int realMin = Math.Min(minValue, maxValue);
            int realMax = Math.Max(minValue, maxValue);

            if (zeroPoint < realMin || zeroPoint > realMax)
                throw new NotSupportedException($"Zero point ({zeroPoint}) is out of range on '{this}'! Must be between min and max values [{realMin} - {realMax}]!");
        }

        /// <inheritdoc/>
        public override unsafe float ReadUnprocessedValueFromState(void* statePtr)
        {
            int value = stateBlock.ReadInt(statePtr);
            if (hasNullValue && value == nullValue)
            {
                return m_LastValue;
            }

            if (signBits > 0 && signBits < 32)
            {
                int shift = 32 - signBits;
                value = (value << shift) >> shift;
            }

            float normalized = Normalize(value, minValue, maxValue, zeroPoint);
            m_LastValue = Preprocess(normalized);
            return m_LastValue;
        }

        public static float Normalize(int value, int minValue, int maxValue, int zeroPoint)
        {
            if (maxValue >= minValue)
            {
                return DoNormalize(value, minValue, maxValue, zeroPoint);
            }
            else
            {
                return -DoNormalize(value, maxValue, minValue, zeroPoint);
            }
        }

        private static float DoNormalize(int value, int minValue, int maxValue, int zeroPoint)
        {
            // Reject invalid bounds
            if (zeroPoint < minValue || zeroPoint > maxValue)
            {
                return 0f;
            }

            // Clamp to min/max
            if (value >= maxValue)
            {
                return maxValue == zeroPoint ? 0f : 1f;
            }
            else if (value <= minValue)
            {
                return minValue == zeroPoint ? 0f : -1f;
            }

            // Split the actual range into positive and negative portions,
            // which are interpolated separately
            if (value >= zeroPoint)
            {
                return Mathf.InverseLerp(zeroPoint, maxValue, value);
            }
            else
            {
                return -1f + Mathf.InverseLerp(minValue, zeroPoint, value);
            }
        }

        public static int Denormalize(float value, int minValue, int maxValue, int zeroPoint)
        {
            if (maxValue >= minValue)
            {
                return DoDenormalize(value, minValue, maxValue, zeroPoint);
            }
            else
            {
                return DoDenormalize(-value, maxValue, minValue, zeroPoint);
            }
        }

        private static int DoDenormalize(float value, int minValue, int maxValue, int zeroPoint)
        {
            // Reject invalid bounds
            if (zeroPoint < minValue || zeroPoint > maxValue || minValue == maxValue)
            {
                return 0;
            }

            // Clamp to min/max
            if (value >= 1f)
            {
                return maxValue;
            }
            else if (value <= -1f)
            {
                return minValue;
            }

            // Split the actual range into positive and negative portions,
            // which are interpolated separately
            if (value >= 0f)
            {
                return IntLerp(zeroPoint, maxValue, value);
            }
            else
            {
                return IntLerp(minValue, zeroPoint, 1 + value);
            }
        }

        private static int IntLerp(int min, int max, float percentage)
        {
            return (int)Math.Round(min + (max - min) * percentage);
        }

        public static int Convert(
            int value,
            int fromMin, int fromMax, int fromZero,
            int toMin, int toMax, int toZero
        )
        {
            float normalized = Normalize(value, fromMin, fromMax, fromZero);
            int converted = Denormalize(normalized, toMin, toMax, toZero);
            return converted;
        }
    }
}
