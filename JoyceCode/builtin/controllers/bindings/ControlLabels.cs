using System.Collections.Generic;
using System.Text;
using engine;
using engine.inputs;

namespace builtin.controllers.bindings;

/**
 * What a Control is called when shown to a human (WP-6.4, rebinding UI).
 *
 * THIS IS THE THIRD CHANNEL, AND IT IS DELIBERATELY ONE-WAY
 *
 * ScanCode's doc-comment describes a three-way split: the POSITION a binding is stored by,
 * the TEXT that arrives already composed by layout and IME, and the LABEL printed on the
 * user's key. Bindings use the first. Text entry uses the second. Only this file uses the
 * third, and it never feeds anything back:
 *
 *   Control -> label     yes, for display
 *   label   -> Control   NEVER
 *
 * because the label moves when the user changes keyboard layout. Storing or matching on it
 * would make a saved binding mean a different physical key after a layout switch, which is
 * the entire failure that storing ScanCode exists to prevent.
 *
 * WHY THE PLATFORM IS ASKED
 *
 * On AZERTY the key at the W position prints Z. A rebinding screen that renders the
 * positional name would instruct the user to press a key their keyboard does not have.
 * SDL answers this properly (SDL_GetKeyName(SDL_GetKeyFromScancode(...))); the fallbacks
 * below exist because a headless engine has no platform at all, not because the fallback
 * is good enough.
 */
public static class ControlLabels
{
    /**
     * Gamepad controls carry this so a mixed list reads unambiguously: "E / Pad Y" says
     * which half is the keyboard without needing a legend.
     */
    private const string PadPrefix = "Pad ";


    public static string Of(Control control) => Of(control, I.Get<Engine>());


    /**
     * Engine passed explicitly so this is testable without a running engine, and callable
     * from one that has no platform.
     */
    public static string Of(Control control, Engine? engine)
    {
        switch (control.Kind)
        {
            case ControlKind.Key:
                return _keyLabel(control.ScanCode, engine);

            case ControlKind.GamepadButton:
                return PadPrefix + (control.Name ?? "?");

            case ControlKind.GamepadTrigger:
                return PadPrefix + control.Index switch
                {
                    0 => "LT",
                    1 => "RT",
                    _ => $"Trigger {control.Index}"
                };

            case ControlKind.GamepadStick:
                return PadPrefix + control.Index switch
                {
                    0 => "L-Stick",
                    1 => "R-Stick",
                    _ => $"Stick {control.Index}"
                };

            case ControlKind.MouseButton:
                return control.Index switch
                {
                    0 => "LMB",
                    1 => "RMB",
                    2 => "MMB",
                    _ => $"Mouse {control.Index}"
                };

            default:
                return "-";
        }
    }


    /**
     * Platform label first, then the ScanCode's enum name.
     *
     * The fallback is the ENUM name ("Escape", "Space", "W"), not ScanCodeNames' engine
     * code ("(escape)", " ", "w"). Those codes are a wire format that happens to be
     * legible; " " in particular renders as an empty cell, which reads as "unbound"
     * rather than "space bar".
     */
    private static string _keyLabel(ScanCode scanCode, Engine? engine)
    {
        string? fromPlatform = engine?.GetKeyDisplayName(scanCode);
        if (!string.IsNullOrWhiteSpace(fromPlatform))
        {
            return fromPlatform!;
        }

        return ScanCode.Unknown == scanCode ? "-" : scanCode.ToString();
    }


    /**
     * All the controls of one action, e.g. "E / Pad Y". Empty reads as "unbound" rather
     * than as an empty cell, because a blank row in a rebinding screen is indistinguishable
     * from a rendering bug.
     */
    public static string Of(IReadOnlyList<Control> controls, Engine? engine)
    {
        if (null == controls || 0 == controls.Count)
        {
            return "unbound";
        }

        var sb = new StringBuilder();
        for (int i = 0; i < controls.Count; ++i)
        {
            if (i > 0)
            {
                sb.Append(" / ");
            }

            sb.Append(Of(controls[i], engine));
        }

        return sb.ToString();
    }
}
