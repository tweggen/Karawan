using System.Collections.Generic;

namespace builtin.controllers.bindings;

/**
 * One game-meaningful verb and the controls bound to it (WP-6.4).
 *
 * An action carries SEVERAL controls, not one: "interact" is E on the keyboard and Y on
 * the gamepad, and that is one action with two bindings rather than two actions. The
 * table this replaces stored press and release as separate rows, which meant a rebind
 * had to remember to edit both and nothing checked that it did.
 */
public class ActionBinding
{
    /**
     * The action id as game code sees it, e.g. "interact". Note this is WITHOUT the
     * angle brackets: the brackets belong to the logical EVENT code ("<interact>"),
     * which BindingTable adds when it emits. Keeping them out of the id means the JSON
     * reads as a name rather than as a piece of the event wire format.
     */
    public string Action = "";

    /**
     * Human-readable, for a rebinding UI. Optional.
     */
    public string? Description;

    public List<Control> Controls = new();

    /**
     * Applied in order, to analog controls only. Empty for a plain button.
     */
    public List<IInputModifier> Modifiers = new();


    public bool Has(Control control) => Controls.Contains(control);
}
