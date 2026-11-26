using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.InputDebug
{
    public class InputLog : MonoBehaviour
    {
        [SerializeField]
        private TextLog m_Log;

        public bool showMouse { get; set; }
        public bool showNoisy { get; set; }
        public bool showSynthetic { get; set; }
        public bool showNonLeaf { get; set; }

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

            // Manual enumeration is performed
            EnumerateControls(device);

            void EnumerateControls(InputControl parent)
            {
                foreach (var control in parent.children)
                {
                    if (!showNoisy && control.noisy)
                    {
                        continue;
                    }

                    if (!showSynthetic && control.synthetic)
                    {
                        continue;
                    }

                    if (control.children.Count < 1 || showNonLeaf)
                    {
                        LogControl(control);
                    }

                    EnumerateControls(control);
                }
            }

            void LogControl(InputControl control)
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

            bool IsChanged<TValue>(InputControl<TValue> control, out TValue value)
                where TValue : struct, IEquatable<TValue>
            {
                var previous = control.ReadValue();
                value = control.ReadValueFromEvent(eventPtr);
                return !value.Equals(previous);
            }
        }
    }
}
