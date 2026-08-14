using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using builtin.controllers.bindings;
using engine;
using engine.news;
using static engine.Logger;

namespace builtin.controllers;


/**
 * Map the controller / keyboard input to logical game buttons.
 *
 * TWO TABLES, ONE OF THEM ON ITS WAY OUT (WP-6.4)
 *
 * `Bindings` is the action/binding layer: controls are stored by POSITION
 * (ScanCode), an action carries all the controls that drive it, the table is writable
 * at runtime, and it round-trips through JSON so a rebinding UI can persist. It is
 * consulted FIRST.
 *
 * `MapButtonToLogical` is the original flat table, keyed on the raw event string
 * ("input.key.pressed:e"). It still works and is still consulted, because emptying it
 * in the same change that introduces its replacement would put every binding in the
 * game on untested code at once. Anything the new table does not answer falls through
 * to it unchanged.
 *
 * Both emit the SAME logical events they always did - `input.button.pressed:<interact>`
 * and friends - so nothing downstream of this class can tell which table answered. That
 * is deliberate: it is what makes the migration invisible to game code, which is the
 * only reason it can be done incrementally.
 */
public class InputMapper : AModule
{
    private static readonly engine.Dc _dc = engine.Dc.Input;

    /**
     * The action/binding layer. Public so a rebinding UI can read, write and persist it.
     */
    public readonly BindingTable Bindings = new();

    /**
     * Raised with the captured control while Bindings.IsCapturing, instead of the event
     * being translated. This is the "listen for the next control" half of rebinding.
     */
    public Action<Control>? OnControlCaptured;

    private SortedDictionary<string, string> _mapButtonToLogical = new ();

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
    };

    private EventQueue _eq;
    
    
    public SortedDictionary<string, string> MapButtonToLogical
    {
        get
        {
            lock (_lo)
            {
                return _mapButtonToLogical;
            }
        }
        set
        {
            lock (_lo)
            {
                _mapButtonToLogical = new SortedDictionary<string, string>(value);;
            }
        }
    }
    

    private SortedDictionary<string, string> _mapLogicalToDescription = new ();
    
    public SortedDictionary<string, string> MapLogicalToDescription
    {
        get
        {
            lock (_lo)
            {
                return _mapLogicalToDescription;
            }
        }
        set
        {
            lock (_lo)
            {
                _mapLogicalToDescription = new SortedDictionary<string, string>(value);
            }
        }
    }


    /**
     * The control this event came from, or Control.None for an event that is not a
     * bindable control.
     *
     * Keys use ev.ScanCode, not ev.Code. Those agree today, but only the ScanCode is
     * guaranteed positional - see Control's doc-comment.
     */
    public static Control ControlOf(Event ev)
    {
        switch (ev.Type)
        {
            case Event.INPUT_KEY_PRESSED:
            case Event.INPUT_KEY_RELEASED:
                return ev.ScanCode != engine.inputs.ScanCode.Unknown
                    ? Control.Key(ev.ScanCode)
                    : Control.None;

            case Event.INPUT_GAMEPAD_BUTTON_PRESSED:
            case Event.INPUT_GAMEPAD_BUTTON_RELEASED:
                return string.IsNullOrEmpty(ev.Code)
                    ? Control.None
                    : Control.GamepadButton(ev.Code);

            default:
                return Control.None;
        }
    }


    private static bool _isPress(Event ev)
        => ev.Type == Event.INPUT_KEY_PRESSED || ev.Type == Event.INPUT_GAMEPAD_BUTTON_PRESSED;


    public Event? ToLogical(Event ev)
    {
        /*
         * Capture mode short-circuits everything. A rebinding prompt asking the user to
         * press a key must not ALSO act on that key - binding Escape would otherwise
         * close the menu that is doing the binding.
         */
        if (Bindings.IsCapturing)
        {
            Control captured = ControlOf(ev);
            if (captured != Control.None && _isPress(ev))
            {
                Bindings.EndCapture();
                OnControlCaptured?.Invoke(captured);
            }

            return null;
        }

        Control control = ControlOf(ev);
        if (control != Control.None)
        {
            string? action = Bindings.ActionOf(control);
            if (null != action)
            {
                /*
                 * The angle brackets live here, not in the binding file: the action id
                 * is a name, "<interact>" is the event wire format.
                 */
                return new Event(
                    _isPress(ev) ? Event.INPUT_BUTTON_PRESSED : Event.INPUT_BUTTON_RELEASED,
                    $"<{action}>");
            }
        }

        lock (_lo)
        {
            if (_mapButtonToLogical.TryGetValue(ev.ToKey(), out var codeLogical))
            {
                int seperatorPos = codeLogical.IndexOf(':');
                if (-1 != seperatorPos)
                {
                    var newEvent = new Event(
                        codeLogical.Substring(0, seperatorPos),
                        codeLogical.Substring(seperatorPos + 1, codeLogical.Length - seperatorPos - 1)
                    );
                    Trace($"Event {ev} mapped to logical event {newEvent}");
                    return newEvent;
                }
            }
        }

        return null;
    }


    public void TranslateEmitLogical(engine.news.Event ev)
    {
        Event? evLogical = ToLogical(ev);
        if (null != evLogical)
        {
            I.Get<EventQueue>().Push(evLogical);
        }
    }

    
    /**
     * Take the event passed to this method, emit both the event and its translation
     * to a logical button into the event pipeline.
     */
    public void EmitPlusTranslation(engine.news.Event ev)
    {
        if (null == _eq)
        {
            return;
        }

        _eq.Push(ev);
        Event? evLogical = ToLogical(ev);
        if (null != evLogical)
        {
            _eq.Push(evLogical);
        }
    }

    
    /**
     * The shipped default bindings, declared as a resource so they reach the APK and
     * the installer like any other asset.
     */
    public const string DefaultBindingsResource = "nogame.bindings.json";

    /**
     * Where a user's rebindings are written. Deliberately NOT the shipped file: the
     * player's overrides have to survive a game update, and the shipped file is
     * replaced by one.
     */
    public static string UserBindingsPath =>
        Path.Combine(engine.GlobalSettings.Get("Engine.RWPath") ?? ".", "bindings.json");


    /**
     * Shipped defaults first, then the user's file layered over them.
     *
     * The overlay is per-ACTION, not whole-file: an action the user never rebound keeps
     * the shipped default, so bindings added by a game update appear for existing
     * players instead of being masked by a stale user file. That is the entire reason
     * the two files are separate.
     */
    public void LoadBindings()
    {
        try
        {
            using var stream = engine.Assets.Open(DefaultBindingsResource);
            if (null != stream)
            {
                using var reader = new StreamReader(stream);
                int nSkipped = Bindings.FromJsonString(reader.ReadToEnd());
                if (nSkipped > 0)
                {
                    Warning($"{nSkipped} unparseable entries in {DefaultBindingsResource}.");
                }

                Trace(_dc, $"Loaded {Bindings.Actions.Count} action bindings from {DefaultBindingsResource}.");
            }
            else
            {
                /*
                 * An Error, not a Trace. Since the shipped config migrated its button
                 * bindings into this file, failing to open it means the game has NO
                 * button bindings at all - and the symptom, "the interact key does
                 * nothing", says nothing about the cause. Same reasoning as TaleModule
                 * logging an Error when it disables itself.
                 */
                Error(_dc, $"Could not open {DefaultBindingsResource}. Button bindings will be missing; "
                           + "check that it is declared in nogame.resources.json.");
            }
        }
        catch (Exception e)
        {
            Warning($"Unable to read {DefaultBindingsResource}: {e.Message}");
        }

        try
        {
            string userPath = UserBindingsPath;
            if (File.Exists(userPath))
            {
                var overlay = new BindingTable();
                overlay.FromJsonString(File.ReadAllText(userPath));
                foreach (var binding in overlay.Actions)
                {
                    Bindings.Set(binding);
                }

                Trace(_dc, $"Applied {overlay.Actions.Count} user bindings from {userPath}.");
            }
        }
        catch (Exception e)
        {
            Warning($"Unable to read user bindings: {e.Message}");
        }
    }


    /**
     * Persist the current table to the user's file. Called by a rebinding UI.
     */
    public bool SaveUserBindings()
    {
        try
        {
            string userPath = UserBindingsPath;
            string? dir = Path.GetDirectoryName(userPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(userPath, Bindings.ToJsonString());
            Trace(_dc, $"Wrote user bindings to {userPath}.");
            return true;
        }
        catch (Exception e)
        {
            Error(_dc, $"Unable to write user bindings: {e.Message}");
            return false;
        }
    }


    protected override void OnModuleActivate()
    {
        _eq = I.Get<EventQueue>();
        LoadBindings();
    }
}

