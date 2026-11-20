using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Backend.Linux
{
    // Not platform guarded
    // Freestanding copy of PlasticBand-Unity's IntegerButtonControl, for simplicity
    internal class EvdevButtonControl : ButtonControl
    {
        internal static void Initialize()
        {
            InputSystem.RegisterLayout<EvdevButtonControl>("EvdevButton");
        }

        public int minValue = 0;
        public int maxValue = 0;
        public int intPressPoint = int.MinValue;

        protected override void FinishSetup()
        {
            base.FinishSetup();

            // For whatever reason, 'sbyte' is not checked for in IsIntegerFormat
            if (!stateBlock.format.IsIntegerFormat() && stateBlock.format != InputStateBlock.FormatSByte)
                throw new NotSupportedException($"Non-integer format '{stateBlock.format}' is not supported on '{this}'");

            if (maxValue == 0 && minValue == 0)
                throw new NotSupportedException($"Range parameters have not been set on '{this}'! Please set {nameof(minValue)} and {nameof(maxValue)}.");

            int realMin = Math.Min(minValue, maxValue);
            int realMax = Math.Max(minValue, maxValue);

            if (pressPoint < 0 && intPressPoint > realMin && intPressPoint < realMax)
                pressPoint = EvdevAxisControl.Normalize(intPressPoint, minValue, maxValue, minValue);
        }

        /// <inheritdoc/>
        public override unsafe float ReadUnprocessedValueFromState(void* statePtr)
        {
            int value = stateBlock.ReadInt(statePtr);
            float normalized = EvdevAxisControl.Normalize(value, minValue, maxValue, minValue);
            return Preprocess(normalized);
        }
    }
}