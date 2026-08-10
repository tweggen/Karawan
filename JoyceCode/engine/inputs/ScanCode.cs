namespace engine.inputs;

/**
 * A PHYSICAL KEY POSITION, not a character.
 *
 * The values are USB HID Keyboard/Keypad usage IDs (HID Usage Tables, page 0x07) -
 * A = 0x04, 1 = 0x1E, Return = 0x28, Left Control = 0xE0. That choice is not arbitrary
 * and it is not ours: it is what the hardware reports, what SDL_Scancode already is,
 * and what every other platform layer ends up translating to. Adopting it means
 * Sdl3KeyCodes is a CAST rather than a translation table, so the enum is
 * platform-neutral BY CONSTRUCTION rather than by somebody maintaining a mapping
 * correctly.
 *
 * WHY POSITIONS AND NOT CHARACTERS
 *
 * ScanCode.W is "the key one row up and one left of centre on a US keyboard", wherever
 * the user's layout puts a W. On AZERTY that physical key prints Z. Bindings must use
 * positions, or WASD movement silently becomes ZQSD-shaped nonsense for French users
 * while still being spelled "WASD" in the config.
 *
 * THE THREE-WAY SPLIT THIS IS ONE THIRD OF
 *
 *   1. ScanCode          - physical position. Bindings use ONLY this.
 *   2. Event.INPUT_KEY_CHARACTER - text, already composed by layout AND IME. Text entry
 *                          uses ONLY this, and never synthesises characters from key
 *                          events; that is what makes dead keys, accents and Android
 *                          IME composition work.
 *   3. A display label   - what the user's layout actually prints on that key, via
 *                          SDL_GetKeyName(SDL_GetKeyFromScancode(...)). Layout-dependent,
 *                          DISPLAY ONLY, never used for lookup. This is the one usually
 *                          forgotten - it is why "press a key to bind" screens show Z on
 *                          AZERTY while correctly storing the W position.
 *
 * Only names are ours; the numbers are the standard. Values were extracted from the
 * vendored SDL3 binding (Platform.SDL3/vendor/SDL3.Core.cs, enum SDL_Scancode) rather
 * than typed from memory, so the cast in Sdl3KeyCodes is provably identity for every
 * member listed here.
 */
public enum ScanCode
{
    Unknown = 0,

    // Letters - USB HID 0x04..0x1D. Positions, not characters.
    A = 4, B = 5, C = 6, D = 7, E = 8, F = 9, G = 10, H = 11, I = 12,
    J = 13, K = 14, L = 15, M = 16, N = 17, O = 18, P = 19, Q = 20,
    R = 21, S = 22, T = 23, U = 24, V = 25, W = 26, X = 27, Y = 28, Z = 29,

    // Number row. Note HID orders these 1..9 then 0, which is why D0 is not 30.
    D1 = 30, D2 = 31, D3 = 32, D4 = 33, D5 = 34,
    D6 = 35, D7 = 36, D8 = 37, D9 = 38, D0 = 39,

    Return = 40,
    Escape = 41,
    Backspace = 42,
    Tab = 43,
    Space = 44,

    Minus = 45,
    Equals = 46,
    LeftBracket = 47,
    RightBracket = 48,
    Backslash = 49,
    Semicolon = 51,
    Apostrophe = 52,
    Grave = 53,
    Comma = 54,
    Period = 55,
    Slash = 56,

    CapsLock = 57,

    F1 = 58, F2 = 59, F3 = 60, F4 = 61, F5 = 62, F6 = 63,
    F7 = 64, F8 = 65, F9 = 66, F10 = 67, F11 = 68, F12 = 69,

    PrintScreen = 70,
    ScrollLock = 71,
    Pause = 72,
    Insert = 73,
    Home = 74,
    PageUp = 75,
    Delete = 76,
    End = 77,
    PageDown = 78,

    Right = 79,
    Left = 80,
    Down = 81,
    Up = 82,

    NumLock = 83,
    KeypadDivide = 84,
    KeypadMultiply = 85,
    KeypadMinus = 86,
    KeypadPlus = 87,
    KeypadEnter = 88,
    Keypad1 = 89, Keypad2 = 90, Keypad3 = 91, Keypad4 = 92, Keypad5 = 93,
    Keypad6 = 94, Keypad7 = 95, Keypad8 = 96, Keypad9 = 97, Keypad0 = 98,
    KeypadPeriod = 99,

    // Modifiers - USB HID 0xE0..0xE7. Left and right are DISTINCT positions; a binding
    // layer that wants "either shift" has to say so itself.
    LeftControl = 224,
    LeftShift = 225,
    LeftAlt = 226,
    LeftGui = 227,
    RightControl = 228,
    RightShift = 229,
    RightAlt = 230,
    RightGui = 231,
}
