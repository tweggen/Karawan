using System;
using System.Collections.Generic;
using System.Linq;
using builtin.controllers.bindings;
using engine;
using engine.news;
using static engine.Logger;

namespace builtin.controllers;

/**
 * Drives one rebinding interaction: "press a key for <action>" (WP-6.4).
 *
 * WHY THIS IS AN IInputPart AND NOT JUST A CALL TO BindingTable.BeginCapture
 *
 * Capture mode already stops InputMapper translating the pressed key into a logical
 * action, so binding Escape does not also close the menu. That is necessary and it is not
 * sufficient, because the platform pushes BOTH events: the raw key AND its translation.
 * Suppressing the translation leaves the raw one, and the menu widgets switch on raw codes
 * too - Widget.cs handles "w", "(cursorup)", " " and "e" directly. Without this part,
 * pressing W to rebind "walk forward" would also move the menu cursor up, and pressing
 * Space would activate whatever option that landed on.
 *
 * So while capturing, this consumes every key and gamepad-button event at the front of the
 * pipeline. Z order 1000 puts it ahead of the map overlay at 500, which is the highest
 * anything else claims - PriorityMap.Add negates the key, so HIGHER runs first.
 *
 * WHY IT SAVES IMMEDIATELY
 *
 * A rebinding screen with an explicit Apply is a screen that can lose the user's work, and
 * the binding table is already the live one - the change has taken effect by the time they
 * see it. Writing the file at the same moment keeps what they see and what persists from
 * disagreeing.
 */
public class RebindController : AModule, IInputPart
{
    private static readonly engine.Dc _dc = engine.Dc.Input;

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<InputEventPipeline>(),
        new SharedModule<InputMapper>()
    };

    /**
     * Ahead of everything. See the class comment: the point is to reach the key before the
     * menu widgets do, and they are ordinary input parts.
     */
    public float MY_Z_ORDER { get; set; } = 1000f;

    private readonly object _lo = new();

    private string? _pendingAction;

    /**
     * The action currently waiting for a control, or null. A UI polls this to show
     * "press a key..." on the right row.
     */
    public string? PendingAction
    {
        get
        {
            lock (_lo)
            {
                return _pendingAction;
            }
        }
    }


    /**
     * Raised after a successful rebind or a cancel, so a UI can re-render. Carries the
     * action that was being bound.
     */
    public Action<string>? OnCaptureFinished;


    private InputMapper _mapper => M<InputMapper>();


    public void BeginCapture(string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return;
        }

        lock (_lo)
        {
            _pendingAction = action;
        }

        _mapper.OnControlCaptured = _onControlCaptured;
        _mapper.Bindings.BeginCapture();
        Trace(_dc, $"Capturing a control for action '{action}'.");
    }


    /**
     * Abandon the capture without changing anything. A screen that can enter "press a
     * key" with no way out is a screen that can trap a player who changed their mind -
     * and the way out cannot be a key, because every key is being captured.
     */
    public void CancelCapture()
    {
        string? action;
        lock (_lo)
        {
            action = _pendingAction;
            _pendingAction = null;
        }

        /*
         * Nothing to cancel, and nothing to reach for. Checked before touching the mapper
         * because this also runs from OnModuleDeactivate, where the dependency may already
         * be on its way out.
         */
        if (null == action)
        {
            return;
        }

        _mapper.Bindings.EndCapture();
        _mapper.OnControlCaptured = null;

        Trace(_dc, $"Capture for action '{action}' cancelled.");
        OnCaptureFinished?.Invoke(action);
    }


    /**
     * The one control that cancels instead of binding.
     *
     * Capture swallows every key and every gamepad button, which is the point - but it
     * also means a player who entered capture by accident has no way out except the mouse,
     * and a gamepad player has no way out at all. Reserving Escape is the convention, and
     * the cost is small and stated: Escape cannot be assigned FROM this screen. It remains
     * assignable by editing the binding file, and it stays bound to "menu" by default.
     */
    public static readonly Control CancelControl = Control.Key(engine.inputs.ScanCode.Escape);


    private void _onControlCaptured(Control control)
    {
        /*
         * Checked here rather than in the input pipeline, because by the time a raw event
         * reaches an IInputPart this has already run: InputMapper.ToLogical short-circuits
         * into OnControlCaptured synchronously while the platform is still pushing the
         * event. An Escape handled in InputPartOnInputEvent would arrive after it had
         * already been bound.
         */
        if (CancelControl == control)
        {
            CancelCapture();
            return;
        }

        string? action;
        lock (_lo)
        {
            action = _pendingAction;
            _pendingAction = null;
        }

        _mapper.OnControlCaptured = null;

        if (null == action)
        {
            return;
        }

        Rebind(_mapper.Bindings, action, control);
        _mapper.SaveUserBindings();

        Trace(_dc, $"Bound {control} to '{action}'.");
        OnCaptureFinished?.Invoke(action);
    }


    /**
     * What "Rebind" means, as opposed to what BindingTable.Bind means.
     *
     * Bind APPENDS, deliberately: an action carries several controls, and "interact" is E
     * on the keyboard AND Y on the pad. That is right for the table and wrong for this
     * button. A player who rebinds jump to J and finds Space still jumping has not rebound
     * anything - they have added a second key, and the one they wanted rid of still fires.
     * That is precisely the defect part 1 refused to ship when it MOVED the button
     * bindings out of nogame.implementations.json rather than duplicating them.
     *
     * So the existing control of the SAME KIND is removed first. Same kind, not
     * everything: pressing a key must not silently unbind the gamepad button, or a player
     * rebinding at the keyboard quietly loses their controller.
     *
     * Static and table-in so it is testable without an engine, a platform or a menu -
     * which matters, because everything else about a rebinding screen needs all three.
     */
    public static void Rebind(BindingTable table, string action, Control control)
    {
        var binding = table.Find(action);
        if (null != binding)
        {
            foreach (var existing in binding.Controls.Where(c => c.Kind == control.Kind).ToList())
            {
                table.Unbind(action, existing);
            }
        }

        /*
         * Bind also removes the control from whatever else it drove, so a player who moves
         * jump onto E does not end up with E doing both jump and interact.
         */
        table.Bind(action, control);
    }


    /**
     * Throw away the user's overrides and go back to what ships.
     *
     * Deletes the file rather than writing the defaults into it. A user file containing a
     * copy of today's defaults would mask any binding a later update adds - which is the
     * whole reason the shipped file and the user file are separate.
     */
    public bool ResetToDefaults()
    {
        try
        {
            string userPath = InputMapper.UserBindingsPath;
            if (System.IO.File.Exists(userPath))
            {
                System.IO.File.Delete(userPath);
            }
        }
        catch (Exception e)
        {
            Error(_dc, $"Unable to remove user bindings: {e.Message}");
            return false;
        }

        _mapper.Bindings.Clear();
        _mapper.LoadBindings();
        Trace(_dc, $"Bindings reset to the shipped defaults.");
        return true;
    }


    public void InputPartOnInputEvent(Event ev)
    {
        if (null == PendingAction)
        {
            return;
        }

        /*
         * Everything a control can arrive on is swallowed while capturing. Note this runs
         * IN ADDITION to InputMapper's capture short-circuit, not instead of it: that one
         * stops the logical translation, this one stops the raw event. Both halves are
         * needed because the platform pushes both.
         *
         * Mouse and touch are left alone deliberately - the user still has to be able to
         * click Cancel.
         */
        switch (ev.Type)
        {
            case Event.INPUT_KEY_PRESSED:
            case Event.INPUT_KEY_RELEASED:
            case Event.INPUT_KEY_CHARACTER:
            case Event.INPUT_GAMEPAD_BUTTON_PRESSED:
            case Event.INPUT_GAMEPAD_BUTTON_RELEASED:
            case Event.INPUT_BUTTON_PRESSED:
            case Event.INPUT_BUTTON_RELEASED:
                ev.IsHandled = true;
                break;
        }
    }


    protected override void OnModuleDeactivate()
    {
        CancelCapture();
        M<InputEventPipeline>().RemoveInputPart(this);
    }


    protected override void OnModuleActivate()
    {
        M<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
    }
}
