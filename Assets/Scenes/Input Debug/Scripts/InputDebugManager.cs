using UnityEngine;
using UnityEngine.UI;

namespace HIDrogen.InputDebug
{
    public class InputDebugManager : MonoBehaviour
    {
        [SerializeField]
        private DeviceLog m_DeviceLog;
        [SerializeField]
        private EventLog m_EventLog;
        [SerializeField]
        private InputLog m_InputLog;

        [Space]
        [SerializeField]
        private Toggle m_ShowMouseEvents;
        [SerializeField]
        private Toggle m_ShowNoisyEvents;

        private void OnEnable()
        {
            m_ShowMouseEvents.onValueChanged.AddListener(OnShowMouseEventsChanged);
            m_ShowNoisyEvents.onValueChanged.AddListener(OnShowNoisyEventsChanged);
        }

        private void OnDisable()
        {
            m_ShowMouseEvents.onValueChanged.RemoveListener(OnShowMouseEventsChanged);
        }

        private void OnShowMouseEventsChanged(bool value)
        {
            m_EventLog.showMouse = value;
            m_InputLog.showMouse = value;
        }

        private void OnShowNoisyEventsChanged(bool value)
        {
            m_InputLog.showNoise = value;
        }
    }
}
