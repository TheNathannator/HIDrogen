using System;
using HIDrogen.Controls;
using HIDrogen.Imports.Linux;
using HIDrogen.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.InputSystem.LowLevel;

namespace HIDrogen.Devices.LowLevel
{
    [Flags]
    public enum EvdevHatValue
    {
        None = 0,

        Up = 1 << 0,
        Down = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
    }

    // Intentionally public and not platform guarded, for other packages to reference.
    public class EvdevStateHandler
    {
        // Offset notes:
        // keys: sizeof(byte) / 8, KEY_CNT == 0x300, max state size == 0x60
        // abs: sizeof(int), ABS_CNT == 0x40, max state size == 0x100
        // rel: sizeof(int), REL_CNT == 0x10, max state size == 0x40
        // Total maximum offset: 0x1A0
        private readonly ushort[] m_KeyBitOffsets;
        private readonly ushort[] m_AbsByteOffsets;
        private readonly ushort[] m_RelByteOffsets;

        private readonly int m_KeyEndByte = 0;
        private readonly int m_AbsEndByte = 0;
        private readonly int m_RelEndByte = 0;

        private readonly bool m_HasDpad;

        public int byteLength => m_RelEndByte;

        public EvdevStateHandler(in EvdevCapabilities capabilities)
        {
            // Determine key bit offsets
            {
                var supportedKeys = capabilities.supportedKeys;
                m_KeyBitOffsets = new ushort[(int)input_key_code.KEY_CNT];
                for (int i = 0; i < m_KeyBitOffsets.Length; i++)
                {
                    m_KeyBitOffsets[i] = ushort.MaxValue;
                }

                for (int i = 0; i < supportedKeys.Length; i++)
                {
                    var key = supportedKeys[i];
                    m_KeyBitOffsets[(int)key] = (ushort)i;
                }

                int keyBits = NumberUtils.AlignToMultiple(supportedKeys.Length, 8);
                // Align for axis values
                m_KeyEndByte = NumberUtils.AlignToMultiple(keyBits / 8, sizeof(int));
            }

            // Determine abs byte offsets
            {
                var supportedAbs = capabilities.supportedAbs;
                m_AbsByteOffsets = new ushort[(int)input_abs_code.ABS_CNT];
                for (int i = 0; i < m_AbsByteOffsets.Length; i++)
                {
                    m_AbsByteOffsets[i] = ushort.MaxValue;
                }

                for (int i = 0; i < supportedAbs.Length; i++)
                {
                    int abs = (int)supportedAbs[i];
                    int byteOffset = i * sizeof(int);
                    m_AbsByteOffsets[abs] = (ushort)(m_KeyEndByte + byteOffset);
                }

                m_AbsEndByte = m_KeyEndByte + (supportedAbs.Length * sizeof(int));
            }

            // Determine rel byte offsets
            {
                var supportedRel = capabilities.supportedRel;
                m_RelByteOffsets = new ushort[(int)input_rel_code.REL_CNT];
                for (int i = 0; i < m_RelByteOffsets.Length; i++)
                {
                    m_RelByteOffsets[i] = ushort.MaxValue;
                }

                for (int i = 0; i < supportedRel.Length; i++)
                {
                    int rel = (int)supportedRel[i];
                    int byteOffset = i * sizeof(int);
                    m_RelByteOffsets[rel] = (ushort)(m_AbsEndByte + byteOffset);
                }

                m_RelEndByte = m_AbsEndByte + (supportedRel.Length * sizeof(int));
            }

            m_HasDpad =
                GetKeyOffset(input_key_code.BTN_DPAD_UP).byteOffset < m_KeyEndByte ||
                GetKeyOffset(input_key_code.BTN_DPAD_DOWN).byteOffset < m_KeyEndByte ||
                GetKeyOffset(input_key_code.BTN_DPAD_LEFT).byteOffset < m_KeyEndByte ||
                GetKeyOffset(input_key_code.BTN_DPAD_RIGHT).byteOffset < m_KeyEndByte;
        }

        public (uint byteOffset, uint bit) GetKeyOffset(input_key_code key)
        {
            ushort offset = m_KeyBitOffsets[(int)key];
            return (offset / 8u, offset % 8u);
        }

        public uint GetAbsOffset(input_abs_code abs)
        {
            return m_AbsByteOffsets[(int)abs];
        }

        public uint GetRelOffset(input_rel_code rel)
        {
            return m_RelByteOffsets[(int)rel];
        }

        public unsafe bool ReadKey(StateEvent* stateEvent, input_key_code key)
        {
            if (stateEvent == null)
            {
                throw new ArgumentNullException(nameof(stateEvent));
            }

            var (byteOffset, bit) = GetKeyOffset(key);
            if (byteOffset < stateEvent->stateSizeInBytes)
            {
                byte* statePtr = (byte*)stateEvent->state;
                byte value = statePtr[(int)byteOffset];
                return (value & (1 << (int)bit)) != 0;
            }

            return false;
        }

        public unsafe int ReadAbs(StateEvent* stateEvent, input_abs_code abs)
        {
            if (stateEvent == null)
            {
                throw new ArgumentNullException(nameof(stateEvent));
            }

            uint byteOffset = GetAbsOffset(abs);
            if (byteOffset < stateEvent->stateSizeInBytes)
            {
                byte* statePtr = (byte*)stateEvent->state;
                return *(int*)(statePtr + byteOffset);
            }

            return 0;
        }

        public unsafe float ReadAbsNormalized(
            StateEvent* stateEvent,
            input_abs_code abs,
            in input_absinfo absInfo,
            EvdevAxisOption option
        )
        {
            var (min, max) = (absInfo.minimum, absInfo.maximum);

            bool naturallyInverted = abs.IsAxisY();
            bool inverted = (option & EvdevAxisOption.Inverted) != 0;
            if (naturallyInverted ^ inverted)
            {
                (min, max) = (max, min);
            }

            int zero = 0;
            if ((option & EvdevAxisOption.Unsigned) != 0)
            {
                zero = min;
            }

            int value = ReadAbs(stateEvent, abs);
            return IntegerAxisControl.Normalize(value, min, max, zero);
        }

        public unsafe int ReadRel(StateEvent* stateEvent, input_rel_code rel)
        {
            if (stateEvent == null)
            {
                throw new ArgumentNullException(nameof(stateEvent));
            }

            uint byteOffset = GetRelOffset(rel);
            if (byteOffset < stateEvent->stateSizeInBytes)
            {
                byte* statePtr = (byte*)stateEvent->state;
                return *(int*)(statePtr + byteOffset);
            }

            return 0;
        }

        public unsafe EvdevHatValue ReadHat(StateEvent* stateEvent, int hat)
        {
            if (stateEvent == null)
            {
                throw new ArgumentNullException(nameof(stateEvent));
            }

            if (hat < 0 || hat > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(hat));
            }

            var value = EvdevHatValue.None;

            // Prefer d-pad buttons over analog hat
            if (hat == 0 && m_HasDpad)
            {
                if (ReadKey(stateEvent, input_key_code.BTN_DPAD_UP)) value |= EvdevHatValue.Up;
                if (ReadKey(stateEvent, input_key_code.BTN_DPAD_DOWN)) value |= EvdevHatValue.Down;
                if (ReadKey(stateEvent, input_key_code.BTN_DPAD_LEFT)) value |= EvdevHatValue.Left;
                if (ReadKey(stateEvent, input_key_code.BTN_DPAD_RIGHT)) value |= EvdevHatValue.Right;

                return value;
            }

            int absX = ReadAbs(stateEvent, input_abs_code.ABS_HAT0X + (hat * 2));
            // Y axis is inverted
            int absY = -ReadAbs(stateEvent, input_abs_code.ABS_HAT0Y + (hat * 2));

            if (absY > 0) value |= EvdevHatValue.Up;
            if (absY < 0) value |= EvdevHatValue.Down;
            if (absX < 0) value |= EvdevHatValue.Left;
            if (absX > 0) value |= EvdevHatValue.Right;

            return value;
        }

        public void WriteKey(NativeArray<byte> stateBuffer, input_key_code key, int value)
        {
            // value == 2 is used to indicate key repeasts
            if (value != 0 && value != 1)
            {
                return;
            }

            var (byteOffset, bit) = GetKeyOffset(key);
            if (byteOffset < stateBuffer.Length)
            {
                byte state = stateBuffer[(int)byteOffset];

                if (value == 1)
                {
                    state |= (byte)(1 << (int)bit);
                }
                else
                {
                    state &= (byte)~(1 << (int)bit);
                }

                stateBuffer[(int)byteOffset] = state;
            }
        }

        public void WriteAbs(NativeArray<byte> stateBuffer, input_abs_code abs, int value)
        {
            uint byteOffset = GetAbsOffset(abs);
            if (byteOffset < stateBuffer.Length)
            {
                stateBuffer.ReinterpretStore((int)byteOffset, value);
            }
        }

        public void WriteRel(NativeArray<byte> stateBuffer, input_rel_code rel, int value)
        {
            uint byteOffset = GetRelOffset(rel);
            if (byteOffset < stateBuffer.Length)
            {
                stateBuffer.ReinterpretStore((int)byteOffset, value);
            }
        }

        public void ClearRel(NativeArray<byte> stateBuffer)
        {
            int relsLength = m_RelEndByte - m_AbsEndByte;
            var rels = stateBuffer.GetSubArray(m_AbsEndByte, relsLength);
            unsafe
            {
                UnsafeUtility.MemClear(rels.GetUnsafePtr(), relsLength);
            }
        }
    }
}
