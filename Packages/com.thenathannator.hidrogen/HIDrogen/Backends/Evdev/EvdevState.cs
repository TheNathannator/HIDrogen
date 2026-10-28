using System.Runtime.InteropServices;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace HIDrogen.Backend
{
    // Wire contract shared with PlasticBand: Linux key bits followed by raw ABS values.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct EvdevState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('E', 'V', 'D', 'V');
        public fixed byte keys[96];
        public fixed int axes[64];

        public void SetKey(int code, bool pressed)
        {
            if (code < 0 || code >= 768)
                throw new System.ArgumentOutOfRangeException(nameof(code));
            fixed (byte* bits = keys)
            {
                byte mask = (byte)(1 << (code & 7));
                if (pressed)
                    bits[code >> 3] |= mask;
                else
                    bits[code >> 3] &= (byte)~mask;
            }
        }
    }
}
