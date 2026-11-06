using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HIDrogen.InputDebug
{
    public sealed class TextLog : MonoBehaviour
    {
        [SerializeField]
        private Text m_Text;
        [SerializeField]
        private ScrollRect m_Scroll;

        [Space]
        [SerializeField]
        [Min(1)]
        private int m_MaxVisibleLines = 100;
        [SerializeField]
        private bool m_SnapToEndOnNewLine = false;

        private readonly StringBuilder m_TextBuilder = new StringBuilder();

        // Ring buffer of visible lines
        private string[] m_VisibleLines;
        private int m_LineStartIndex = 0;
        private int m_LineCount = 0;

        private bool m_Dirty;

        private void Awake()
        {
            m_Text.text = "";
            m_VisibleLines = new string[m_MaxVisibleLines];
        }

        private void Update()
        {
            if (m_VisibleLines.Length != m_MaxVisibleLines)
            {
                Array.Resize(ref m_VisibleLines, m_MaxVisibleLines);
            }

            if (m_Dirty)
            {
                UpdateText();
                m_Dirty = false;
            }
        }

        public void AppendLine(string line)
        {
            int index = (m_LineStartIndex + m_LineCount) % m_VisibleLines.Length;

            m_VisibleLines[index] = line;

            if (m_LineCount < m_VisibleLines.Length)
            {
                m_LineCount++;
            }
            else
            {
                m_LineStartIndex++;
                if (m_LineStartIndex >= m_VisibleLines.Length)
                {
                    m_LineStartIndex = 0;
                }
            }

            m_Dirty = true;
        }

        private void UpdateText()
        {
            m_TextBuilder.Clear();
            for (int i = 0; i < m_LineCount; i++)
            {
                string line = m_VisibleLines[(m_LineStartIndex + i) % m_VisibleLines.Length];
                if (i < m_LineCount - 1)
                {
                    m_TextBuilder.AppendLine(line);
                }
                else
                {
                    m_TextBuilder.Append(line);
                }
            }
            m_Text.text = m_TextBuilder.ToString();

            if (m_SnapToEndOnNewLine)
            {
                // Scroll value is effectively a normalized Y position, so 1 is top and 0 is bottom
                m_Scroll.verticalScrollbar.value = 0;
            }
        }
    }
}
