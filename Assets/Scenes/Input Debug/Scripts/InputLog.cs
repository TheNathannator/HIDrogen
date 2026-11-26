using System;
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

        public bool showMouse { get; set; }
        public bool showNoisy { get; set; }
        public bool showSynthetic { get; set; }
        public bool showUnchanged { get; set; }

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
            if (showNoisy) enumerateFlags |= EnumerateFlags.IncludeNoisyControls;
            if (showSynthetic) enumerateFlags |= EnumerateFlags.IncludeSyntheticControls;

            bool IsChanged<TValue>(InputControl<TValue> control, out TValue value)
                where TValue : struct, IEquatable<TValue>
            {
                value = control.ReadValueFromEvent(eventPtr);
                if (showUnchanged)
                {
                    return true;
                }

                var previous = control.ReadValue();
                return !value.Equals(previous);
            }

            foreach (var control in eventPtr.EnumerateControls(enumerateFlags, device))
            {
                switch (control)
                {
                    case ButtonControl button:
                    {
                        if (IsChanged(button, out float value))
                        {
                            if (button.IsValueConsideredPressed(value))
                            {
                                m_Log.AppendLine($"{eventPtr.time:F6}: {button.path} {value:F3} (pressed)");
                            }
                            else
                            {
                                m_Log.AppendLine($"{eventPtr.time:F6}: {button.path} {value:F3}");
                            }
                        }
                        break;
                    }
                    case AxisControl axis:
                    {
                        if (IsChanged(axis, out float value))
                        {
                            m_Log.AppendLine($"{eventPtr.time:F6}: {axis.path} {value:F3}");
                        }
                        break;
                    }
                    case IntegerControl integer:
                    {
                        if (IsChanged(integer, out int value))
                        {
                            m_Log.AppendLine($"{eventPtr.time:F6}: {integer.path} {value}");
                        }
                        break;
                    }
                    case Vector2Control vector2:
                    {
                        if (IsChanged(vector2, out var value))
                        {
                            m_Log.AppendLine($"{eventPtr.time:F6}: {vector2.path} {value:F3}");
                        }
                        break;
                    }
                    case Vector3Control vector3:
                    {
                        if (IsChanged(vector3, out var value))
                        {
                            m_Log.AppendLine($"{eventPtr.time:F6}: {vector3.path} {value:F3}");
                        }
                        break;
                    }
                    case QuaternionControl quat:
                    {
                        if (IsChanged(quat, out var value))
                        {
                            m_Log.AppendLine($"{eventPtr.time:F6}: {quat.path} {value:F3}");
                        }
                        break;
                    }
                    default:
                    {
                        m_Log.AppendLine($"{eventPtr.time:F6}: {control.path}");
                        break;
                    }
                }
            }
        }
    }
}
