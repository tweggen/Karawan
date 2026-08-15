using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.controllers.bindings;
using engine;
using engine.news;
using static engine.Logger;

namespace builtin.controllers;


/**
 * Translate input events to the Game Controller data structure, providing a more
 * semantic input representation that can be polled.
 *
 * COnsumes input
 *
 * THE ANALOG PATH READS THE BINDING TABLE (WP-6.4 part 2)
 *
 * This class used to switch on raw control identity - the key string "w", stick index 0,
 * trigger index 1 - and apply its response curves as arithmetic written into the handler.
 * It now asks InputMapper.Bindings which ACTION a control drives and applies that action's
 * modifier pipeline, so both halves live in models/nogame.bindings.json:
 *
 *   walkforward/backward/left/right   were "w"/"s"/"a"/"d", now rebindable
 *   run                               was "(shiftleft)"
 *   move / look                       were stick indices 0 / 1, with StickTransfer's
 *                                     sign(x)*|x^4| inline; now "curve 4"
 *   brake / accelerate                were trigger indices 0 / 1, with (x+1)/2*255
 *                                     inline; now "range -1 1 0 255"
 *
 * There is deliberately NO hardcoded fallback: a second, unexercised copy of the defaults
 * is how a rebind ends up leaving the old control live. If the file is missing the game
 * does not move, which is loud - and OnModuleActivate names the missing actions so the
 * loudness is also informative.
 *
 * NOT migrated: the TOUCH path (TouchSteerTransfer*, the finger states). Those transfers
 * are scale factors tied to screen geometry rather than response curves on a bindable
 * control, and touch has nothing to rebind onto.
 */
public class InputController : engine.AController, engine.IInputPart
{
    private static readonly engine.Dc _dc = engine.Dc.Input;

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<InputEventPipeline>(),

        /*
         * For Bindings. A dependency rather than an I.Get at use time: AModule activates
         * dependencies BEFORE OnModuleActivate, which is what guarantees the table has
         * been loaded by the time the first event arrives.
         */
        new SharedModule<InputMapper>()
    };

    /*
     * The action ids this class reacts to. They are names in nogame.bindings.json; which
     * controls sit behind them is that file's business, which is the whole point.
     */
    private const string ActionWalkForward = "walkforward";
    private const string ActionWalkBackward = "walkbackward";
    private const string ActionWalkLeft = "walkleft";
    private const string ActionWalkRight = "walkright";
    private const string ActionRun = "run";
    private const string ActionMove = "move";
    private const string ActionLook = "look";
    private const string ActionBrake = "brake";
    private const string ActionAccelerate = "accelerate";

    private BindingTable? _bindings;

    private object _lo = new();

    /**
     * We only propagate the inp0ut evets to the common space if nobody else
     * in the pipeline already has captured it.
     */
    public float MY_Z_ORDER { get; set; } = 0f;
    public float TouchLookSensitivity { get; set; } = 12f;
    public float TouchMoveSensitivity { get; set; } = 4.0f;
    public float TouchPeakMoveSensitivity { get; set; } = 64f;
    public float MouseLookMoveSensitivity  { get; set; }= 1f;


    /*
     * These are scaled from y = [0...1] and x = [0...16/9] (1.777)
     */
    public float ControllerYMax { get; set; } = 0.2f;
    public float ControllerYTolerance { get; set; } = 0.01f;
    public float ControllerXMax { get; set; } = 0.2f;
    public float ControllerXTolerance { get; set; } = 0.01f;


    public int KeyboardAnalogWalk { get; set; } = 180;
    public int KeyboardAnalogMax { get; set; } = 255;
    public int TouchAnalogMax { get; set; } = 255;
    
    
    private Vector2 _v2ViewSize = Vector2.Zero;
    private Vector2 _v2MouseMove = Vector2.Zero;

    public Vector2 V2MouseMove
    {
        get
        {
            lock (_lo)
            {
                return _v2MouseMove;
            }
        }
        
        set
        {
            lock (_lo)
            {
                _v2MouseMove = value;
            }
        }
    }
    
    private Vector2 _v2RightTouchMove = Vector2.Zero;

    public Vector2 V2RightTouchMove
    {
        get
        {
            lock (_lo)
            {
                return _v2RightTouchMove;
            }
        }

        set
        {
            lock (_lo)
            {
                _v2RightTouchMove = value;
            }
        }
    }
        
    private Vector2 _v2StickOffset = Vector2.Zero;
    private Vector2 _v2MousePressPosition = Vector2.Zero;
    private Vector2 _v2CurrentMousePosition = Vector2.Zero;
    private bool _isMouseButtonClicked = false;
    private Vector2 _lastMousePosition;
    private bool _isKeyboardFast = false;
    
    private ControllerState _controllerState = new();

    public ControllerState ControllerState
    {
        get
        {
            lock (_lo)
            {
                return _controllerState;
            }
        }
        set
        {
            lock (_lo)
            {
                _controllerState = value;
            }
        }
    }

    
    /**
     * Which action, if any, this key event drives.
     *
     * Keyed on ev.ScanCode, never ev.Code: only the scancode is guaranteed to be the
     * physical POSITION, and a binding that means "the key left of S" must not turn into
     * "the key that prints a" on a French keyboard. See Control's doc-comment.
     */
    private string? _keyActionOf(Event ev)
    {
        var bindings = _bindings;
        if (null == bindings || engine.inputs.ScanCode.Unknown == ev.ScanCode)
        {
            return null;
        }

        return bindings.ActionOf(Control.Key(ev.ScanCode));
    }


    /**
     * The binding for an analog control, or null if nothing is bound to it.
     */
    private ActionBinding? _analogBindingOf(Control control)
    {
        var bindings = _bindings;
        if (null == bindings)
        {
            return null;
        }

        string? action = bindings.ActionOf(control);
        return null != action ? bindings.Find(action) : null;
    }


    private void _onKeyDown(Event ev)
    {
        string? action = _keyActionOf(ev);

        lock (_lo)
        {
            _controllerState.LastInput = DateTime.UtcNow;

            // TXWTODO: This is for driving mode only. Walking mode would have a different assignment.

            switch (action)
            {
                case ActionRun:
                    _isKeyboardFast = true;
                    break;
                case ActionWalkForward:
                    _controllerState.WASDUp = _isKeyboardFast?KeyboardAnalogMax:KeyboardAnalogWalk;
                    break;
                case ActionWalkBackward:
                    _controllerState.WASDDown = _isKeyboardFast?KeyboardAnalogMax:KeyboardAnalogWalk;
                    break;
                case ActionWalkLeft:
                    _controllerState.WASDLeft = KeyboardAnalogMax;
                    break;
                case ActionWalkRight:
                    _controllerState.WASDRight = KeyboardAnalogMax;
                    break;
                default:
                    break;
            }
        }
    }


    private void _onKeyUp(Event ev)
    {
        string? action = _keyActionOf(ev);

        lock (_lo)
        {
            _controllerState.LastInput = DateTime.UtcNow;

            switch (action)
            {
                case ActionRun:
                    _isKeyboardFast = false;
                    break;
                case ActionWalkForward:
                    _controllerState.WASDUp = 0;
                    break;
                case ActionWalkBackward:
                    _controllerState.WASDDown = 0;
                    break;
                case ActionWalkLeft:
                    _controllerState.WASDLeft = 0;
                    break;
                case ActionWalkRight:
                    _controllerState.WASDRight = 0;
                    break;
                default:
                    break;
            }
        }
    }


    
    /**
     * Respond to a move, press position is relative view size (anamorphic),
     * vRel is movement (relative to viewY resolution)
     *
     * NOT ON MOBILE!!!!
     */
    private void _handleTouchMove(Vector2 vPress, Vector2 vRel)
    {
        lock (_lo)
        {
            /*
             * Pressed in the left half of the screen?
             */
            if (vPress.X <= 0.5)
            {
                _controllerState.LastInput = DateTime.UtcNow;

                if (vRel.Y < -ControllerYTolerance)
                {
                    /*
                     * The user dragged up compare to the press position
                     */
                    _controllerState.TouchLeftStickUp = (int)(Single.Min(ControllerYMax, -vRel.Y-ControllerYTolerance)
                        / ControllerYMax * TouchAnalogMax);
                    _controllerState.TouchLeftStickDown = 0;
                }
                else if (vRel.Y > ControllerYTolerance)
                {
                    /*
                     * The user dragged down compared to the press position.
                     */
                    _controllerState.TouchLeftStickDown = (int)(Single.Min(ControllerYMax, vRel.Y-ControllerYTolerance) 
                        / ControllerYMax * TouchAnalogMax);
                    _controllerState.TouchLeftStickUp = 0;
                }

                if (vRel.X < -ControllerXTolerance)
                {
                    _controllerState.TouchLeftStickLeft = (int)(Single.Min(ControllerXMax, -vRel.X-ControllerXTolerance) 
                        / ControllerXMax * TouchAnalogMax);
                    _controllerState.TouchLeftStickRight = 0;
                }
                else if (vRel.X > ControllerXTolerance)
                {
                    _controllerState.TouchLeftStickRight = (int)(Single.Min(ControllerXMax, vRel.X-ControllerXTolerance) 
                        / ControllerXMax * TouchAnalogMax);
                    _controllerState.TouchLeftStickLeft = 0;
                }
           }
            else
            {
                var viewSize = _v2ViewSize;
                if (_lastTouchPosition == default)
                {
                    _lastTouchPosition = _v2CurrentMousePosition;
                }

                V2MouseMove += ((_v2CurrentMousePosition - _lastTouchPosition) / viewSize.Y) * 900f *
                               TouchMoveSensitivity;
            }
        }
    }
    
    
    /*
     * Vars to implement zoom emulation on touch
     */
    private Vector2 _lastTouchPosition = default;
    
    /**
     * How far on the y axis do I need to move to do a complete zoom controller?
     */
    public float ControllerTouchZoomFull { get; set; } = 1.0f;
    private float _zoomAtPress = 0f;
    
    /*
     * Vars to emulate debug button on touch
     *
     * We have to click alteratingly into the right and left half of the screen
     * quickly to enable debug display.
     */
    private int _enableDebugCounter = 0;
    private readonly int _maxDebugCounter = 5;
    private float _enableDebugYAbove = 0.9f;
    private DateTime _enableDebugStartTime = default;
    
    /**
     * Besides reading the standard touch movements, the touch controller also implements
     * a mouse wheel controller on the right hand side of the screen.
     */
    private void _touchMouseController()
    {
        lock (_lo)
        {
            if (_isMouseButtonClicked)
            {
                Vector2 currDist = _v2CurrentMousePosition - _v2MousePressPosition;
                var viewSize = _v2ViewSize;

                /*
                 * Compute movement relative to view height, 
                 */
                float relY = (float)currDist.Y / (float)viewSize.Y;
                float relX = (float)currDist.X / (float)viewSize.Y;
                
                if (_v2MousePressPosition.X >= (viewSize.X - viewSize.X/25f))
                {
#if false
                    float zoomWay = relY / ControllerTouchZoomFull * (8);
                    
                    I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_MOUSE_WHEEL, "(zoom)")
                    {
                        Position = new Vector2(0f, zoomWay)
                    });
#else
                    var v2Moved = (_v2CurrentMousePosition - _lastTouchPosition) / (float)viewSize.Y;
                    float virtualWheelY = v2Moved.Y * 20f;

                    I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_MOUSE_WHEEL, "(zoom)")
                    {
                        PhysicalPosition = new Vector2(0f, virtualWheelY)
                    });
#endif
                }
                else
                {
                    _handleTouchMove(
                        new Vector2(
                            _v2MousePressPosition.X / viewSize.X, 
                            _v2MousePressPosition.Y / viewSize.Y),
                        new Vector2(relX, relY));

                }
                _lastTouchPosition = _v2CurrentMousePosition;
                
            }
            else
            {
                /*
                 * on any release, reset all controller movements.
                 */
                _controllerState.TouchLeftStickUp = 0;
                _controllerState.TouchLeftStickDown = 0;
                _controllerState.TouchLeftStickRight = 0;
                _controllerState.TouchLeftStickLeft = 0;

                _lastTouchPosition = default;
            }
        }
    }


    private void _desktopMouseController()
    {
        lock (_lo)
        {
            if (!_isMouseButtonClicked)
            {
                if (_lastMousePosition == default)
                {
                }
                else
                {
                    var xOffset = (_v2CurrentMousePosition.X - _lastMousePosition.X) * MouseLookMoveSensitivity;
                    var yOffset = (_v2CurrentMousePosition.Y - _lastMousePosition.Y) * MouseLookMoveSensitivity;
                    V2MouseMove += new Vector2(xOffset, yOffset);
                }
                _lastMousePosition = _v2CurrentMousePosition;
            }
        }
    }

    
    private void _handleMouseReleased(Event ev)
    {
        if (ev.Data1 != 0)
        {
            return;
        }
        
        string? strButton = _codeToMouseButton(ev.Code);
        
        lock (_lo)
        {
            _v2CurrentMousePosition = ev.PhysicalPosition;
            _isMouseButtonClicked = false;
            if (strButton != null)
            {
                Trace(_dc, $"Sending {strButton} released event");
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_BUTTON_RELEASED, strButton));
                ev.IsHandled = true;
            }
        }
    }


    private void _touchCheckDebugClick(Event ev)
    {
        DateTime now = DateTime.Now;
        bool doEmitDebug = false;
        
        lock (_lo)
        {
            var sinceFirst = _enableDebugStartTime - now;
            bool doResetDebug = false;

            if (ev.PhysicalPosition.Y / _v2ViewSize.Y < _enableDebugYAbove)
            {
                doResetDebug = true;
            }
            else
            {
                /*
                 * Is this a first click?
                 */
                if (sinceFirst > TimeSpan.FromMilliseconds(2000))
                {
                    if (ev.PhysicalPosition.X > _v2ViewSize.X / 2f)
                    {
                        _enableDebugStartTime = now;
                        _enableDebugCounter = 1;
                    }
                }
                else
                {
                    /*
                     * So this could be a continued click?
                     */
                    bool expectOnLeft = (_enableDebugCounter & 1) != 0;
                    if (expectOnLeft && ev.PhysicalPosition.X <= _v2ViewSize.X / 2f
                        || !expectOnLeft && ev.PhysicalPosition.X >= _v2ViewSize.X / 2f)
                    {
                        /*
                         * This is inside the correct side of the screen.
                         */
                        _enableDebugCounter++;
                        if (_enableDebugCounter == _maxDebugCounter)
                        {
                            doEmitDebug = true;
                        }
                    }
                    else
                    {
                        /*
                         * Wrong side, so reset.
                         */
                        doResetDebug = true;
                    }
                }
            }

            if (doResetDebug)
            {
                _enableDebugCounter = 0;
                _enableDebugStartTime = default;
            }
        }

        if (doEmitDebug)
        {
            Trace("Emitting debug key \"(escape)\".");
            I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_PRESSED, "(escape)"));
            I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_RELEASED, "(escape)"));
        }
    }


    private string? _codeToMouseButton(string code)
    {
        string? strButton;
        switch (code)
        {
            case "0":
                strButton = "<fire>";
                break;
            case "1":
                strButton = "rmb";
                break;
            case "2":
                strButton = "mmb";
                break;
            default:
                strButton = null;
                break;
        }

        return strButton;
    }
    

    private void _handleMousePressed(Event ev)
    {
        if (ev.Data1 != 0)
        {
            return;
        }

        string? strButton = _codeToMouseButton(ev.Code);
        
        lock (_lo)
        {
            _v2MousePressPosition = ev.PhysicalPosition;
            _v2CurrentMousePosition = ev.PhysicalPosition;
            _isMouseButtonClicked = true;

            _lastMousePosition = ev.PhysicalPosition;
            _lastTouchPosition = ev.PhysicalPosition;
            if (strButton != null)
            {
                Trace(_dc, $"Sending {strButton} pressed event");
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_BUTTON_PRESSED, strButton));
                ev.IsHandled = true;
            }
        }

    }


    private void _handleMouseMoved(Event ev)
    {
        lock (_lo)
        {
            _v2CurrentMousePosition = ev.PhysicalPosition;
        }
    }


    private void _refreshViewSize()
    {
        string viewSize = engine.GlobalSettings.Get("view.size");

        lock (_lo)
        {
            _v2ViewSize = engine.GlobalSettings.ParseSize(viewSize);
        }
    }
    

    private void _onViewSizeChanged(Event ev)
    {
        _refreshViewSize();
    }

    
    protected override void OnLogicalFrame(object? sender, float dt)
    {
        if (engine.GlobalSettings.Get("splash.touchControls") == "false")
        {
            _desktopMouseController();
        }
    }

    
    public float TouchSteerTransferX(float X)
    {
        return Single.Clamp(Single.Sign(X)*Single.Abs(X) / 9f, -1f, 1f);
    }
    

    public float TouchSteerTransferY(float X)
    {
        return Single.Clamp(Single.Sign(X) * Single.Abs(X) / 6f, -1f, 1f);
    }
    

    public Vector2 TouchSteerTransfer(Vector2 v)
    {
        return new Vector2(TouchSteerTransferX(v.X), TouchSteerTransferY(v.Y));
    }


    public float TouchViewTransfer(float X)
    {
        return Single.Clamp(Single.Sign(X) * Single.Abs(X * X), -1f, 1f);
    }
    

    public Vector2 TouchViewTransfer(Vector2 v)
    {
        return new Vector2(TouchViewTransfer(v.X), TouchViewTransfer(v.Y));
    }


    /*
     * StickTransfer - sign(X) * |X^4| - used to live here and is gone. It is now
     * "curve 4" on the "move" and "look" actions in models/nogame.bindings.json, which is
     * the same function expressed where it can be seen and changed. Keeping a copy of it
     * on this class would make the response curve two things that must be edited
     * together, and only one of them would be.
     *
     * TouchSteerTransfer* above stay: those are screen-geometry scale factors on a path
     * with no bindable control behind it.
     */


    public void GetStickOffset(out Vector2 vStickOffset)
    {
        lock (_lo)
        {
            vStickOffset = _v2StickOffset;
        }
    }
    

    public void GetMouseMove(out Vector2 vMouseMove)
    {            
        lock (_lo)
        {
            vMouseMove = V2MouseMove;
            V2MouseMove = new Vector2(0f, 0f);
        }
    }


    public void GetRightTouchMove(out Vector2 vRightTouchMove)
    {
        lock (_lo)
        {
            vRightTouchMove = V2RightTouchMove;
            V2RightTouchMove = new Vector2(0f, 0f);
        }
    }


    public void GetControllerState(out ControllerState controllerState)
    {
        lock (_lo)
        {
            controllerState = _controllerState;
        }
    }


    private FingerStateHandler _fingerStateHandler;

    
    public void _onStickMoved(Event ev)
    {
        ActionBinding? binding = _analogBindingOf(Control.GamepadStick((int)ev.Data1));
        if (null == binding)
        {
            return;
        }

        /*
         * Per component, because a stick's two axes are one control (see
         * ControlKind.GamepadStick) but a curve is a scalar transform.
         *
         * The branches below then test the MODIFIED value, where the old code tested the
         * raw one. Equivalent for the curve it used to hardcode - sign(x)*|x^4| preserves
         * sign - but an "invert" modifier does not, and branching on the raw value would
         * put the magnitude in the wrong accumulator. Invert exists here precisely
         * because WP-3.1 got a stick axis backwards.
         */
        Vector2 pos = new(
            InputModifiers.Apply(binding.Modifiers, ev.PhysicalPosition.X),
            InputModifiers.Apply(binding.Modifiers, ev.PhysicalPosition.Y));

        switch (binding.Action)
        {
            case ActionMove:
                lock (_lo)
                {
                    if (pos.X > 0)
                    {
                        _controllerState.AnalogLeftStickRight = (int)(pos.X * 255f);
                        _controllerState.AnalogLeftStickLeft = 0;
                    }
                    else
                    {
                        _controllerState.AnalogLeftStickRight = 0;
                        _controllerState.AnalogLeftStickLeft = -(int)(pos.X * 255f);
                    }

                    if (pos.Y > 0)
                    {
                        _controllerState.AnalogLeftStickUp = (int)(pos.Y * 255f);
                        _controllerState.AnalogLeftStickDown = 0;
                    }
                    else
                    {
                        _controllerState.AnalogLeftStickUp = 0;
                        _controllerState.AnalogLeftStickDown = -(int)(pos.Y * 255f);
                    }
                }

                break;

            case ActionLook:
                /*
                 * This is for viewing or zooming
                 */
                lock (_lo)
                {
                    if (_isGamepadRightStickPressed)
                    {
                        float zoomWay = -pos.Y;
                        I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_MOUSE_WHEEL, "(zoom)")
                        {
                            PhysicalPosition = new Vector2(0f, zoomWay)
                        });
                    }
                    else
                    {
                        /*
                         * Viewing.
                         */
                        _v2StickOffset = pos;
                    }

                }

                break;
            default:
                break;
        }
    }


    public void _onButtonPressed(Event ev)
    {
        Trace(_dc, $"Button {ev.Code} pressed");

        switch (ev.Code)
        {
            case "DPadDown":
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_PRESSED, "(cursordown)"));
                break;
            case "DPadUp":
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_PRESSED, "(cursorup)"));
                break;
            case "DPadLeft":
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_PRESSED, "(cursorleft)"));
                break;
            case "DPadRight":
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_PRESSED, "(cursorright)"));
                break;
            default:
                break;
        }
    }
    

    public void _onButtonReleased(Event ev)
    {
        Trace(_dc, $"Button {ev.Code} released");
        
        switch (ev.Code)
        {
            case "DPadDown":
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_RELEASED, "(cursordown)"));
                break;
            case "DPadUp":
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_RELEASED, "(cursorup)"));
                break;
            case "DPadLeft":
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_RELEASED, "(cursorleft)"));
                break;
            case "DPadRight":
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_RELEASED, "(cursorright)"));
                break;
            default:
                break;
        }
    }
    
    
    private bool _isGamepadRightStickPressed = false;
    
    public void _onGamepadButtonPressed(Event ev)
    {
        Trace(_dc, $"Button {ev.Code} pressed");

        switch (ev.Code)
        {
            case "RightStick":
                _isGamepadRightStickPressed = true;
                break;
            default:
                break;
        }
    }
    

    public void _onGamepadButtonReleased(Event ev)
    {
        Trace(_dc, $"Button {ev.Code} released");
        
        switch (ev.Code)
        {
            case "RightStick":
                _isGamepadRightStickPressed = false;
                break;
            default:
                break;
        }
    }
    
    
    public void _onTriggerMoved(Event ev)
    {
        ActionBinding? binding = _analogBindingOf(Control.GamepadTrigger((int)ev.Data1));
        if (null == binding)
        {
            return;
        }

        /*
         * "range -1 1 0 255" in the binding file, replacing 255f * (x+1f)/2f here. Same
         * endpoints - a released trigger is -1 and still lands on 0 - which is the
         * property that matters, since a trigger reading 127 at rest would brake the car
         * for as long as the pad is connected (Sdl3GamepadCodes.TriggerAxisToEngine).
         */
        int value = (int)InputModifiers.Apply(binding.Modifiers, ev.PhysicalPosition.X);

        lock (_lo)
        {
            switch (binding.Action)
            {
                case ActionBrake:
                    _controllerState.AnalogLeft2 = value;
                    break;
                case ActionAccelerate:
                    _controllerState.AnalogRight2 = value;
                    break;
                default:
                    break;
            }
        }
    }
    
    
    public void InputPartOnInputEvent(Event ev)
    {
        if (engine.GlobalSettings.Get("splash.touchControls") == "false")
        {
            /*
             * When there is no touch controls, we do not track any click events
             * as virtual sticks or alike. 
             */
            if (true)
            {
                if (ev.Type.StartsWith(Event.INPUT_MOUSE_PRESSED)) _handleMousePressed(ev);
                if (ev.Type.StartsWith(Event.INPUT_MOUSE_RELEASED)) _handleMouseReleased(ev);
                if (ev.Type.StartsWith(Event.INPUT_MOUSE_MOVED)) _handleMouseMoved(ev);
            }
            else
            {
                if (ev.Type.StartsWith(Event.INPUT_MOUSE_PRESSED)) _fingerStateHandler.OnFingerPressed(ev);
                if (ev.Type.StartsWith(Event.INPUT_MOUSE_RELEASED)) _fingerStateHandler.OnFingerReleased(ev);
                if (ev.Type.StartsWith(Event.INPUT_MOUSE_MOVED)) _fingerStateHandler.OnFingerMotion(ev);
        
            }
        }

        if (ev.Type.StartsWith(Event.INPUT_KEY_PRESSED)) _onKeyDown(ev);
        if (ev.Type.StartsWith(Event.INPUT_KEY_RELEASED)) _onKeyUp(ev);

        if (ev.Type.StartsWith(Event.INPUT_FINGER_PRESSED)) _fingerStateHandler.OnFingerPressed(ev);
        if (ev.Type.StartsWith(Event.INPUT_FINGER_RELEASED)) _fingerStateHandler.OnFingerReleased(ev);
        if (ev.Type.StartsWith(Event.INPUT_FINGER_MOVED)) _fingerStateHandler.OnFingerMotion(ev);
        
        if (ev.Type.StartsWith(Event.INPUT_GAMEPAD_STICK_MOVED)) _onStickMoved(ev);
        if (ev.Type.StartsWith(Event.INPUT_GAMEPAD_TRIGGER_MOVED)) _onTriggerMoved(ev);
        if (ev.Type.StartsWith(Event.INPUT_GAMEPAD_BUTTON_PRESSED)) _onGamepadButtonPressed(ev);
        if (ev.Type.StartsWith(Event.INPUT_GAMEPAD_BUTTON_RELEASED)) _onGamepadButtonReleased(ev);
        if (ev.Type.StartsWith(Event.INPUT_BUTTON_PRESSED)) _onButtonPressed(ev);
        if (ev.Type.StartsWith(Event.INPUT_BUTTON_RELEASED)) _onButtonReleased(ev);
    }

    
    protected override void OnModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);
        _fingerStateHandler = null;
    }


    protected override void OnModuleActivate()
    {
        _fingerStateHandler = new(ev =>
            {
                if (ev.PhysicalPosition.X < 0.5f)
                {
                    return new LeftStickFingerState(ev.PhysicalPosition, this);
                }
                else if (ev.PhysicalPosition.X < 0.9f)
                {
                    return new RightStickFingerState(ev.PhysicalPosition, this);
                }
                else
                {
                    return new ZoomStickFingerState(ev.PhysicalPosition, this);
                }
            }
        );
        _bindings = M<InputMapper>().Bindings;
        _checkRequiredActions();

        Subscribe(Event.VIEW_SIZE_CHANGED, _onViewSizeChanged);
        M<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        _refreshViewSize();
    }


    /**
     * Every action this class reads. Nothing else in the game does, so an action missing
     * from the binding file shows up as a dead control and nothing else.
     *
     * Internal rather than private so ShippedBindingsTests can assert the shipped file
     * binds all of them. That check has to be driven from THIS list, not a copy of it, or
     * adding a tenth action here would leave the test still passing on nine.
     */
    internal static readonly string[] RequiredActions =
    {
        ActionWalkForward, ActionWalkBackward, ActionWalkLeft, ActionWalkRight,
        ActionRun, ActionMove, ActionLook, ActionBrake, ActionAccelerate
    };


    /**
     * Say at startup which movement actions are unbound.
     *
     * The analog path has no hardcoded fallback (see the class comment), so a binding file
     * that is missing or stale means the player cannot move. That failure is loud but
     * mute: "the game does not respond to WASD" says nothing about its cause, and the
     * cause is one line of JSON. Naming it here is the same trade as InputMapper logging
     * an Error rather than a Trace when it cannot open the file at all.
     */
    private void _checkRequiredActions()
    {
        var bindings = _bindings;
        if (null == bindings)
        {
            return;
        }

        var missing = new List<string>();
        foreach (var action in RequiredActions)
        {
            var binding = bindings.Find(action);
            if (null == binding || 0 == binding.Controls.Count)
            {
                missing.Add(action);
            }
        }

        if (missing.Count > 0)
        {
            Error(_dc, $"No control is bound to: {String.Join(", ", missing)}. Check {InputMapper.DefaultBindingsResource}; those inputs will do nothing.");
        }
    }
}