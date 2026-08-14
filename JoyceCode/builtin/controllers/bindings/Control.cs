using System;
using engine.inputs;

namespace builtin.controllers.bindings;

/**
 * What kind of physical thing produced a value.
 */
public enum ControlKind
{
    None = 0,

    /**
     * A keyboard key, identified by POSITION (ScanCode / USB HID usage id), never by
     * the character it prints. See Control's doc-comment for why that distinction is
     * the whole point of storing bindings this way.
     */
    Key,

    /**
     * A gamepad button, identified by the engine's button name ("Y", "DPadUp",
     * "LeftShoulder"). Unlike keys these are already positional - a gamepad's A button
     * is the same physical button everywhere - so no separate scancode channel exists
     * or is needed.
     */
    GamepadButton,

    /**
     * A gamepad trigger, identified by its index (0 = left, 1 = right, matching
     * Event.Data1 on INPUT_GAMEPAD_TRIGGER_MOVED).
     */
    GamepadTrigger,

    /**
     * A mouse button, identified by index.
     */
    MouseButton,
}


/**
 * One physical control a binding can be attached to (WP-6.4).
 *
 * WHY THIS IS A VALUE TYPE WITH A STRING FORM
 *
 * A binding has to survive three round trips: into JSON and back, into a save file and
 * back, and through a rebinding UI that captures a control and writes it down. A struct
 * with an unambiguous canonical string satisfies all three without a serializer.
 *
 * WHY KEYS ARE STORED AS ScanCode AND NOT AS THE ENGINE'S KEY STRING
 *
 * This is the point of the exercise, and it is easy to get backwards. The engine's key
 * strings ("w", "(escape)") are POSITIONS that merely look like characters: on AZERTY
 * the physical W-position key prints Z, and `ScanCodeNames` says so explicitly. Storing
 * "w" therefore works today only because the string happens to be positional too - but
 * it reads as a character, so the next person to touch it will reasonably assume a
 * layout-dependent meaning and break someone's saved bindings.
 *
 * Storing the ScanCode removes the ambiguity: `Key:W` cannot be read as "the key that
 * prints w". A rebinding UI asks the platform for the display label separately
 * (SDL_GetKeyName(SDL_GetKeyFromScancode(...))) - layout-dependent, display-only, and
 * never used for lookup.
 */
public readonly struct Control : IEquatable<Control>
{
    public readonly ControlKind Kind;

    /**
     * ScanCode for Key; trigger/mouse index for those kinds; unused for
     * GamepadButton, which carries its identity in Name.
     */
    public readonly int Index;

    /**
     * Gamepad button name. Null for every other kind.
     */
    public readonly string? Name;


    public Control(ControlKind kind, int index, string? name = null)
    {
        Kind = kind;
        Index = index;
        Name = name;
    }


    public static Control Key(ScanCode scanCode) => new(ControlKind.Key, (int)scanCode);

    public static Control GamepadButton(string name) => new(ControlKind.GamepadButton, 0, name);

    public static Control GamepadTrigger(int index) => new(ControlKind.GamepadTrigger, index);

    public static Control MouseButton(int index) => new(ControlKind.MouseButton, index);

    public static readonly Control None = new(ControlKind.None, 0);


    public ScanCode ScanCode => Kind == ControlKind.Key ? (ScanCode)Index : engine.inputs.ScanCode.Unknown;


    /**
     * The canonical persisted form, e.g. "Key:W", "GamepadButton:DPadUp",
     * "GamepadTrigger:1". Round-trips through TryParse.
     *
     * Keys use the ScanCode's ENUM NAME rather than its numeric value: a human editing
     * the JSON should not have to know that W is 26, and a name survives a renumbering
     * of the enum while a number does not.
     */
    public override string ToString() => Kind switch
    {
        ControlKind.Key => $"Key:{(ScanCode)Index}",
        ControlKind.GamepadButton => $"GamepadButton:{Name}",
        ControlKind.GamepadTrigger => $"GamepadTrigger:{Index}",
        ControlKind.MouseButton => $"MouseButton:{Index}",
        _ => "None",
    };


    public static bool TryParse(string? s, out Control control)
    {
        control = None;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        int idxColon = s.IndexOf(':');
        if (idxColon <= 0 || idxColon == s.Length - 1)
        {
            return "None" == s;
        }

        string strKind = s.Substring(0, idxColon);
        string strValue = s.Substring(idxColon + 1);

        switch (strKind)
        {
            case "Key":
                /*
                 * Enum NAME only, and the digit check is load-bearing: Enum.TryParse
                 * also accepts the underlying NUMBER, so "Key:26" would otherwise parse
                 * happily as W. That is the one form this must reject - a number keeps
                 * parsing after a renumbering of ScanCode and silently means a
                 * different physical key, which is exactly what storing bindings
                 * positionally is meant to prevent.
                 */
                char first = strValue[0];
                if (char.IsDigit(first) || '-' == first || '+' == first)
                {
                    return false;
                }

                if (Enum.TryParse<ScanCode>(strValue, ignoreCase: false, out var scanCode)
                    && Enum.IsDefined(typeof(ScanCode), scanCode))
                {
                    control = Key(scanCode);
                    return true;
                }

                return false;

            case "GamepadButton":
                control = GamepadButton(strValue);
                return true;

            case "GamepadTrigger":
                if (int.TryParse(strValue, out var idxTrigger))
                {
                    control = GamepadTrigger(idxTrigger);
                    return true;
                }

                return false;

            case "MouseButton":
                if (int.TryParse(strValue, out var idxMouse))
                {
                    control = MouseButton(idxMouse);
                    return true;
                }

                return false;

            default:
                return false;
        }
    }


    public bool Equals(Control other)
        => Kind == other.Kind && Index == other.Index && Name == other.Name;

    public override bool Equals(object? o) => o is Control c && Equals(c);

    public override int GetHashCode() => HashCode.Combine((int)Kind, Index, Name);

    public static bool operator ==(Control a, Control b) => a.Equals(b);

    public static bool operator !=(Control a, Control b) => !a.Equals(b);
}
