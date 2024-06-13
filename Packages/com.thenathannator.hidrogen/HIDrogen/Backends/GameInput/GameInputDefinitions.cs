using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend
{
    internal enum GameInputCommandResult : long
    {
        Success = InputDeviceCommand.GenericSuccess,
        Failure = InputDeviceCommand.GenericFailure,
        ArgumentError = -10,
        Disconnected,
    }

    public static class GameInputDefinitions
    {
        public const string InterfaceName = "GameInput";
        public static readonly FourCC InputFormat = new FourCC('G', 'I', 'P');
        public static readonly FourCC OutputFormat = new FourCC('G', 'I', 'P', 'O');
    }
}