#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HIDrogen.Imports.Linux
{
    using static Ioctl;

    [StructLayout(LayoutKind.Sequential)]
    internal struct input_event
    {
#if UNITY_64
        // Inlined version of this for brevity:
        // private timeval time;
        public long sec;
        public long usec;
#else
        private uint __sec;
        private uint __usec;

        public long sec => __sec;
        public long usec => __usec;
#endif

        public ushort type;
        public ushort code;
        public int value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct input_id
    {
        public ushort bustype;
        public ushort vendor;
        public ushort product;
        public ushort version;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct input_absinfo
    {
        public int value;
        public int minimum;
        public int maximum;
        public int fuzz;
        public int flat;
        public int resolution;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct input_keymap_entry
    {
        public enum Flags : byte
        {
            INPUT_KEYMAP_BY_INDEX = 1 << 0,
        }

        public Flags flags;
        public byte len;
        public ushort index;
        public uint keycode;
        public unsafe fixed byte scancode[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct input_mask
    {
        public uint type;
        public uint codes_size;
        public ulong codes_ptr;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct input_mt_request_layout
    {
        // Arbitrary number of multi-touch slots
        public const int MAX_SLOTS = 10;

        public uint code;
        public unsafe fixed int values[MAX_SLOTS];
    }

    internal enum MultiTouchToolType
    {
        Finger = 0x00,
        Pen = 0x01,
        Palm = 0x02,
        Dial = 0x0a,

        Maximum = 0x0f,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ff_replay
    {
        public ushort length;
        public ushort delay;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ff_trigger
    {
        public ushort button;
        public ushort interval;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ff_envelope
    {
        public ushort attack_length;
        public ushort attack_level;
        public ushort fade_length;
        public ushort fade_level;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ff_constant_effect
    {
        public short level;
        public ff_envelope envelope;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ff_ramp_effect
    {
        public short start_level;
        public short end_level;
        public ff_envelope envelope;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ff_condition_effect
    {
        public const int SIZE = sizeof(short) * 6;

        public ushort right_saturation;
        public ushort left_saturation;

        public short right_coeff;
        public short left_coeff;

        public ushort deadband;
        public short center;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ff_periodic_effect
    {
        public ushort waveform;
        public ushort period;
        public short magnitude;
        public short offset;
        public ushort phase;

        public ff_envelope envelope;

        public uint custom_len;
        public unsafe short *custom_data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ff_rumble_effect
    {
        public ushort strong_magnitude;
        public ushort weak_magnitude;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ff_effect
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct ff_effect_union
        {
            [FieldOffset(0)]
            public ff_constant_effect constant;

            [FieldOffset(0)]
            public ff_ramp_effect ramp;

            [FieldOffset(0)]
            public ff_periodic_effect periodic;

            [FieldOffset(0)]
            public ff_condition_effect condition_0;
            [FieldOffset(ff_condition_effect.SIZE)]
            public ff_condition_effect condition_1;

            [FieldOffset(0)]
            public ff_rumble_effect rumble;
        }

        public ushort type;
        public short id;
        public ushort direction;
        public ff_trigger trigger;
        public ff_replay replay;

        public ff_effect_union u;
    }

    internal enum ForceFeedbackStatus
    {
        Stopped = 0x00,
        Playing = 0x01,
        Maximum = Playing,
    }

    internal enum ForceFeedbackEffectType
    {
        Minimum = Rumble,
        Rumble = 0x50,
        Periodic = 0x51,
        Constant = 0x52,
        Spring = 0x53,
        Friction = 0x54,
        Damper = 0x55,
        Inertia = 0x56,
        Ramp = 0x57,
        Maximum = Ramp,
    }

    internal enum ForceFeedbackPeriodicEffect
    {
        Minimum = Square,
        Square = 0x58,
        Triangle = 0x59,
        Sine = 0x5a,
        SawUp = 0x5b,
        SawDown = 0x5c,
        Custom = 0x5d,
        Maximum = Custom,
    }

    internal enum ForceFeedbackProperties
    {
        Gain = 0x60,
        AutoCenter = 0x61,
    }

    internal static class Evdev
    {
        public const int EV_VERSION = 0x010001;

        public const int ID_BUS = 0;
        public const int ID_VENDOR = 1;
        public const int ID_PRODUCT = 2;
        public const int ID_VERSION = 3;

        public const int FF_MAX_EFFECTS = (int)ForceFeedbackProperties.Gain;

        public const int FF_MAX = 0x7f;
        public const int FF_CNT = FF_MAX + 1;

        public static readonly int EVIOCGVERSION = IOR<int>('E', 0x01);
        public static readonly int EVIOCGID = IOR<input_id>('E', 0x02);
        public static readonly int EVIOCGREP = IOR('E', 0x03, sizeof(uint) * 2);
        public static readonly int EVIOCSREP = IOW('E', 0x03, sizeof(uint) * 2);

        public static readonly int EVIOCGKEYCODE = IOR('E', 0x04, sizeof(uint) * 2);
        public static readonly int EVIOCGKEYCODE_V2 = IOR<input_keymap_entry>('E', 0x04);
        public static readonly int EVIOCSKEYCODE = IOW('E', 0x04, sizeof(uint) * 2);
        public static readonly int EVIOCSKEYCODE_V2 = IOW<input_keymap_entry>('E', 0x04);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGNAME(int len) => IOC(IOC_READ, 'E', 0x06, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGPHYS(int len) => IOC(IOC_READ, 'E', 0x07, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGUNIQ(int len) => IOC(IOC_READ, 'E', 0x08, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGPROP(int len) => IOC(IOC_READ, 'E', 0x09, len);

        public static readonly int EVIOCGMTSLOTS = IOC<input_mt_request_layout>(IOC_READ, 'E', 0x0a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGKEY(int len) => IOC(IOC_READ, 'E', 0x18, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGLED(int len) => IOC(IOC_READ, 'E', 0x19, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGSND(int len) => IOC(IOC_READ, 'E', 0x1a, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGSW(int len) => IOC(IOC_READ, 'E', 0x1b, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGBIT(int ev, int len) => IOC(IOC_READ, 'E', 0x20 + ev, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGABS(int abs) => IOR<input_absinfo>('E', 0x40 + abs);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCSABS(int abs) => IOW<input_absinfo>('E', 0xc0 + abs);

        public static readonly int EVIOCSFF = IOW<ff_effect>('E', 0x80);
        public static readonly int EVIOCRMFF = IOW<int>('E', 0x81);
        public static readonly int EVIOCGEFFECTS = IOR<int>('E', 0x84);

        public static readonly int EVIOCGRAB = IOW<int>('E', 0x90);
        public static readonly int EVIOCREVOKE = IOW<int>('E', 0x91);

        public static readonly int EVIOCGMASK = IOR<input_mask>('E', 0x92);
        public static readonly int EVIOCSMASK = IOW<input_mask>('E', 0x93);

        public static readonly int EVIOCSCLOCKID = IOW<int>('E', 0xa0);
    }
}
#endif