using System.Collections.Generic;
using System.Linq;
using builtin.controllers.bindings;
using engine;

namespace builtin.controllers;

/// <summary>
/// Lua bindings for the key-rebinding screen (WP-6.4).
/// </summary>
/// <remarks>
/// Mirrors QuestLuaBindings: a flat list of dictionaries for the menu's
/// <c>&lt;for items='...'&gt;</c>, plus the verbs its options call.
///
/// Everything here is a thin forward to <c>RebindController</c> and <c>BindingTable</c>.
/// The interesting decisions - suppressing the raw key while capturing, binding stealing a
/// control from its previous action, deleting the user file rather than rewriting it -
/// live there, where they are testable without a menu.
/// </remarks>
public class BindingsLuaBindings
{
    private static RebindController _rebind => I.Get<RebindController>();

    private static InputMapper _mapper => I.Get<InputMapper>();


    /// <summary>
    /// One row per action: what it is, what drives it, and whether this screen can
    /// rebind it.
    /// </summary>
    /// <remarks>
    /// <c>canRebind</c> is false for the analog actions, and the row is still shown. A
    /// screen that silently omitted "move" and "look" would leave the player unable to
    /// discover what the sticks do; one that offered to rebind them would be offering
    /// something capture cannot deliver, because capture recognises a PRESS and a stick
    /// does not press. Showing them read-only is the honest version of both.
    /// </remarks>
    public List<SortedDictionary<string, object>> getActionList()
    {
        var engine = I.Get<Engine>();
        string? pending = _rebind.PendingAction;

        var list = new List<SortedDictionary<string, object>>();
        foreach (var binding in _mapper.Bindings.Actions)
        {
            bool isAnalog = binding.Controls.Any(
                c => ControlKind.GamepadStick == c.Kind || ControlKind.GamepadTrigger == c.Kind);

            var row = new SortedDictionary<string, object>();
            row.Add("action", binding.Action);
            row.Add("description", binding.Description ?? binding.Action);
            row.Add("controls", ControlLabels.Of(binding.Controls, engine));
            row.Add("canRebind", !isAnalog);
            row.Add("capturing", binding.Action == pending);
            list.Add(row);
        }

        return list;
    }


    /// <summary>Is the screen currently waiting for a key?</summary>
    public bool isCapturing() => null != _rebind.PendingAction;


    public void beginCapture(string action) => _rebind.BeginCapture(action);


    public void cancelCapture() => _rebind.CancelCapture();


    public bool resetDefaults() => _rebind.ResetToDefaults();
}
