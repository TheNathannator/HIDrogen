using UnityEngine.InputSystem;

namespace HIDrogen.Backend.Linux
{
    // Not platform guarded
    internal class EvdevDevice : InputDevice
    {
        internal static void Initialize()
        {
            InputSystem.RegisterLayout<EvdevDevice>();
        }
    }
}
