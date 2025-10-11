using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.news;
using static engine.Logger;

namespace builtin.controllers;


/**
 * Translate input events to the Game Controller data structure, providing a more
 * semantic input representation that can be polled.
 *
 * COnsumes input 
 */
public class InputController : engine.AController, engine.IInputPart
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<InputEventPipeline>()    
    };

    private object _lo = new();

    public float MY_Z_ORDER { get; set; } = 0f;
    public float TouchLookSensitivity { get; set; } = 12f;
    public float TouchMoveSensitivity { get; set; } = 1.4f;
    public float TouchPeakMoveSensitivity { get; set; } = 4f * 8f;
    public float MouseLookMoveSensitivity  { get; set; }= 1f;


    public float ControllerYMax { get; set; } = 0.2f;
    public float ControllerYTolerance { get; set; } = 0.008f;
    public float ControllerXMax { get; set; } = 0.2f;
    public float ControllerXTolerance { get; set; } = 0.0020f;


    public int KeyboardAnalogWalk { get; set; } = 180;
    public int KeyboardAnalogMax { get; set; } = 255;
    public int TouchAnalogMax { get; set; } = 255;
    
    
    private Vector2 _vViewSize = Vector2.Zero;
    public Vector2 VMouseMove = Vector2.Zero;
    private Vector2 _vStickOffset = Vector2.Zero;
    private Vector2 _mousePressPosition = Vector2.Zero;
    private Vector2 _currentMousePosition = Vector2.Zero;
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

    
    private void _onKeyDown(Event ev)
    {
        lock (_lo)
        {
            _controllerState.LastInput = DateTime.UtcNow;

            // TXWTODO: This is for driving mode only. Walking mode would have a different assignment.
            
            switch (ev.Code)
            {
                case "(shiftleft)":
                    _isKeyboardFast = true;
                    break;
                case "w":
                    _controllerState.WASDUp = _isKeyboardFast?KeyboardAnalogMax:KeyboardAnalogWalk;
                    break;
                case "s":
                    _controllerState.WASDDown = _isKeyboardFast?KeyboardAnalogMax:KeyboardAnalogWalk;
                    break;
                case "a":
                    _controllerState.WASDLeft = KeyboardAnalogMax;
                    break;
                case "d":
                    _controllerState.WASDRight = KeyboardAnalogMax;
                    break;
                case "e":
                    
                    break;
                default:
                    break;
            }
            _controllerState.AnalogToWalkControllerNoLock();
        }
    }
    

    private void _onKeyUp(Event ev)
    {
        lock (_lo)
        {
            _controllerState.LastInput = DateTime.UtcNow;

            switch (ev.Code)
            {
                case "(shiftleft)":
                    _isKeyboardFast = false;
                    break;
                case "w":
                    _controllerState.WASDUp = 0;
                    break;
                case "s":
                    _controllerState.WASDDown = 0;
                    break;
                case "a":
                    _controllerState.WASDLeft = 0;
                    break;
                case "d":
                    _controllerState.WASDRight = 0;
                    break;
                default:
                    break;
            }

            _controllerState.AnalogToWalkControllerNoLock();
        }
    }


    /**
     * Respond to a move, press position is relative view size (anamorphic),
     * vRel is movement (relative to viewY resolution)
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
                
                _controllerState.AnalogToWalkControllerNoLock();

            }
            else
            {
                var viewSize = _vViewSize;
                if (_lastTouchPosition == default)
                {
                    _lastTouchPosition = _currentMousePosition;
                }

                VMouseMove += ((_currentMousePosition - _lastTouchPosition) / viewSize.Y) * 900f *
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
                Vector2 currDist = _currentMousePosition - _mousePressPosition;
                var viewSize = _vViewSize;

                /*
                 * Compute movement relative to view height, 
                 */
                float relY = (float)currDist.Y / (float)viewSize.Y;
                float relX = (float)currDist.X / (float)viewSize.Y;
                
                if (_mousePressPosition.X >= (viewSize.X - viewSize.X/25f))
                {
#if false
                    float zoomWay = relY / ControllerTouchZoomFull * (8);
                    
                    I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_MOUSE_WHEEL, "(zoom)")
                    {
                        Position = new Vector2(0f, zoomWay)
                    });
#else
                    var v2Moved = (_currentMousePosition - _lastTouchPosition) / (float)viewSize.Y;
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
                            _mousePressPosition.X / viewSize.X, 
                            _mousePressPosition.Y / viewSize.Y),
                        new Vector2(relX, relY));

                }
                _lastTouchPosition = _currentMousePosition;
                
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
                
                _controllerState.AnalogToWalkControllerNoLock();

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
                    var xOffset = (_currentMousePosition.X - _lastMousePosition.X) * MouseLookMoveSensitivity;
                    var yOffset = (_currentMousePosition.Y - _lastMousePosition.Y) * MouseLookMoveSensitivity;
                    VMouseMove += new Vector2(xOffset, yOffset);
                }
                _lastMousePosition = _currentMousePosition;
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
            _currentMousePosition = ev.PhysicalPosition;
            _isMouseButtonClicked = false;
            if (strButton != null)
            {
                Trace($"Sending {strButton} released event");
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

            if (ev.PhysicalPosition.Y / _vViewSize.Y < _enableDebugYAbove)
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
                    if (ev.PhysicalPosition.X > _vViewSize.X / 2f)
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
                    if (expectOnLeft && ev.PhysicalPosition.X <= _vViewSize.X / 2f
                        || !expectOnLeft && ev.PhysicalPosition.X >= _vViewSize.X / 2f)
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
            _mousePressPosition = ev.PhysicalPosition;
            _currentMousePosition = ev.PhysicalPosition;
            _isMouseButtonClicked = true;

            _lastMousePosition = ev.PhysicalPosition;
            _lastTouchPosition = ev.PhysicalPosition;
            if (strButton != null)
            {
                Trace($"Sending {strButton} pressed event");
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_BUTTON_PRESSED, strButton));
                ev.IsHandled = true;
            }
        }

    }


    private void _handleMouseMoved(Event ev)
    {
        lock (_lo)
        {
            _currentMousePosition = ev.PhysicalPosition;
        }
    }


    private void _refreshViewSize()
    {
        string viewSize = engine.GlobalSettings.Get("view.size");

        lock (_lo)
        {
            _vViewSize = engine.GlobalSettings.ParseSize(viewSize);
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


    public float StickTransfer(float X)
    {
        return Single.Sign(X) * Single.Abs(X * X * X * X);
    }


    public Vector2 StickTransfer(Vector2 v)
    {
        return new Vector2(StickTransfer(v.X), StickTransfer(v.Y));
    }


    public void GetStickOffset(out Vector2 vStickOffset)
    {
        lock (_lo)
        {
            vStickOffset = _vStickOffset;
        }
    }
    

    public void GetMouseMove(out Vector2 vMouseMove)
    {            
        lock (_lo)
        {
            vMouseMove = VMouseMove;
            VMouseMove = new Vector2(0f, 0f);
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
        Vector2 pos = StickTransfer(ev.PhysicalPosition);
        switch (ev.Data1)
        {
            case 0:
                /*
                 * Left stick
                 */
                lock (_lo)
                {
                    if (ev.PhysicalPosition.X > 0)
                    {
                        _controllerState.AnalogLeftStickRight = (int)(pos.X * 255f);
                        _controllerState.AnalogLeftStickLeft = 0;
                    }
                    else
                    {
                        _controllerState.AnalogLeftStickRight = 0;
                        _controllerState.AnalogLeftStickLeft = -(int)(pos.X * 255f);
                    }

                    if (ev.PhysicalPosition.Y > 0)
                    {
                        _controllerState.AnalogLeftStickUp = (int)(pos.Y * 255f);
                        _controllerState.AnalogLeftStickDown = 0;
                    }
                    else
                    {
                        _controllerState.AnalogLeftStickUp = 0;
                        _controllerState.AnalogLeftStickDown = -(int)(pos.Y * 255f);
                    }

                    _controllerState.AnalogToWalkControllerNoLock();
                }

                break;
            
            case 1:
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
                        _vStickOffset = pos;
                    }

                }

                break;
            default:
                break;
        }
    }


    public void _onButtonPressed(Event ev)
    {
        Trace($"Button {ev.Code} pressed");

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
        Trace($"Button {ev.Code} released");
        
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
        Trace($"Button {ev.Code} pressed");

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
        Trace($"Button {ev.Code} released");
        
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
        switch (ev.Data1)
        {
            case 0:
                /*
                 * This is braking
                 */
                lock (_lo)
                {
                    _controllerState.AnalogLeft2 = (int)(255f * (ev.PhysicalPosition.X+1f)/2f);
                    _controllerState.AnalogToWalkControllerNoLock();
                }

                break;
            
            case 1:
                /*
                 * This is for accelerating
                 */
                lock (_lo)
                {
                    _controllerState.AnalogRight2 = (int)(255f * (ev.PhysicalPosition.X+1f)/2f);
                    _controllerState.AnalogToWalkControllerNoLock();
                }

                break;
            default:
                break;
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
        I.Get<SubscriptionManager>().Unsubscribe(Event.VIEW_SIZE_CHANGED, _onViewSizeChanged);
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
        I.Get<SubscriptionManager>().Subscribe(Event.VIEW_SIZE_CHANGED, _onViewSizeChanged);
        M<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        _refreshViewSize();
    }
}