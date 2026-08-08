using SDL3;

namespace Splash.Silk;

/// <summary>
/// SDL3 scancode to the engine's key-code string.
/// </summary>
/// <remarks>
/// <para>
/// This is the "left-hand side" WP-3.1 talks about. The right-hand side - the strings
/// <c>"a"</c>, <c>"(enter)"</c>, <c>"(cursorleft)"</c> and the rest - is the CONTRACT, shared
/// with <c>Platform._convertKeyCodeFromPlatform</c>, which does the same job for Silk's
/// <c>Key</c> enum. The two tables must produce identical strings for the same physical key
/// or the same keypress means different things on different platforms.
/// </para>
/// <para>
/// <b>Scancodes, not keycodes.</b> A scancode is the physical key position and does not move
/// with the keyboard layout; a keycode does. The Silk table this mirrors is also
/// position-based, so matching it means matching scancodes - using keycodes here would make
/// WASD movement land on ZQSD for a French layout while the desktop build kept WASD.
/// </para>
/// <para>
/// Only the keys the engine actually binds are mapped; anything else returns null and is
/// ignored, exactly as the Silk path ignores unmapped keys. Text entry does NOT come through
/// here - it arrives as <c>SDL_EVENT_TEXT_INPUT</c>, already composed by the IME.
/// </para>
/// </remarks>
internal static class Sdl3KeyCodes
{
    public static string? ToEngineCode(SDL.SDL_Scancode scancode) => scancode switch
    {
        SDL.SDL_Scancode.SDL_SCANCODE_LSHIFT => "(shiftleft)",
        SDL.SDL_Scancode.SDL_SCANCODE_RSHIFT => "(shiftright)",
        SDL.SDL_Scancode.SDL_SCANCODE_SPACE => " ",

        SDL.SDL_Scancode.SDL_SCANCODE_0 => "0",
        SDL.SDL_Scancode.SDL_SCANCODE_1 => "1",
        SDL.SDL_Scancode.SDL_SCANCODE_2 => "2",
        SDL.SDL_Scancode.SDL_SCANCODE_3 => "3",
        SDL.SDL_Scancode.SDL_SCANCODE_4 => "4",
        SDL.SDL_Scancode.SDL_SCANCODE_5 => "5",
        SDL.SDL_Scancode.SDL_SCANCODE_6 => "6",
        SDL.SDL_Scancode.SDL_SCANCODE_7 => "7",
        SDL.SDL_Scancode.SDL_SCANCODE_8 => "8",
        SDL.SDL_Scancode.SDL_SCANCODE_9 => "9",

        SDL.SDL_Scancode.SDL_SCANCODE_A => "a",
        SDL.SDL_Scancode.SDL_SCANCODE_D => "d",
        SDL.SDL_Scancode.SDL_SCANCODE_E => "e",
        SDL.SDL_Scancode.SDL_SCANCODE_F => "f",
        SDL.SDL_Scancode.SDL_SCANCODE_S => "s",
        SDL.SDL_Scancode.SDL_SCANCODE_Q => "q",
        SDL.SDL_Scancode.SDL_SCANCODE_W => "w",
        SDL.SDL_Scancode.SDL_SCANCODE_Z => "z",

        SDL.SDL_Scancode.SDL_SCANCODE_RETURN => "(enter)",
        SDL.SDL_Scancode.SDL_SCANCODE_TAB => "(tab)",
        SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE => "(escape)",

        SDL.SDL_Scancode.SDL_SCANCODE_F8 => "(F8)",
        SDL.SDL_Scancode.SDL_SCANCODE_F9 => "(F9)",
        SDL.SDL_Scancode.SDL_SCANCODE_F10 => "(F10)",
        SDL.SDL_Scancode.SDL_SCANCODE_F11 => "(F11)",
        SDL.SDL_Scancode.SDL_SCANCODE_F12 => "(F12)",

        SDL.SDL_Scancode.SDL_SCANCODE_UP => "(cursorup)",
        SDL.SDL_Scancode.SDL_SCANCODE_DOWN => "(cursordown)",
        SDL.SDL_Scancode.SDL_SCANCODE_RIGHT => "(cursorright)",
        SDL.SDL_Scancode.SDL_SCANCODE_LEFT => "(cursorleft)",

        SDL.SDL_Scancode.SDL_SCANCODE_DELETE => "(delete)",
        SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE => "(backspace)",

        _ => null,
    };
}
