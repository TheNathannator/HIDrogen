namespace HIDrogen.Imports.Linux
{
    // Intentionally not platform guarded, for maintenance simplicity.
    // Some types are public for other packages to reference.

    internal enum input_device_prop
    {
        PROP_POINTER = 0x00,
        PROP_DIRECT = 0x01,
        PROP_BUTTONPAD = 0x02,
        PROP_SEMI_MT = 0x03,
        PROP_TOPBUTTONPAD = 0x04,
        PROP_POINTING_STICK = 0x05,
        PROP_ACCELEROMETER = 0x06,
        INPUT_PROP_PRESSUREPAD = 0x07,

        PROP_MAX = 0x1f,
        PROP_COUNT = PROP_MAX + 1,
    }

    internal enum input_event_type : ushort
    {
        EV_SYN = 0x00,
        EV_KEY = 0x01,
        EV_REL = 0x02,
        EV_ABS = 0x03,
        EV_MSC = 0x04,
        EV_SW = 0x05,
        EV_LED = 0x11,
        EV_SND = 0x12,
        EV_REP = 0x14,
        EV_FF = 0x15,
        EV_PWR = 0x16,
        EV_FF_STATUS = 0x17,
        EV_MAX = 0x1f,
        EV_CNT = EV_MAX + 1,
    }

    internal enum input_sync_code
    {
        SYN_REPORT = 0,
        SYN_CONFIG = 1,
        SYN_MT_REPORT = 2,
        SYN_DROPPED = 3,

        SYN_MAX = 0xf,
        SYN_CNT = SYN_MAX + 1,
    }

    // intentionally public
    public enum input_key_code
    {
        KEY_RESERVED = 0,
        KEY_ESC = 1,
        KEY_1 = 2,
        KEY_2 = 3,
        KEY_3 = 4,
        KEY_4 = 5,
        KEY_5 = 6,
        KEY_6 = 7,
        KEY_7 = 8,
        KEY_8 = 9,
        KEY_9 = 10,
        KEY_0 = 11,
        KEY_MINUS = 12,
        KEY_EQUAL = 13,
        KEY_BACKSPACE = 14,
        KEY_TAB = 15,
        KEY_Q = 16,
        KEY_W = 17,
        KEY_E = 18,
        KEY_R = 19,
        KEY_T = 20,
        KEY_Y = 21,
        KEY_U = 22,
        KEY_I = 23,
        KEY_O = 24,
        KEY_P = 25,
        KEY_LEFT_BRACE = 26,
        KEY_RIGHT_BRACE = 27,
        KEY_ENTER = 28,
        KEY_LEFT_CTRL = 29,
        KEY_A = 30,
        KEY_S = 31,
        KEY_D = 32,
        KEY_F = 33,
        KEY_G = 34,
        KEY_H = 35,
        KEY_J = 36,
        KEY_K = 37,
        KEY_L = 38,
        KEY_SEMICOLON = 39,
        KEY_APOSTROPHE = 40,
        KEY_GRAVE = 41,
        KEY_LEFT_SHIFT = 42,
        KEY_BACKSLASH = 43,
        KEY_Z = 44,
        KEY_X = 45,
        KEY_C = 46,
        KEY_V = 47,
        KEY_B = 48,
        KEY_N = 49,
        KEY_M = 50,
        KEY_COMMA = 51,
        KEY_DOT = 52,
        KEY_SLASH = 53,
        KEY_RIGHT_SHIFT = 54,
        KEY_KP_ASTERISK = 55,
        KEY_LEFT_ALT = 56,
        KEY_SPACE = 57,
        KEY_CAPS_LOCK = 58,
        KEY_F1 = 59,
        KEY_F2 = 60,
        KEY_F3 = 61,
        KEY_F4 = 62,
        KEY_F5 = 63,
        KEY_F6 = 64,
        KEY_F7 = 65,
        KEY_F8 = 66,
        KEY_F9 = 67,
        KEY_F10 = 68,
        KEY_NUM_LOCK = 69,
        KEY_SCROLL_LOCK = 70,
        KEY_KP_7 = 71,
        KEY_KP_8 = 72,
        KEY_KP_9 = 73,
        KEY_KP_MINUS = 74,
        KEY_KP_4 = 75,
        KEY_KP_5 = 76,
        KEY_KP_6 = 77,
        KEY_KP_PLUS = 78,
        KEY_KP_1 = 79,
        KEY_KP_2 = 80,
        KEY_KP_3 = 81,
        KEY_KP_0 = 82,
        KEY_KP_DOT = 83,

        KEY_ZENKAKU_HANKAKU = 85,
        KEY_102ND = 86,
        KEY_F11 = 87,
        KEY_F12 = 88,
        KEY_RO = 89,
        KEY_KATAKANA = 90,
        KEY_HIRAGANA = 91,
        KEY_HENKAN = 92,
        KEY_KATAKANA_HIRAGANA = 93,
        KEY_MUHENKAN = 94,
        KEY_KP_JP_COMMA = 95,
        KEY_KP_ENTER = 96,
        KEY_RIGHT_CTRL = 97,
        KEY_KP_SLASH = 98,
        KEY_SYS_RQ = 99,
        KEY_RIGHT_ALT = 100,
        KEY_LINE_FEED = 101,
        KEY_HOME = 102,
        KEY_UP = 103,
        KEY_PAGE_UP = 104,
        KEY_LEFT = 105,
        KEY_RIGHT = 106,
        KEY_END = 107,
        KEY_DOWN = 108,
        KEY_PAGE_DOWN = 109,
        KEY_INSERT = 110,
        KEY_DELETE = 111,
        KEY_MACRO = 112,
        KEY_MUTE = 113,
        KEY_VOLUME_DOWN = 114,
        KEY_VOLUME_UP = 115,
        KEY_POWER = 116,
        KEY_KP_EQUAL = 117,
        KEY_KP_PLUS_MINUS = 118,
        KEY_PAUSE = 119,
        KEY_SCALE = 120,

        KEY_KP_COMMA = 121,
        KEY_HANGEUL = 122,
        KEY_HANJA = 123,
        KEY_YEN = 124,
        KEY_LEFT_META = 125,
        KEY_RIGHT_META = 126,
        KEY_COMPOSE = 127,

        KEY_STOP = 128,
        KEY_AGAIN = 129,
        KEY_PROPS = 130,
        KEY_UNDO = 131,
        KEY_FRONT = 132,
        KEY_COPY = 133,
        KEY_OPEN = 134,
        KEY_PASTE = 135,
        KEY_FIND = 136,
        KEY_CUT = 137,
        KEY_HELP = 138,
        KEY_MENU = 139,
        KEY_CALC = 140,
        KEY_SETUP = 141,
        KEY_SLEEP = 142,
        KEY_WAKE_UP = 143,
        KEY_FILE = 144,
        KEY_SEND_FILE = 145,
        KEY_DELETE_FILE = 146,
        KEY_XFER = 147,
        KEY_PROG_1 = 148,
        KEY_PROG_2 = 149,
        KEY_WWW = 150,
        KEY_MS_DOS = 151,
        KEY_SCREEN_LOCK = 152,
        KEY_ROTATE_DISPLAY = 153,
        KEY_CYCLE_WINDOWS = 154,
        KEY_MAIL = 155,
        KEY_BOOKMARKS = 156,
        KEY_COMPUTER = 157,
        KEY_BACK = 158,
        KEY_FORWARD = 159,
        KEY_CLOSE_CD = 160,
        KEY_EJECT_CD = 161,
        KEY_EJECT_CLOSE_CD = 162,
        KEY_NEXT_SONG = 163,
        KEY_PLAY_PAUSE = 164,
        KEY_PREVIOUS_SONG = 165,
        KEY_STOP_CD = 166,
        KEY_RECORD = 167,
        KEY_REWIND = 168,
        KEY_PHONE = 169,
        KEY_ISO = 170,
        KEY_CONFIG = 171,
        KEY_HOME_PAGE = 172,
        KEY_REFRESH = 173,
        KEY_EXIT = 174,
        KEY_MOVE = 175,
        KEY_EDIT = 176,
        KEY_SCROLL_UP = 177,
        KEY_SCROLL_DOWN = 178,
        KEY_KP_LEFT_PAREN = 179,
        KEY_KP_RIGHT_PAREN = 180,
        KEY_NEW = 181,
        KEY_REDO = 182,

        KEY_F13 = 183,
        KEY_F14 = 184,
        KEY_F15 = 185,
        KEY_F16 = 186,
        KEY_F17 = 187,
        KEY_F18 = 188,
        KEY_F19 = 189,
        KEY_F20 = 190,
        KEY_F21 = 191,
        KEY_F22 = 192,
        KEY_F23 = 193,
        KEY_F24 = 194,

        KEY_PLAY_CD = 200,
        KEY_PAUSE_CD = 201,
        KEY_PROG_3 = 202,
        KEY_PROG_4 = 203,
        KEY_DASHBOARD = 204,
        KEY_SUSPEND = 205,
        KEY_CLOSE = 206,
        KEY_PLAY = 207,
        KEY_FAST_FORWARD = 208,
        KEY_BASS_BOOST = 209,
        KEY_PRINT = 210,
        KEY_HP = 211,
        KEY_CAMERA = 212,
        KEY_SOUND = 213,
        KEY_QUESTION = 214,
        KEY_EMAIL = 215,
        KEY_CHAT = 216,
        KEY_SEARCH = 217,
        KEY_CONNECT = 218,
        KEY_FINANCE = 219,
        KEY_SPORT = 220,
        KEY_SHOP = 221,
        KEY_ALT_ERASE = 222,
        KEY_CANCEL = 223,
        KEY_BRIGHTNESS_DOWN = 224,
        KEY_BRIGHTNESS_UP = 225,
        KEY_MEDIA = 226,

        KEY_SWITCH_VIDEO_MODE = 227,
        KEY_KBD_ILLUM_TOGGLE = 228,
        KEY_KBD_ILLUM_DOWN = 229,
        KEY_KBD_ILLUM_UP = 230,

        KEY_SEND = 231,
        KEY_REPLY = 232,
        KEY_FORWARD_MAIL = 233,
        KEY_SAVE = 234,
        KEY_DOCUMENTS = 235,

        KEY_BATTERY = 236,

        KEY_BLUETOOTH = 237,
        KEY_WLAN = 238,
        KEY_UWB = 239,

        KEY_UNKNOWN = 240,

        KEY_VIDEO_NEXT = 241,
        KEY_VIDEO_PREV = 242,
        KEY_BRIGHTNESS_CYCLE = 243,
        KEY_BRIGHTNESS_AUTO = 244,
        KEY_DISPLAY_OFF = 245,

        KEY_WWAN = 246,
        KEY_RF_KILL = 247,

        KEY_MIC_MUTE = 248,

        BTN_0 = 0x100,
        BTN_1 = 0x101,
        BTN_2 = 0x102,
        BTN_3 = 0x103,
        BTN_4 = 0x104,
        BTN_5 = 0x105,
        BTN_6 = 0x106,
        BTN_7 = 0x107,
        BTN_8 = 0x108,
        BTN_9 = 0x109,

        BTN_LEFT = 0x110,
        BTN_RIGHT = 0x111,
        BTN_MIDDLE = 0x112,
        BTN_SIDE = 0x113,
        BTN_EXTRA = 0x114,
        BTN_FORWARD = 0x115,
        BTN_BACK = 0x116,
        BTN_TASK = 0x117,

        BTN_TRIGGER = 0x120,
        BTN_THUMB = 0x121,
        BTN_THUMB_2 = 0x122,
        BTN_TOP = 0x123,
        BTN_TOP_2 = 0x124,
        BTN_PINKIE = 0x125,
        BTN_BASE = 0x126,
        BTN_BASE_2 = 0x127,
        BTN_BASE_3 = 0x128,
        BTN_BASE_4 = 0x129,
        BTN_BASE_5 = 0x12a,
        BTN_BASE_6 = 0x12b,
        BTN_DEAD = 0x12f,

        BTN_SOUTH = 0x130,
        BTN_EAST = 0x131,
        BTN_C = 0x132,
        BTN_NORTH = 0x133,
        BTN_WEST = 0x134,
        BTN_Z = 0x135,
        BTN_TL = 0x136,
        BTN_TR = 0x137,
        BTN_TL_2 = 0x138,
        BTN_TR_2 = 0x139,
        BTN_SELECT = 0x13a,
        BTN_START = 0x13b,
        BTN_MODE = 0x13c,
        BTN_THUMB_L = 0x13d,
        BTN_THUMB_R = 0x13e,

        BTN_TOOL_PEN = 0x140,
        BTN_TOOL_RUBBER = 0x141,
        BTN_TOOL_BRUSH = 0x142,
        BTN_TOOL_PENCIL = 0x143,
        BTN_TOOL_AIRBRUSH = 0x144,
        BTN_TOOL_FINGER = 0x145,
        BTN_TOOL_MOUSE = 0x146,
        BTN_TOOL_LENS = 0x147,
        BTN_TOOL_QUINT_TAP = 0x148,
        BTN_STYLUS_3 = 0x149,
        BTN_TOUCH = 0x14a,
        BTN_STYLUS = 0x14b,
        BTN_STYLUS_2 = 0x14c,
        BTN_TOOL_DOUBLE_TAP = 0x14d,
        BTN_TOOL_TRIPLE_TAP = 0x14e,
        BTN_TOOL_QUAD_TAP = 0x14f,

        BTN_GEAR_DOWN = 0x150,
        BTN_GEAR_UP = 0x151,

        KEY_OK = 0x160,
        KEY_SELECT = 0x161,
        KEY_GOTO = 0x162,
        KEY_CLEAR = 0x163,
        KEY_POWER_2 = 0x164,
        KEY_OPTION = 0x165,
        KEY_INFO = 0x166,
        KEY_TIME = 0x167,
        KEY_VENDOR = 0x168,
        KEY_ARCHIVE = 0x169,
        KEY_PROGRAM = 0x16a,
        KEY_CHANNEL = 0x16b,
        KEY_FAVORITES = 0x16c,
        KEY_EPG = 0x16d,
        KEY_PVR = 0x16e,
        KEY_MHP = 0x16f,
        KEY_LANGUAGE = 0x170,
        KEY_TITLE = 0x171,
        KEY_SUBTITLE = 0x172,
        KEY_ANGLE = 0x173,
        KEY_ZOOM = 0x174,
        KEY_MODE = 0x175,
        KEY_KEYBOARD = 0x176,
        KEY_ASPECT_RATIO = 0x177,
        KEY_PC = 0x178,
        KEY_TV = 0x179,
        KEY_TV_2 = 0x17a,
        KEY_VCR = 0x17b,
        KEY_VCR_2 = 0x17c,
        KEY_SAT = 0x17d,
        KEY_SAT_2 = 0x17e,
        KEY_CD = 0x17f,
        KEY_TAPE = 0x180,
        KEY_RADIO = 0x181,
        KEY_TUNER = 0x182,
        KEY_PLAYER = 0x183,
        KEY_TEXT = 0x184,
        KEY_DVD = 0x185,
        KEY_AUX = 0x186,
        KEY_MP3 = 0x187,
        KEY_AUDIO = 0x188,
        KEY_VIDEO = 0x189,
        KEY_DIRECTORY = 0x18a,
        KEY_LIST = 0x18b,
        KEY_MEMO = 0x18c,
        KEY_CALENDAR = 0x18d,
        KEY_RED = 0x18e,
        KEY_GREEN = 0x18f,
        KEY_YELLOW = 0x190,
        KEY_BLUE = 0x191,
        KEY_CHANNEL_UP = 0x192,
        KEY_CHANNEL_DOWN = 0x193,
        KEY_FIRST = 0x194,
        KEY_LAST = 0x195,
        KEY_AB = 0x196,
        KEY_NEXT = 0x197,
        KEY_RESTART = 0x198,
        KEY_SLOW = 0x199,
        KEY_SHUFFLE = 0x19a,
        KEY_BREAK = 0x19b,
        KEY_PREVIOUS = 0x19c,
        KEY_DIGITS = 0x19d,
        KEY_TEEN = 0x19e,
        KEY_TWEN = 0x19f,
        KEY_VIDEO_PHONE = 0x1a0,
        KEY_GAMES = 0x1a1,
        KEY_ZOOM_IN = 0x1a2,
        KEY_ZOOM_OUT = 0x1a3,
        KEY_ZOOM_RESET = 0x1a4,
        KEY_WORD_PROCESSOR = 0x1a5,
        KEY_EDITOR = 0x1a6,
        KEY_SPREADSHEET = 0x1a7,
        KEY_GRAPHICS_EDITOR = 0x1a8,
        KEY_PRESENTATION = 0x1a9,
        KEY_DATABASE = 0x1aa,
        KEY_NEWS = 0x1ab,
        KEY_VOICE_MAIL = 0x1ac,
        KEY_ADDRESS_BOOK = 0x1ad,
        KEY_MESSENGER = 0x1ae,
        KEY_DISPLAY_TOGGLE = 0x1af,
        KEY_SPELL_CHECK = 0x1b0,
        KEY_LOGOFF = 0x1b1,

        KEY_DOLLAR = 0x1b2,
        KEY_EURO = 0x1b3,

        KEY_FRAME_BACK = 0x1b4,
        KEY_FRAME_FORWARD = 0x1b5,
        KEY_CONTEXT_MENU = 0x1b6,
        KEY_MEDIA_REPEAT = 0x1b7,
        KEY_10_CHANNELS_UP = 0x1b8,
        KEY_10_CHANNELS_DOWN = 0x1b9,
        KEY_IMAGES = 0x1ba,
        KEY_NOTIFICATION_CENTER = 0x1bc,
        KEY_PICKUP_PHONE = 0x1bd,
        KEY_HANGUP_PHONE = 0x1be,
        KEY_LINK_PHONE = 0x1bf,

        KEY_DEL_EOL = 0x1c0,
        KEY_DEL_EOS = 0x1c1,
        KEY_INS_LINE = 0x1c2,
        KEY_DEL_LINE = 0x1c3,

        KEY_FN = 0x1d0,
        KEY_FN_ESC = 0x1d1,
        KEY_FN_F1 = 0x1d2,
        KEY_FN_F2 = 0x1d3,
        KEY_FN_F3 = 0x1d4,
        KEY_FN_F4 = 0x1d5,
        KEY_FN_F5 = 0x1d6,
        KEY_FN_F6 = 0x1d7,
        KEY_FN_F7 = 0x1d8,
        KEY_FN_F8 = 0x1d9,
        KEY_FN_F9 = 0x1da,
        KEY_FN_F10 = 0x1db,
        KEY_FN_F11 = 0x1dc,
        KEY_FN_F12 = 0x1dd,
        KEY_FN_1 = 0x1de,
        KEY_FN_2 = 0x1df,
        KEY_FN_D = 0x1e0,
        KEY_FN_E = 0x1e1,
        KEY_FN_F = 0x1e2,
        KEY_FN_S = 0x1e3,
        KEY_FN_B = 0x1e4,
        KEY_FN_RIGHT_SHIFT = 0x1e5,

        KEY_BRL_DOT_1 = 0x1f1,
        KEY_BRL_DOT_2 = 0x1f2,
        KEY_BRL_DOT_3 = 0x1f3,
        KEY_BRL_DOT_4 = 0x1f4,
        KEY_BRL_DOT_5 = 0x1f5,
        KEY_BRL_DOT_6 = 0x1f6,
        KEY_BRL_DOT_7 = 0x1f7,
        KEY_BRL_DOT_8 = 0x1f8,
        KEY_BRL_DOT_9 = 0x1f9,
        KEY_BRL_DOT_10 = 0x1fa,

        KEY_NUMERIC_0 = 0x200,
        KEY_NUMERIC_1 = 0x201,
        KEY_NUMERIC_2 = 0x202,
        KEY_NUMERIC_3 = 0x203,
        KEY_NUMERIC_4 = 0x204,
        KEY_NUMERIC_5 = 0x205,
        KEY_NUMERIC_6 = 0x206,
        KEY_NUMERIC_7 = 0x207,
        KEY_NUMERIC_8 = 0x208,
        KEY_NUMERIC_9 = 0x209,
        KEY_NUMERIC_STAR = 0x20a,
        KEY_NUMERIC_POUND = 0x20b,
        KEY_NUMERIC_A = 0x20c,
        KEY_NUMERIC_B = 0x20d,
        KEY_NUMERIC_C = 0x20e,
        KEY_NUMERIC_D = 0x20f,

        KEY_CAMERA_FOCUS = 0x210,
        KEY_WPS_BUTTON = 0x211,

        KEY_TOUCHPAD_TOGGLE = 0x212,
        KEY_TOUCHPAD_ON = 0x213,
        KEY_TOUCHPAD_OFF = 0x214,

        KEY_CAMERA_ZOOMIN = 0x215,
        KEY_CAMERA_ZOOMOUT = 0x216,
        KEY_CAMERA_UP = 0x217,
        KEY_CAMERA_DOWN = 0x218,
        KEY_CAMERA_LEFT = 0x219,
        KEY_CAMERA_RIGHT = 0x21a,

        KEY_ATTENDANT_ON = 0x21b,
        KEY_ATTENDANT_OFF = 0x21c,
        KEY_ATTENDANT_TOGGLE = 0x21d,
        KEY_LIGHTS_TOGGLE = 0x21e,

        BTN_DPAD_UP = 0x220,
        BTN_DPAD_DOWN = 0x221,
        BTN_DPAD_LEFT = 0x222,
        BTN_DPAD_RIGHT = 0x223,

        BTN_GRIP_L = 0x224,
        BTN_GRIP_R = 0x225,
        BTN_GRIP_L_2 = 0x226,
        BTN_GRIP_R_2 = 0x227,

        KEY_ALS_TOGGLE = 0x230,
        KEY_ROTATE_LOCK_TOGGLE = 0x231,
        KEY_REFRESH_RATE_TOGGLE = 0x232,

        KEY_BUTTON_CONFIG = 0x240,
        KEY_TASK_MANAGER = 0x241,
        KEY_JOURNAL = 0x242,
        KEY_CONTROL_PANEL = 0x243,
        KEY_APP_SELECT = 0x244,
        KEY_SCREEN_SAVER = 0x245,
        KEY_VOICE_COMMAND = 0x246,
        KEY_ASSISTANT = 0x247,
        KEY_KBD_LAYOUT_NEXT = 0x248,
        KEY_EMOJI_PICKER = 0x249,
        KEY_DICTATE = 0x24a,
        KEY_CAMERA_ACCESS_ENABLE = 0x24b,
        KEY_CAMERA_ACCESS_DISABLE = 0x24c,
        KEY_CAMERA_ACCESS_TOGGLE = 0x24d,
        KEY_ACCESSIBILITY = 0x24e,
        KEY_DO_NOT_DISTURB = 0x24f,

        KEY_BRIGHTNESS_MIN = 0x250,
        KEY_BRIGHTNESS_MAX = 0x251,

        KEY_EPRIVACY_SCREEN_ON = 0x252,
        KEY_EPRIVACY_SCREEN_OFF = 0x253,

        KEY_ACTION_ON_SELECTION = 0x254,
        KEY_CONTEXTUAL_INSERT = 0x255,
        KEY_CONTEXTUAL_QUERY = 0x256,

        KEY_KBD_INPUT_ASSIST_PREV = 0x260,
        KEY_KBD_INPUT_ASSIST_NEXT = 0x261,
        KEY_KBD_INPUT_ASSIST_PREVGROUP = 0x262,
        KEY_KBD_INPUT_ASSIST_NEXTGROUP = 0x263,
        KEY_KBD_INPUT_ASSIST_ACCEPT = 0x264,
        KEY_KBD_INPUT_ASSIST_CANCEL = 0x265,

        KEY_RIGHT_UP = 0x266,
        KEY_RIGHT_DOWN = 0x267,
        KEY_LEFT_UP = 0x268,
        KEY_LEFT_DOWN = 0x269,

        KEY_ROOT_MENU = 0x26a,
        KEY_MEDIA_TOP_MENU = 0x26b,
        KEY_NUMERIC_11 = 0x26c,
        KEY_NUMERIC_12 = 0x26d,
        KEY_AUDIO_DESC = 0x26e,
        KEY_3D_MODE = 0x26f,
        KEY_NEXT_FAVORITE = 0x270,
        KEY_STOP_RECORD = 0x271,
        KEY_PAUSE_RECORD = 0x272,
        KEY_VOD = 0x273,
        KEY_UNMUTE = 0x274,
        KEY_FAST_REVERSE = 0x275,
        KEY_SLOW_REVERSE = 0x276,
        KEY_DATA = 0x277,
        KEY_ONSCREEN_KEYBOARD = 0x278,
        KEY_PRIVACY_SCREEN_TOGGLE = 0x279,

        KEY_SELECTIVE_SCREENSHOT = 0x27a,

        KEY_NEXT_ELEMENT = 0x27b,
        KEY_PREVIOUS_ELEMENT = 0x27c,

        KEY_AUTOPILOT_ENGAGE_TOGGLE = 0x27d,

        KEY_MARK_WAYPOINT = 0x27e,
        KEY_SOS = 0x27f,
        KEY_NAV_CHART = 0x280,
        KEY_FISHING_CHART = 0x281,
        KEY_SINGLE_RANGE_RADAR = 0x282,
        KEY_DUAL_RANGE_RADAR = 0x283,
        KEY_RADAR_OVERLAY = 0x284,
        KEY_TRADITIONAL_SONAR = 0x285,
        KEY_CLEARVU_SONAR = 0x286,
        KEY_SIDEVU_SONAR = 0x287,
        KEY_NAV_INFO = 0x288,
        KEY_BRIGHTNESS_MENU = 0x289,

        KEY_MACRO_1 = 0x290,
        KEY_MACRO_2 = 0x291,
        KEY_MACRO_3 = 0x292,
        KEY_MACRO_4 = 0x293,
        KEY_MACRO_5 = 0x294,
        KEY_MACRO_6 = 0x295,
        KEY_MACRO_7 = 0x296,
        KEY_MACRO_8 = 0x297,
        KEY_MACRO_9 = 0x298,
        KEY_MACRO_10 = 0x299,
        KEY_MACRO_11 = 0x29a,
        KEY_MACRO_12 = 0x29b,
        KEY_MACRO_13 = 0x29c,
        KEY_MACRO_14 = 0x29d,
        KEY_MACRO_15 = 0x29e,
        KEY_MACRO_16 = 0x29f,
        KEY_MACRO_17 = 0x2a0,
        KEY_MACRO_18 = 0x2a1,
        KEY_MACRO_19 = 0x2a2,
        KEY_MACRO_20 = 0x2a3,
        KEY_MACRO_21 = 0x2a4,
        KEY_MACRO_22 = 0x2a5,
        KEY_MACRO_23 = 0x2a6,
        KEY_MACRO_24 = 0x2a7,
        KEY_MACRO_25 = 0x2a8,
        KEY_MACRO_26 = 0x2a9,
        KEY_MACRO_27 = 0x2aa,
        KEY_MACRO_28 = 0x2ab,
        KEY_MACRO_29 = 0x2ac,
        KEY_MACRO_30 = 0x2ad,

        KEY_MACRO_RECORD_START = 0x2b0,
        KEY_MACRO_RECORD_STOP = 0x2b1,
        KEY_MACRO_PRESET_CYCLE = 0x2b2,
        KEY_MACRO_PRESET_1 = 0x2b3,
        KEY_MACRO_PRESET_2 = 0x2b4,
        KEY_MACRO_PRESET_3 = 0x2b5,

        KEY_KBD_LCD_MENU_1 = 0x2b8,
        KEY_KBD_LCD_MENU_2 = 0x2b9,
        KEY_KBD_LCD_MENU_3 = 0x2ba,
        KEY_KBD_LCD_MENU_4 = 0x2bb,
        KEY_KBD_LCD_MENU_5 = 0x2bc,

        KEY_PERFORMANCE = 0x2bd,

        BTN_TRIGGER_HAPPY_1 = 0x2c0,
        BTN_TRIGGER_HAPPY_2 = 0x2c1,
        BTN_TRIGGER_HAPPY_3 = 0x2c2,
        BTN_TRIGGER_HAPPY_4 = 0x2c3,
        BTN_TRIGGER_HAPPY_5 = 0x2c4,
        BTN_TRIGGER_HAPPY_6 = 0x2c5,
        BTN_TRIGGER_HAPPY_7 = 0x2c6,
        BTN_TRIGGER_HAPPY_8 = 0x2c7,
        BTN_TRIGGER_HAPPY_9 = 0x2c8,
        BTN_TRIGGER_HAPPY_10 = 0x2c9,
        BTN_TRIGGER_HAPPY_11 = 0x2ca,
        BTN_TRIGGER_HAPPY_12 = 0x2cb,
        BTN_TRIGGER_HAPPY_13 = 0x2cc,
        BTN_TRIGGER_HAPPY_14 = 0x2cd,
        BTN_TRIGGER_HAPPY_15 = 0x2ce,
        BTN_TRIGGER_HAPPY_16 = 0x2cf,
        BTN_TRIGGER_HAPPY_17 = 0x2d0,
        BTN_TRIGGER_HAPPY_18 = 0x2d1,
        BTN_TRIGGER_HAPPY_19 = 0x2d2,
        BTN_TRIGGER_HAPPY_20 = 0x2d3,
        BTN_TRIGGER_HAPPY_21 = 0x2d4,
        BTN_TRIGGER_HAPPY_22 = 0x2d5,
        BTN_TRIGGER_HAPPY_23 = 0x2d6,
        BTN_TRIGGER_HAPPY_24 = 0x2d7,
        BTN_TRIGGER_HAPPY_25 = 0x2d8,
        BTN_TRIGGER_HAPPY_26 = 0x2d9,
        BTN_TRIGGER_HAPPY_27 = 0x2da,
        BTN_TRIGGER_HAPPY_28 = 0x2db,
        BTN_TRIGGER_HAPPY_29 = 0x2dc,
        BTN_TRIGGER_HAPPY_30 = 0x2dd,
        BTN_TRIGGER_HAPPY_31 = 0x2de,
        BTN_TRIGGER_HAPPY_32 = 0x2df,
        BTN_TRIGGER_HAPPY_33 = 0x2e0,
        BTN_TRIGGER_HAPPY_34 = 0x2e1,
        BTN_TRIGGER_HAPPY_35 = 0x2e2,
        BTN_TRIGGER_HAPPY_36 = 0x2e3,
        BTN_TRIGGER_HAPPY_37 = 0x2e4,
        BTN_TRIGGER_HAPPY_38 = 0x2e5,
        BTN_TRIGGER_HAPPY_39 = 0x2e6,
        BTN_TRIGGER_HAPPY_40 = 0x2e7,

        KEY_MAX = 0x2ff,
        KEY_CNT = KEY_MAX + 1,
    }

    // intentionally public
    /// <summary>
    /// Aliases and alternative forms for key codes.
    /// </summary>
    /// <remarks>
    /// These are kept separate from <see cref="input_key_code"/> to ensure that every key value
    /// has only one standard name. This property is relied on for stable control paths and
    /// display names in the evdev layout builder.
    /// </remarks>
    public static class input_key_code_alt
    {
        public const input_key_code KEY_HANGUEL = input_key_code.KEY_HANGEUL;
        public const input_key_code KEY_COFFEE = input_key_code.KEY_SCREEN_LOCK;
        public const input_key_code KEY_DIRECTION = input_key_code.KEY_ROTATE_DISPLAY;
        public const input_key_code KEY_ALL_APPLICATIONS = input_key_code.KEY_DASHBOARD;
        public const input_key_code KEY_BRIGHTNESS_ZERO = input_key_code.KEY_BRIGHTNESS_AUTO;
        public const input_key_code KEY_WIMAX = input_key_code.KEY_WWAN;
        public const input_key_code KEY_FULL_SCREEN = input_key_code.KEY_ZOOM;
        public const input_key_code KEY_SCREEN = input_key_code.KEY_ASPECT_RATIO;
        public const input_key_code KEY_BRIGHTNESS_TOGGLE = input_key_code.KEY_DISPLAY_TOGGLE;

        public const input_key_code BTN_MISC = input_key_code.BTN_0; // 0x100
        public const input_key_code BTN_MOUSE = input_key_code.BTN_LEFT; // 0x110
        public const input_key_code BTN_JOYSTICK = input_key_code.BTN_TRIGGER; // 0x120
        public const input_key_code BTN_GAMEPAD = input_key_code.BTN_SOUTH; // 0x130
        public const input_key_code BTN_DIGI = input_key_code.BTN_TOOL_PEN; // 0x140
        public const input_key_code BTN_WHEEL = input_key_code.BTN_GEAR_DOWN; // 0x150
        public const input_key_code BTN_TRIGGER_HAPPY = input_key_code.BTN_TRIGGER_HAPPY_1; // 0x2c0

        /// <summary>
        /// Alias for <see cref="input_key_code.BTN_SOUTH"/> that refers to a button labelled 'A'.
        /// </summary>
        /// <remarks>
        /// The only notable gamepad drivers to explicitly use this alias are xpad and hid-steam.
        /// Gamepad mappings for all other drivers should probably refer to the standard enum instead.
        /// </remarks>
        public const input_key_code BTN_A = input_key_code.BTN_SOUTH;

        /// <summary>
        /// Alias for <see cref="input_key_code.BTN_EAST"/> that refers to a button labelled 'B'.
        /// </summary>
        /// <inheritdoc cref="BTN_A"/>
        public const input_key_code BTN_B = input_key_code.BTN_EAST;

        // yes, X is defined as north and Y is defined as west. why...?
        /// <summary>
        /// Alias for <see cref="input_key_code.BTN_NORTH"/> (sic) that refers to a button labelled 'X'.
        /// </summary>
        /// <inheritdoc cref="BTN_A"/>
        public const input_key_code BTN_X = input_key_code.BTN_NORTH;

        /// <summary>
        /// Alias for <see cref="input_key_code.BTN_WEST"/> (sic) that refers to a button labelled 'Y'.
        /// </summary>
        /// <inheritdoc cref="BTN_A"/>
        public const input_key_code BTN_Y = input_key_code.BTN_WEST;

        public const input_key_code KEY_MIN_INTERESTING = input_key_code.KEY_MUTE;
    }

    // intentionally public
    public enum input_rel_code
    {
        REL_X = 0x00,
        REL_Y = 0x01,
        REL_Z = 0x02,
        REL_RX = 0x03,
        REL_RY = 0x04,
        REL_RZ = 0x05,
        REL_HWHEEL = 0x06,
        REL_DIAL = 0x07,
        REL_WHEEL = 0x08,
        REL_MISC = 0x09,
        REL_RESERVED = 0x0a,
        REL_WHEEL_HI_RES = 0x0b,
        REL_HWHEEL_HI_RES = 0x0c,

        REL_MAX = 0x0f,
        REL_CNT = REL_MAX + 1,
    }

    // intentionally public
    public enum input_abs_code
    {
        ABS_X = 0x00,
        ABS_Y = 0x01,
        ABS_Z = 0x02,
        ABS_RX = 0x03,
        ABS_RY = 0x04,
        ABS_RZ = 0x05,
        ABS_THROTTLE = 0x06,
        ABS_RUDDER = 0x07,
        ABS_WHEEL = 0x08,
        ABS_GAS = 0x09,
        ABS_BRAKE = 0x0a,
        ABS_HAT0X = 0x10,
        ABS_HAT0Y = 0x11,
        ABS_HAT1X = 0x12,
        ABS_HAT1Y = 0x13,
        ABS_HAT2X = 0x14,
        ABS_HAT2Y = 0x15,
        ABS_HAT3X = 0x16,
        ABS_HAT3Y = 0x17,
        ABS_PRESSURE = 0x18,
        ABS_DISTANCE = 0x19,
        ABS_TILT_X = 0x1a,
        ABS_TILT_Y = 0x1b,
        ABS_TOOL_WIDTH = 0x1c,

        ABS_VOLUME = 0x20,
        ABS_PROFILE = 0x21,
        ABS_SND_PROFILE = 0x22,

        ABS_MISC_1 = 0x28,
        ABS_MISC_2 = 0x29,
        ABS_MISC_3 = 0x2a,
        ABS_MISC_4 = 0x2b,
        ABS_MISC_5 = 0x2c,
        ABS_MISC_6 = 0x2d,

        ABS_RESERVED = 0x2e,

        ABS_MT_SLOT = 0x2f,
        ABS_MT_TOUCH_MAJOR = 0x30,
        ABS_MT_TOUCH_MINOR = 0x31,
        ABS_MT_WIDTH_MAJOR = 0x32,
        ABS_MT_WIDTH_MINOR = 0x33,
        ABS_MT_ORIENTATION = 0x34,
        ABS_MT_POSITION_X = 0x35,
        ABS_MT_POSITION_Y = 0x36,
        ABS_MT_TOOL_TYPE = 0x37,
        ABS_MT_BLOB_ID = 0x38,
        ABS_MT_TRACKING_ID = 0x39,
        ABS_MT_PRESSURE = 0x3a,
        ABS_MT_DISTANCE = 0x3b,
        ABS_MT_TOOL_X = 0x3c,
        ABS_MT_TOOL_Y = 0x3d,

        ABS_MAX = 0x3f,
        ABS_CNT = ABS_MAX + 1,
    }

    internal enum input_switch_code
    {
        SW_LID = 0x00,
        SW_TABLET_MODE = 0x01,
        SW_HEADPHONE_INSERT = 0x02,
        SW_RFKILL_ALL = 0x03,
        SW_RADIO = SW_RFKILL_ALL,
        SW_MICROPHONE_INSERT = 0x04,
        SW_DOCK = 0x05,
        SW_LINEOUT_INSERT = 0x06,
        SW_JACK_PHYSICAL_INSERT = 0x07,
        SW_VIDEOOUT_INSERT = 0x08,
        SW_CAMERA_LENS_COVER = 0x09,
        SW_KEYPAD_SLIDE = 0x0a,
        SW_FRONT_PROXIMITY = 0x0b,
        SW_ROTATE_LOCK = 0x0c,
        SW_LINEIN_INSERT = 0x0d,
        SW_MUTE_DEVICE = 0x0e,
        SW_PEN_INSERTED = 0x0f,
        SW_MACHINE_COVER = 0x10,
        SW_USB_INSERT = 0x11,

        SW_MAX = 0x11,
        SW_CNT = SW_MAX + 1,
    }

    internal enum input_misc_code
    {
        MSC_SERIAL = 0x00,
        MSC_PULSELED = 0x01,
        MSC_GESTURE = 0x02,
        MSC_RAW = 0x03,
        MSC_SCAN = 0x04,
        MSC_TIMESTAMP = 0x05,

        MSC_MAX = 0x07,
        MSC_CNT = MSC_MAX + 1,
    }

    internal enum input_led_code
    {
        LED_NUML = 0x00,
        LED_CAPSL = 0x01,
        LED_SCROLLL = 0x02,
        LED_COMPOSE = 0x03,
        LED_KANA = 0x04,
        LED_SLEEP = 0x05,
        LED_SUSPEND = 0x06,
        LED_MUTE = 0x07,
        LED_MISC = 0x08,
        LED_MAIL = 0x09,
        LED_CHARGING = 0x0a,

        LED_MAX = 0x0f,
        LED_CNT = LED_MAX + 1,
    }

    internal enum input_repeat_code
    {
        REP_DELAY = 0x00,
        REP_PERIOD = 0x01,

        REP_MAX = 0x01,
        REP_CNT = REP_MAX + 1,
    }

    internal enum input_sound_code
    {
        SND_CLICK = 0x00,
        SND_BELL = 0x01,
        SND_TONE = 0x02,

        SND_MAX = 0x07,
        SND_CNT = SND_MAX + 1,
    }

    internal enum input_sound_profile
    {
        SND_PROFILE_SILENT = 0x00,
        SND_PROFILE_VIBRATE = 0x01,
        SND_PROFILE_RING = 0x02,
    }

    // intentionally public
    public static class EvdevExtensions
    {
        public static bool IsDpadKey(this input_key_code key)
        {
            return
                key == input_key_code.BTN_DPAD_UP ||
                key == input_key_code.BTN_DPAD_DOWN ||
                key == input_key_code.BTN_DPAD_LEFT ||
                key == input_key_code.BTN_DPAD_RIGHT;
        }

        public static bool IsAxisX(this input_abs_code abs)
        {
            return
                abs == input_abs_code.ABS_X ||
                abs == input_abs_code.ABS_RX ||
                abs == input_abs_code.ABS_TILT_X;
        }

        public static bool IsAxisY(this input_abs_code abs)
        {
            return
                abs == input_abs_code.ABS_Y ||
                abs == input_abs_code.ABS_RY ||
                abs == input_abs_code.ABS_TILT_Y;
        }

        public static bool IsHatX(this input_abs_code abs)
        {
            return
                abs == input_abs_code.ABS_HAT0X ||
                abs == input_abs_code.ABS_HAT1X ||
                abs == input_abs_code.ABS_HAT2X ||
                abs == input_abs_code.ABS_HAT3X;
        }

        public static bool IsHatY(this input_abs_code abs)
        {
            return
                abs == input_abs_code.ABS_HAT0Y ||
                abs == input_abs_code.ABS_HAT1Y ||
                abs == input_abs_code.ABS_HAT2Y ||
                abs == input_abs_code.ABS_HAT3Y;
        }
    }
}
