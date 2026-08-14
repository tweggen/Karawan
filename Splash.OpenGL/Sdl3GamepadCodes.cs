using SDL3;

namespace Splash.OpenGL;

/// <summary>
/// SDL3 gamepad buttons and axes to the engine's gamepad contract.
/// </summary>
/// <remarks>
/// <para>
/// Same job as <see cref="Sdl3KeyCodes"/>, for the gamepad: the left-hand side is SDL's,
/// the right-hand side is the CONTRACT that <c>InputController</c> already reads, and which
/// the Silk path produces from <c>Silk.NET.Input.ButtonName</c> and <c>Thumbstick</c>/
/// <c>Trigger</c>. The two must agree or a gamepad means different things per backend.
/// </para>
/// <para>
/// <b>Only <c>Event.Code</c> is actually consumed for buttons.</b>
/// <c>InputController._onGamepadButtonPressed</c> switches on the string and today handles
/// exactly one case, <c>"RightStick"</c>; <c>Data1</c>/<c>Data2</c> are set to mirror the
/// Silk path's shape but nothing reads them. So the strings below must match Silk's enum
/// NAMES exactly, while the ordinals need only be stable - pinning Silk's enum layout is
/// neither possible nor necessary.
/// </para>
/// </remarks>
internal static class Sdl3GamepadCodes
{
    /*
     * Mirrors Silk.NET.Input.ButtonName. Verified against the shipped
     * Silk.NET.Input.Common.xml: Unknown, A, B, X, Y, Back, Home, Start, LeftStick,
     * RightStick, LeftBumper, RightBumper, DPadUp, DPadDown, DPadLeft, DPadRight.
     *
     * Note SDL names its face buttons by POSITION (south/east/west/north) where Silk names
     * them by LABEL (A/B/X/Y). On an Xbox-layout pad those coincide, which is the layout
     * the mapping below assumes - the same assumption the Silk path makes implicitly.
     */
    public static string? ToEngineButtonName(SDL.SDL_GamepadButton button) => button switch
    {
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => "A",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => "B",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => "X",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => "Y",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK => "Back",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE => "Home",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START => "Start",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK => "LeftStick",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK => "RightStick",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER => "LeftBumper",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER => "RightBumper",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => "DPadUp",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => "DPadDown",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => "DPadLeft",
        SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => "DPadRight",

        // Paddles, misc and touchpad have no Silk equivalent; ignored, as unmapped keys are.
        _ => null
    };

    /*
     * Stable ordinal for Event.Data1, mirroring the Silk path's (uint)button.Name. Not
     * consumed downstream - see the class remarks.
     */
    public static uint ToEngineButtonOrdinal(string engineName) => engineName switch
    {
        "A" => 1, "B" => 2, "X" => 3, "Y" => 4,
        "Back" => 5, "Home" => 6, "Start" => 7,
        "LeftStick" => 8, "RightStick" => 9,
        "LeftBumper" => 10, "RightBumper" => 11,
        "DPadUp" => 12, "DPadDown" => 13, "DPadLeft" => 14, "DPadRight" => 15,
        _ => 0
    };

    /// <summary>The full extent of a signed SDL axis. Sticks report -32768..32767.</summary>
    private const float AxisRange = 32767f;

    /// <summary>
    /// Stick axis to the engine's -1..+1. SDL's sign is passed through unchanged.
    /// </summary>
    /// <remarks>
    /// <b>An earlier revision negated Y and that was wrong.</b> The reasoning was that SDL
    /// reports +Y as DOWN while <c>InputController._onStickMoved</c> stores
    /// <c>PhysicalPosition.Y &gt; 0</c> into <c>AnalogLeftStickUp</c> - so a flip looked
    /// obviously correct. It is not: that field name describes which accumulator the value
    /// lands in, not which way the character walks. Measured on Windows 11 at GATE-C,
    /// front/back came out inverted while walking.
    ///
    /// The contract is defined by what the Silk path produced, and Silk normalises axes
    /// uniformly without flipping. Passing SDL's sign through matches it. Derive input
    /// conventions from the observed end behaviour, not from what a field is called.
    /// </remarks>
    public static float StickAxisToEngine(short value, bool isVertical)
    {
        return System.Math.Clamp(value / AxisRange, -1f, 1f);
    }

    /// <summary>
    /// Trigger axis to the engine's -1..+1, where -1 is fully RELEASED.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SDL reports triggers as 0..32767. The engine's convention is derived from its only
    /// consumer, <c>InputController._onTriggerMoved</c>:
    /// </para>
    /// <code>AnalogLeft2 = (int)(255f * (ev.PhysicalPosition.X + 1f) / 2f);</code>
    /// <para>
    /// That maps -1 to 0 and +1 to 255, so a released trigger MUST arrive as -1. Deriving it
    /// from the consumer rather than from Silk is deliberate: if Silk's SDL backend
    /// normalises triggers to 0..1 - which its uniform <c>value / 32767</c> would - then a
    /// released trigger reads 0 and the formula yields 127, i.e. the car brakes at rest for
    /// as long as the pad is connected. Whichever way Silk behaves, this is the mapping that
    /// makes the game correct, so it is the one to verify at GATE-C.
    /// </para>
    /// </remarks>
    public static float TriggerAxisToEngine(short value)
    {
        float f = System.Math.Clamp(value / AxisRange, 0f, 1f);
        return f * 2f - 1f;
    }
}
