namespace engine.inputs;

/**
 * ScanCode -> the engine's key-code string ("a", "(enter)", "(cursorleft)").
 *
 * WHY THIS LIVES HERE AND NOT IN A BACKEND
 *
 * It used to live in Splash.Silk/Sdl3KeyCodes.cs, whose own doc-comment warned that it
 * and Platform._convertKeyCodeFromPlatform "must produce identical strings for the same
 * physical key or the same keypress means different things on different platforms". Two
 * tables that must agree are a defect waiting for someone to edit one of them. There is
 * now ONE table, keyed on a platform-neutral ScanCode, and the backends only cast.
 *
 * WHAT THESE STRINGS ARE
 *
 * They are the existing CONTRACT with game code - `Scene.cs` tests for "(F8)",
 * `InputMapper` keys its JSON on them. They are deliberately unchanged here, so this
 * step introduces the ScanCode channel without breaking a single binding.
 *
 * WHAT THESE STRINGS ARE NOT
 *
 * They are not characters, and not display labels, even where they look like one. "a"
 * means THE A-POSITION KEY, which on AZERTY is the key printed Q. Text entry must use
 * Event.INPUT_KEY_CHARACTER; a rebinding UI must ask the platform for a display name.
 * See the comment on ScanCode for the full three-way split.
 *
 * WP-6.4 is expected to move bindings onto ScanCode directly and let these strings
 * retire; until then both travel on the event.
 */
public static class ScanCodeNames
{
    /**
     * Null for anything the engine does not bind - the historical behaviour, kept
     * deliberately. An unmapped key is ignored rather than delivered under some
     * invented name.
     */
    public static string? ToEngineCode(ScanCode scanCode) => scanCode switch
    {
        ScanCode.LeftShift => "(shiftleft)",
        ScanCode.RightShift => "(shiftright)",
        ScanCode.Space => " ",

        ScanCode.D0 => "0",
        ScanCode.D1 => "1",
        ScanCode.D2 => "2",
        ScanCode.D3 => "3",
        ScanCode.D4 => "4",
        ScanCode.D5 => "5",
        ScanCode.D6 => "6",
        ScanCode.D7 => "7",
        ScanCode.D8 => "8",
        ScanCode.D9 => "9",

        ScanCode.A => "a",
        ScanCode.D => "d",
        ScanCode.E => "e",
        ScanCode.F => "f",
        ScanCode.S => "s",
        ScanCode.Q => "q",
        ScanCode.W => "w",
        ScanCode.Z => "z",

        ScanCode.Return => "(enter)",
        ScanCode.Tab => "(tab)",
        ScanCode.Escape => "(escape)",

        ScanCode.F8 => "(F8)",
        ScanCode.F9 => "(F9)",
        ScanCode.F10 => "(F10)",
        ScanCode.F11 => "(F11)",
        ScanCode.F12 => "(F12)",

        ScanCode.Up => "(cursorup)",
        ScanCode.Down => "(cursordown)",
        ScanCode.Right => "(cursorright)",
        ScanCode.Left => "(cursorleft)",

        ScanCode.Delete => "(delete)",
        ScanCode.Backspace => "(backspace)",

        _ => null,
    };
}
