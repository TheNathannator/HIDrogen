using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.InputDebug
{
    public class EventLog : MonoBehaviour
    {
        [SerializeField]
        private TextLog m_Log;

        public bool showMouse;

        private void OnEnable()
        {
            InputSystem.onEvent += OnEvent;
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= OnEvent;
        }

        private unsafe void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device is Mouse && !showMouse)
            {
                return;
            }

            switch (eventPtr.type)
            {
                case StateEvent.Type:
                {
                    var ev = StateEvent.From(eventPtr);
                    m_Log.AppendLine($"{eventPtr.time:F6}: {device} {eventPtr.type} {ev->stateFormat} [{ev->stateSizeInBytes}]");
                    break;
                }
                case DeltaStateEvent.Type:
                {
                    var ev = DeltaStateEvent.From(eventPtr);
                    m_Log.AppendLine($"{eventPtr.time:F6}: {device} {eventPtr.type} {ev->stateFormat} [{ev->deltaStateSizeInBytes}] {{{ev->stateOffset}-{ev->stateOffset + ev->deltaStateSizeInBytes}}}");
                    break;
                }
                case TextEvent.Type:
                {
                    var ev = TextEvent.From(eventPtr);
                    m_Log.AppendLine($"{eventPtr.time:F6}: {device} {eventPtr.type} '{char.ConvertFromUtf32(ev->character)}' ({ev->character})");
                    break;
                }
                case IMECompositionEvent.Type:
                {
                    var ev = (IMECompositionEvent*)eventPtr.data;
                    m_Log.AppendLine($"{eventPtr.time:F6}: {device} {eventPtr.type} \"{ev->compositionString}\"");
                    break;
                }
                case DeviceResetEvent.Type:
                {
                    var ev = (DeviceResetEvent*)eventPtr.data;
                    if (ev->hardReset)
                    {
                        m_Log.AppendLine($"{eventPtr.time:F6}: {device} {eventPtr.type} HARD");
                    }
                    else
                    {
                        m_Log.AppendLine($"{eventPtr.time:F6}: {device} {eventPtr.type} SOFT");
                    }
                    break;
                }
                default:
                {
                    m_Log.AppendLine($"{eventPtr.time:F6}: {device} {eventPtr.type} ({eventPtr.sizeInBytes})");
                    break;
                }
            }
        }
    }
}
