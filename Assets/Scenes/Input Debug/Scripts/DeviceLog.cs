using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.InputDebug
{
    public class DeviceLog : MonoBehaviour
    {
        [SerializeField]
        private TextLog m_Log;

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            m_Log.AppendLine($"{InputState.currentTime:F6}: {device} {change}");
        }
    }
}
