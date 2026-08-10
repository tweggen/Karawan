using engine.inputs;
using SDL3;

namespace Splash.Silk;

/// <summary>
/// SDL3 scancode to the engine's platform-neutral <see cref="ScanCode"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>This used to be a translation table and is now a cast.</b> Both sides are USB HID
/// Keyboard/Keypad usage IDs (HID Usage Tables page 0x07): <c>SDL_SCANCODE_A</c> is 4 and
/// <c>ScanCode.A</c> is 4, because <c>ScanCode</c>'s values were extracted from
/// <c>Platform.SDL3/vendor/SDL3.Core.cs</c> rather than typed from memory. There is no
/// mapping left to get wrong.
/// </para>
/// <para>
/// The string table that lived here has moved to <c>engine.inputs.ScanCodeNames</c>. Its
/// old doc-comment warned that it and <c>Platform._convertKeyCodeFromPlatform</c> "must
/// produce identical strings for the same physical key or the same keypress means
/// different things on different platforms" - two tables obliged to agree, which is a
/// defect waiting for someone to edit one of them. There is now one table, keyed on
/// <see cref="ScanCode"/>, and backends only cast.
/// </para>
/// <para>
/// Scancodes, not keycodes: a scancode is the physical key position and does not move with
/// the keyboard layout. Using keycodes here would put WASD movement on ZQSD for a French
/// layout. Text entry does NOT come through here - it arrives as
/// <c>SDL_EVENT_TEXT_INPUT</c>, already composed by layout and IME.
/// </para>
/// </remarks>
internal static class Sdl3KeyCodes
{
    /// <summary>
    /// Identity cast. Values SDL defines outside <see cref="ScanCode"/>'s enumerated set
    /// (SDL carries 249 entries up to 512 - media keys, international keys, and its own
    /// SDL_SCANCODE_MODE) pass through as their numeric value rather than being dropped,
    /// so a future binding UI can still round-trip them. Naming them is not required for
    /// the cast to be correct.
    /// </summary>
    public static ScanCode ToScanCode(SDL.SDL_Scancode scancode) => (ScanCode)(int)scancode;

    /// <summary>
    /// Convenience for the backend: physical position plus the legacy engine code string,
    /// which is null for any key the engine does not bind.
    /// </summary>
    public static (ScanCode ScanCode, string? EngineCode) Translate(SDL.SDL_Scancode scancode)
    {
        ScanCode sc = ToScanCode(scancode);
        return (sc, ScanCodeNames.ToEngineCode(sc));
    }
}
