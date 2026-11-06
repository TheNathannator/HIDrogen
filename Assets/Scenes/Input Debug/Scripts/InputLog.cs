using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.InputDebug
{
    using EnumerateFlags = InputControlExtensions.Enumerate;

    public class InputLog : MonoBehaviour
    {
        [SerializeField]
        private TextLog m_Log;

        public bool showMouse;
        public bool showNoise;

        private void OnEnable()
        {
            InputSystem.onEvent += OnEvent;
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= OnEvent;
        }

        private void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            {
                return;
            }

            if (device is Mouse && !showMouse)
            {
                return;
            }

            var enumerateFlags = EnumerateFlags.IgnoreControlsInCurrentState;
            if (showNoise)
            {
                enumerateFlags |= EnumerateFlags.IncludeNoisyControls;
            }

            foreach (var control in eventPtr.EnumerateControls(enumerateFlags, device))
            {
                switch (control)
                {
                    case ButtonControl button:
                    {
                        if (button.isPressed)
                        {
                            m_Log.AppendLine($"{eventPtr.time:F6}: {button} {button.ReadValueFromEvent(eventPtr):F3} (pressed)");
                        }
                        else
                        {
                            m_Log.AppendLine($"{eventPtr.time:F6}: {button} {button.ReadValueFromEvent(eventPtr):F3}");
                        }
                        break;
                    }
                    case AxisControl axis:
                    {
                        m_Log.AppendLine($"{eventPtr.time:F6}: {axis} {axis.ReadValueFromEvent(eventPtr):F3}");
                        break;
                    }
                    case IntegerControl integer:
                    {
                        m_Log.AppendLine($"{eventPtr.time:F6}: {integer} {integer.ReadValueFromEvent(eventPtr)}");
                        break;
                    }
                    case Vector2Control vector2:
                    {
                        m_Log.AppendLine($"{eventPtr.time:F6}: {vector2} {vector2.ReadValueFromEvent(eventPtr):F3}");
                        break;
                    }
                    case Vector3Control vector3:
                    {
                        m_Log.AppendLine($"{eventPtr.time:F6}: {vector3} {vector3.ReadValueFromEvent(eventPtr):F3}");
                        break;
                    }
                    case QuaternionControl quat:
                    {
                        m_Log.AppendLine($"{eventPtr.time:F6}: {quat} {quat.ReadValueFromEvent(eventPtr):F3}");
                        break;
                    }
                    default:
                    {
                        m_Log.AppendLine($"{eventPtr.time:F6}: {control}");
                        break;
                    }
                }
            }
        }
    }
}
