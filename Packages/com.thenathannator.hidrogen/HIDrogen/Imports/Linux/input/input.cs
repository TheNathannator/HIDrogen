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
        // NB: Arbitrary number of multi-touch slots; this may be changed
        public const int MAX_SLOTS = 10;

        public uint code;
        public unsafe fixed int values[MAX_SLOTS];
    }

    internal enum input_id_type
    {
        BUS = 0,
        VENDOR = 1,
        PRODUCT = 2,
        VERSION = 3,
    }

    internal enum input_bus_type
    {
        PCI = 0x01,
        ISAPNP = 0x02,
        USB = 0x03,
        HIL = 0x04,
        BLUETOOTH = 0x05,
        VIRTUAL = 0x06,

        ISA = 0x10,
        I8042 = 0x11,
        XTKBD = 0x12,
        RS232 = 0x13,
        GAMEPORT = 0x14,
        PARPORT = 0x15,
        AMIGA = 0x16,
        ADB = 0x17,
        I2C = 0x18,
        HOST = 0x19,
        GSC = 0x1A,
        ATARI = 0x1B,
        SPI = 0x1C,
        RMI = 0x1D,
        CEC = 0x1E,
        INTEL_ISHTP = 0x1F,
    }

    internal enum mt_tool_type
    {
        MT_TOOL_FINGER = 0x00,
        MT_TOOL_PEN = 0x01,
        MT_TOOL_PALM = 0x02,
        MT_TOOL_DIAL = 0x0a,
        MT_TOOL_MAX = 0x0f,
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
        public const int MAX_EFFECTS = (int)ff_property_type.GAIN;

        public const int MAX = 0x7f;
        public const int COUNT = MAX + 1;

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

    internal enum ff_status
    {
        STOPPED = 0x00,
        PLAYING = 0x01,
        MAX = 0x01,
    }

    internal enum ff_effect_type
    {
        RUMBLE = 0x50,
        PERIODIC = 0x51,
        CONSTANT = 0x52,
        SPRING = 0x53,
        FRICTION = 0x54,
        DAMPER = 0x55,
        INERTIA = 0x56,
        RAMP = 0x57,

        MIN = RUMBLE,
        MAX = RAMP,
    }

    internal enum ff_waveform_type
    {
        SQUARE = 0x58,
        TRIANGLE = 0x59,
        SINE = 0x5a,
        SAW_UP = 0x5b,
        SAW_DOWN = 0x5c,
        CUSTOM = 0x5d,

        MIN = SQUARE,
        MAX = CUSTOM,
    }

    internal enum ff_property_type
    {
        GAIN = 0x60,
        AUTOCENTER = 0x61,
    }

    internal static class Evdev
    {
        public const int VERSION = 0x010001;

        /// <summary>
        /// Get driver version.
        /// </summary>
        public static readonly int EVIOCGVERSION = IOR<int>('E', 0x01);

        /// <summary>
        /// Get device ID.
        /// </summary>
        public static readonly int EVIOCGID = IOR<input_id>('E', 0x02);

        /// <summary>
        /// Get repeat settings.
        /// </summary>
        public static readonly int EVIOCGREP = IOR('E', 0x03, sizeof(uint) * 2);

        /// <summary>
        /// Set repeat settings.
        /// </summary>
        public static readonly int EVIOCSREP = IOW('E', 0x03, sizeof(uint) * 2);

        /// <summary>
        /// Get keycode.
        /// </summary>
        public static readonly int EVIOCGKEYCODE = IOR('E', 0x04, sizeof(uint) * 2);

        /// <summary>
        /// Get keycode (v2).
        /// </summary>
        public static readonly int EVIOCGKEYCODE_V2 = IOR<input_keymap_entry>('E', 0x04);

        /// <summary>
        /// Set keycode.
        /// </summary>
        public static readonly int EVIOCSKEYCODE = IOW('E', 0x04, sizeof(uint) * 2);

        /// <summary>
        /// Set keycode (v2).
        /// </summary>
        public static readonly int EVIOCSKEYCODE_V2 = IOW<input_keymap_entry>('E', 0x04);

        /// <summary>
        /// Get device name.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGNAME(int len) => IOC(IOC_READ, 'E', 0x06, len);

        /// <summary>
        /// Get physical location.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGPHYS(int len) => IOC(IOC_READ, 'E', 0x07, len);

        /// <summary>
        /// Get unique identifier.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGUNIQ(int len) => IOC(IOC_READ, 'E', 0x08, len);

        /// <summary>
        /// Get device properties.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGPROP(int len) => IOC(IOC_READ, 'E', 0x09, len);

        /// <summary>
        /// Get multi-touch slot values.
        /// </summary>
        public static readonly int EVIOCGMTSLOTS = IOC<input_mt_request_layout>(IOC_READ, 'E', 0x0a);

        /// <summary>
        /// Get global key state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGKEY(int len) => IOC(IOC_READ, 'E', 0x18, len);

        /// <summary>
        /// Get all LEDs.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGLED(int len) => IOC(IOC_READ, 'E', 0x19, len);

        /// <summary>
        /// Get sound control statuses.
        /// </summary>
        /// <param name="len"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGSND(int len) => IOC(IOC_READ, 'E', 0x1a, len);

        /// <summary>
        /// Get switch states.
        /// </summary>
        /// <param name="len"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGSW(int len) => IOC(IOC_READ, 'E', 0x1b, len);

        /// <summary>
        /// Get event bits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGBIT(int ev, int len) => IOC(IOC_READ, 'E', 0x20 + ev, len);

        /// <summary>
        /// Get abs value/limits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCGABS(int abs) => IOR<input_absinfo>('E', 0x40 + abs);

        /// <summary>
        /// Set abs value/limits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EVIOCSABS(int abs) => IOW<input_absinfo>('E', 0xc0 + abs);

        /// <summary>
        /// Set force-feedback.
        /// </summary>
        public static readonly int EVIOCSFF = IOW<ff_effect>('E', 0x80);

        /// <summary>
        /// Remove force-feedback.
        /// </summary>
        public static readonly int EVIOCRMFF = IOW<int>('E', 0x81);

        /// <summary>
        /// Get max number of concurrent effects.
        /// </summary>
        public static readonly int EVIOCGEFFECTS = IOR<int>('E', 0x84);

        /// <summary>
        /// Grab or release device.
        /// </summary>
        /// <remarks>
        /// Do not use.
        /// </remarks>
        public static readonly int EVIOCGRAB = IOW<int>('E', 0x90);

        /// <summary>
        /// Revoke device access.
        /// </summary>
        public static readonly int EVIOCREVOKE = IOW<int>('E', 0x91);

        /// <summary>
        /// Get current event mask.
        /// </summary>
        public static readonly int EVIOCGMASK = IOR<input_mask>('E', 0x92);

        /// <summary>
        /// Set current event mask.
        /// </summary>
        public static readonly int EVIOCSMASK = IOW<input_mask>('E', 0x93);

        /// <summary>
        /// Set timestamp clock ID.
        /// </summary>
        public static readonly int EVIOCSCLOCKID = IOW<int>('E', 0xa0);
    }
}
#endif