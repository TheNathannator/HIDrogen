using System.Diagnostics;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.TestProject
{
    internal enum GameInputButton : ushort
    {
        Menu = 1 << 2,
        View = 1 << 3,

        A = 1 << 4,
        B = 1 << 5,
        X = 1 << 6,
        Y = 1 << 7,

        DpadUp = 1 << 8,
        DpadDown = 1 << 9,
        DpadLeft = 1 << 10,
        DpadRight = 1 << 11,

        LeftShoulder = 1 << 8,
        RightShoulder = 1 << 9,
        LeftThumb = 1 << 10,
        RightThumb = 1 << 11,
    }

    internal static class GameInputDefinitions
    {
        public const string InterfaceName = "GameInput";
        public static readonly FourCC InputFormat = new FourCC('G', 'I', 'P');
        public static readonly FourCC OutputFormat = new FourCC('G', 'I', 'P', 'O');
    }

    internal static class GameInputLayoutFinder
    {
        [Conditional("UNITY_STANDALONE_WIN"), Conditional("UNITY_EDITOR_WIN")]
        internal static void RegisterLayout<TDevice>(ushort vendorId, ushort productId)
            where TDevice : InputDevice
        {
            InputSystem.RegisterLayout<TDevice>(matches: GetMatcher(vendorId, productId));
        }

        internal static InputDeviceMatcher GetMatcher(int vendorId, int productId)
        {
            return new InputDeviceMatcher()
                .WithInterface(GameInputDefinitions.InterfaceName)
                .WithCapability("vendorId", vendorId)
                .WithCapability("productId", productId);
        }
    }

    internal static class GameInputExtensions
    {
        internal static void SetBit(ref this GameInputButton value, GameInputButton mask, bool set)
        {
            if (set)
                value |= mask;
            else
                value &= ~mask;
        }
    }
}