using System;
using System.Diagnostics;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace HIDrogen.TestProject
{
    public class XboxOneVibrationTest : MonoBehaviour
    {
        private readonly Stopwatch m_RumbleCooldown = new Stopwatch();
        private bool m_Rumbling;

        private void Start()
        {
            m_RumbleCooldown.Start();
        }

        private void Update()
        {
            var gamepad = XboxOneGamepad.current;
            if (gamepad == null)
            {
                m_Rumbling = false;
                return;
            }

            byte lx = (byte)(gamepad.leftStick.x.ReadValue() * 255);
            byte ly = (byte)(gamepad.leftStick.y.ReadValue() * 255);
            byte lMax = Math.Max(lx, ly);

            byte rx = (byte)(gamepad.rightStick.x.ReadValue() * 255);
            byte ry = (byte)(gamepad.rightStick.y.ReadValue() * 255);
            byte rMax = Math.Max(rx, ry);

            byte max = Math.Max(lMax, rMax);
            if (max > 10)
            {
                // Don't send rumble commands too quickly, some gamepads struggle otherwise
                if (m_RumbleCooldown.ElapsedMilliseconds > 50)
                {
                    m_Rumbling = true;
                    var vibration = new XboxOneGamepadVibration(lx, ly, rx, ry);
                    gamepad.ExecuteCommand(ref vibration);
                    m_RumbleCooldown.Restart();
                }
            }
            else if (m_Rumbling)
            {
                m_Rumbling = false;
                var vibration = new XboxOneGamepadVibration(0, 0, 0, 0);
                gamepad.ExecuteCommand(ref vibration);
            }
        }
    }
}