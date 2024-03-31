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
public class InputController : engine.AModule, engine.IInputPart
{
    private object _lo = new();
    
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

    
    public float TouchLookSensitivity { get; set; } = 12f;
    public float TouchMoveSensitivity { get; set; } = 1.4f;
    public float TouchPeakMoveSensitivity { get; set; } = 4f * 8f;
    public float MouseLookMoveSensitivity  { get; set; }= 1f;


    public float ControllerWalkForwardNormal { get; set; } = 184f;
    public float ControllerWalkBackwardNormal { get; set; } = 184f;
    public float ControllerWalkForwardFast { get; set; } = 255f;
    public float ControllerWalkBackwardFast { get; set; } = 255f;
    public float ControllerFlyUpNormal { get; set; } = 255f;
    public float ControllerFlyDownNormal { get; set; } = 255f;
    public float ControllerTurnLeftRight { get; set; } = 255f;


    public float ControllerYMax { get; set; } = 0.2f;
    public float ControllerYTolerance { get; set; } = 0.008f;
    public float ControllerXMax { get; set; } = 0.2f;
    public float ControllerXTolerance { get; set; } = 0.008f;


    public int KeyboardAnalogMax { get; set; } = 255;
    public int TouchAnalogMax { get; set; } = 255;

    private void _toggleSpeedNoLock()
    {
        _isKeyboardFast = !_isKeyboardFast;
    }


    private void _onKeyDown(Event ev)
    {
        lock (_lo)
        {
            _controllerState.LastInput = DateTime.UtcNow;

            switch (ev.Code)
            {
                case "(shiftleft)":
                    _toggleSpeedNoLock();
                    break;
                case "Q":
                    _controllerState.AnalogUp = KeyboardAnalogMax; 
                    _controllerState.FlyUp = (int)ControllerFlyUpNormal;
                    break;
                case "Z":
                    _controllerState.AnalogDown = KeyboardAnalogMax;
                    _controllerState.FlyDown = (int)ControllerFlyDownNormal;
                    break;
                case "W":
                    _controllerState.AnalogForward = KeyboardAnalogMax;
                    _controllerState.WalkForward = _isKeyboardFast?(int)ControllerWalkForwardFast:(int)ControllerWalkForwardNormal;
                    break;
                case "S":
                    _controllerState.AnalogBackward = KeyboardAnalogMax;
                    _controllerState.WalkBackward = _isKeyboardFast?(int)ControllerWalkBackwardFast:(int)ControllerWalkBackwardNormal;
                    break;
                case "A":
                    _controllerState.AnalogLeft = KeyboardAnalogMax;
                    _controllerState.TurnLeft = (int)ControllerTurnLeftRight;
                    break;
                case "D":
                    _controllerState.AnalogRight = KeyboardAnalogMax;
                    _controllerState.TurnRight = (int)ControllerTurnLeftRight;
                    break;
                default:
                    break;
            }
        }
    }
    

    private void _onKeyUp(Event ev)
    {
        lock (_lo)
        {
            _controllerState.LastInput = DateTime.UtcNow;

            switch (ev.Code)
            {
                case "Q":
                    _controllerState.AnalogUp = 0;
                    break;
                case "Z":
                    _controllerState.AnalogDown = 0;
                    break;
                case "W":
                    _controllerState.AnalogForward = 0;
                    break;
                case "S":
                    _controllerState.AnalogBackward = 0;
                    break;
                case "A":
                    _controllerState.AnalogLeft = 0;
                    break;
                case "D":
                    _controllerState.AnalogRight = 0;
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
                    _controllerState.AnalogForward = (int)(Single.Min(ControllerYMax, -vRel.Y-ControllerYTolerance)
                        / ControllerYMax * TouchAnalogMax);
                    _controllerState.AnalogBackward = 0;
                }
                else if (vRel.Y > ControllerYTolerance)
                {
                    /*
                     * The user dragged down compared to the press position.
                     */
                    _controllerState.AnalogBackward = (int)(Single.Min(ControllerYMax, vRel.Y-ControllerYTolerance) 
                        / ControllerYMax * TouchAnalogMax);
                    _controllerState.AnalogForward = 0;
                }

                if (vRel.X < -ControllerXTolerance)
                {
                    _controllerState.AnalogLeft = (int)(Single.Min(ControllerXMax, -vRel.X-ControllerXTolerance) 
                        / ControllerXMax * TouchAnalogMax);
                    _controllerState.AnalogRight = 0;
                }
                else if (vRel.X > ControllerXTolerance)
                {
                    _controllerState.AnalogRight = (int)(Single.Min(ControllerXMax, vRel.X-ControllerXTolerance) 
                        / ControllerXMax * TouchAnalogMax);
                    _controllerState.AnalogLeft = 0;
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
     * a zoom controller (mouse wheel) on the right hand side of the screen.
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

                //Console.WriteLine($"ViewSize: {_vViewSize}, press: {_mousePressPosition}, relX: {relX}, relY: {relY}");

                if (_mousePressPosition.X >= (viewSize.X - viewSize.X/25f))
                {
                    float zoomWay = relY / ControllerTouchZoomFull * (8);
                    
                    I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_MOUSE_WHEEL, "(zoom)")
                    {
                        Position = new Vector2(0f, zoomWay)
                    });
                }
                else
                {
                    _handleTouchMove(
                        new Vector2(
                            _mousePressPosition.X / viewSize.X, 
                            _mousePressPosition.Y / viewSize.Y),
                        new Vector2(relX, relY));

                    _lastTouchPosition = _currentMousePosition;
                }
                
            }
            else
            {
                /*
                 * on any release, reset all controller movements.
                 */
                _controllerState.AnalogForward = 0;
                _controllerState.AnalogBackward = 0;
                _controllerState.AnalogRight = 0;
                _controllerState.AnalogLeft = 0;
                _controllerState.AnalogUp = 0;
                _controllerState.AnalogDown = 0;
                
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

        lock (_lo)
        {
            _currentMousePosition = ev.Position;
            _isMouseButtonClicked = false;
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

            if (ev.Position.Y / _vViewSize.Y < _enableDebugYAbove)
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
                    if (ev.Position.X > _vViewSize.X / 2f)
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
                    if (expectOnLeft && ev.Position.X <= _vViewSize.X / 2f
                        || !expectOnLeft && ev.Position.X >= _vViewSize.X / 2f)
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
    

    private void _handleMousePressed(Event ev)
    {
        if (ev.Data1 != 0)
        {
            return;
        }

        lock (_lo)
        {
            _mousePressPosition = ev.Position;
            _currentMousePosition = ev.Position;
            _isMouseButtonClicked = true;
        }

    }


    private void _handleMouseMoved(Event ev)
    {
        lock (_lo)
        {
            _currentMousePosition = ev.Position;
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

    
    private void _onLogicalFrame(object? sender, float dt)
    {
        if (engine.GlobalSettings.Get("splash.touchControls") == "false")
        {
            _desktopMouseController();
        }
    }

    
    public float TouchSteerTransfer(float X)
    {
        return Single.Clamp(Single.Sign(X) * Single.Abs(X) / 6f, -1f, 1f);
    }
    

    public Vector2 TouchSteerTransfer(Vector2 v)
    {
        return new Vector2(TouchSteerTransfer(v.X), TouchSteerTransfer(v.Y));
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


    private SortedDictionary<uint, AFingerState> _mapFingerStates = new();

    
    private void _onFingerPressed(Event ev)
    {
        AFingerState? oldFingerState = null;
        AFingerState aFingerState;
        
        lock (_lo)
        {
            if (_mapFingerStates.TryGetValue(ev.Data2, out oldFingerState))
            {
                /*
                 * This should not happen. Terminate the old one, start a new.
                 */
                _mapFingerStates.Remove(ev.Data2);
            }
        }

        if (oldFingerState != null)
        {
            oldFingerState.HandleReleased(ev);
        }

        if (ev.Position.X < 0.5f)
        {
            aFingerState = new LeftStickFingerState(ev.Position, this);
        }
        else if (ev.Position.X < 0.9f)
        {
            aFingerState = new RightStickFingerState(ev.Position, this);
        }
        else
        {
            aFingerState = new ZoomStickFingerState(ev.Position, this);
        }

        lock (_lo)
        {
            _mapFingerStates[ev.Data2] = aFingerState;
        }
        
        aFingerState.HandlePressed(ev);
    }
    
    
    private void _onFingerReleased(Event ev)
    {
        AFingerState? fingerState = null;
        
        lock (_lo)
        {
            if (_mapFingerStates.TryGetValue(ev.Data2, out fingerState))
            {
                /*
                 * We better have an old one.
                 */
                _mapFingerStates.Remove(ev.Data2);
            }
        }

        if (fingerState != null)
        {
            fingerState.HandleReleased(ev);
        }
    }
    
    
    private void _onFingerMotion(Event ev)
    {
        AFingerState? fingerState = null;
        
        lock (_lo)
        {
            if (_mapFingerStates.TryGetValue(ev.Data2, out fingerState))
            {
            }
        }

        if (fingerState != null)
        {
            fingerState.HandleMotion(ev);
        }
    }


    public void _onStickMoved(Event ev)
    {
        Vector2 pos = StickTransfer(ev.Position);
        switch (ev.Data1)
        {
            case 0:
                /*
                 * This is left-right for driving.
                 */
                lock (_lo)
                {
                    if (ev.Position.X > 0)
                    {
                        _controllerState.AnalogRight = (int)(pos.X * 255f);
                        _controllerState.AnalogLeft = 0;
                    }
                    else
                    {
                        _controllerState.AnalogRight = 0;
                        _controllerState.AnalogLeft = -(int)(pos.X * 255f);
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
                            Position = new Vector2(0f, zoomWay)
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


    private bool _isGamepadRightStickPressed = false;
    
    public void _onGamepadButtonPressed(Event ev)
    {
        //Trace($"Button {ev.Code} pressed");

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
                    _controllerState.AnalogBackward = (int)(255f * (ev.Position.X+1f)/2f);
                    _controllerState.AnalogToWalkControllerNoLock();
                }

                break;
            
            case 1:
                /*
                 * This is for accelerating
                 */
                lock (_lo)
                {
                    _controllerState.AnalogForward = (int)(255f * (ev.Position.X+1f)/2f);
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
            if (ev.Type.StartsWith(Event.INPUT_MOUSE_PRESSED)) _handleMousePressed(ev);
            if (ev.Type.StartsWith(Event.INPUT_MOUSE_RELEASED)) _handleMouseReleased(ev);
            if (ev.Type.StartsWith(Event.INPUT_MOUSE_MOVED)) _handleMouseMoved(ev);
        }

        if (ev.Type.StartsWith(Event.INPUT_KEY_PRESSED)) _onKeyDown(ev);
        if (ev.Type.StartsWith(Event.INPUT_KEY_RELEASED)) _onKeyUp(ev);

        if (ev.Type.StartsWith(Event.INPUT_FINGER_PRESSED)) _onFingerPressed(ev);
        if (ev.Type.StartsWith(Event.INPUT_FINGER_RELEASED)) _onFingerReleased(ev);
        if (ev.Type.StartsWith(Event.INPUT_FINGER_MOVED)) _onFingerMotion(ev);
        
        if (ev.Type.StartsWith(Event.INPUT_GAMEPAD_STICK_MOVED)) _onStickMoved(ev);
        if (ev.Type.StartsWith(Event.INPUT_GAMEPAD_TRIGGER_MOVED)) _onTriggerMoved(ev);
        if (ev.Type.StartsWith(Event.INPUT_GAMEPAD_BUTTON_PRESSED)) _onGamepadButtonPressed(ev);
        if (ev.Type.StartsWith(Event.INPUT_GAMEPAD_BUTTON_RELEASED)) _onGamepadButtonReleased(ev);
        
    }

    
    public void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        I.Get<InputEventPipeline>().RemoveInputPart(this);
        I.Get<SubscriptionManager>().Unsubscribe(Event.VIEW_SIZE_CHANGED, _onViewSizeChanged);
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        I.Get<SubscriptionManager>().Subscribe(Event.VIEW_SIZE_CHANGED, _onViewSizeChanged);
        I.Get<InputEventPipeline>().AddInputPart(0f, this);
        _refreshViewSize();
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}