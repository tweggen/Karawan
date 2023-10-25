using System;
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
    private Vector2 _vViewSize = Vector2.Zero;
    private Vector2 _vMouseMove = Vector2.Zero;
    private Vector2 _mousePressPosition = Vector2.Zero;
    private Vector2 _currentMousePosition = Vector2.Zero;
    private bool _isMouseButtonClicked = false;
    private Vector2 _lastMousePosition;


    public ControllerState _controllerState = new();


    public float TouchLookMoveSensitivity { get; set; } = 1.5f;
    public float MouseLookMoveSensitivity  { get; set; }= 1f;


    private void _onKeyDown(Event ev)
    {
        _controllerState.LastInput = DateTime.UtcNow;
        
        switch (ev.Code)
        {
            case "(shiftleft)":
                _controllerState.WalkFast = true;
                break;
            case "Q":
                _controllerState.FlyUp = (int) ControllerFlyUpNormal;
                break;
            case "Z":
                _controllerState.FlyDown = (int)ControllerFlyDownNormal;
                break;
            case "W":
                _controllerState.WalkForward = _controllerState.WalkFast?(int)ControllerWalkForwardFast:(int)ControllerWalkForwardNormal;
                break;
            case "S":
                _controllerState.WalkBackward = _controllerState.WalkFast?(int)ControllerWalkBackwardFast:(int)ControllerWalkBackwardNormal;
                break;
            case "A":
                _controllerState.TurnLeft = Int32.Min((int)(_controllerState.TurnLeft + ControllerTurnLeftRight / 3), (int)ControllerTurnLeftRight); 
                break;
            case "D":
                _controllerState.TurnRight = Int32.Min((int)(_controllerState.TurnRight + ControllerTurnLeftRight / 3), (int)ControllerTurnLeftRight); 
                break;
            default:
                break;
        }
    }
    

    private void _onKeyUp(Event ev)
    {
        _controllerState.LastInput = DateTime.UtcNow;

        switch (ev.Code)
        {
            case "(shiftleft)":
                _controllerState.WalkFast = false;
                break;
            case "Q":
                _controllerState.FlyUp = 0;
                break;
            case "Z":
                _controllerState.FlyDown = 0;
                break;
            case "W":
                _controllerState.WalkForward = 0;
                break;
            case "S":
                _controllerState.WalkBackward = 0;
                break;
            case "A":
                _controllerState.TurnLeft = 0;
                break;
            case "D":
                _controllerState.TurnRight = 0;
                break;
            default:
                break;
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
                    _controllerState.WalkForward = (int)(Single.Min(ControllerYMax, -vRel.Y-ControllerYTolerance) / ControllerYMax *
                                                         ControllerWalkForwardFast);
                    _controllerState.WalkBackward = 0;
                }
                else if (vRel.Y > ControllerYTolerance)
                {
                    /*
                     * The user dragged down compared to the press position.
                     */
                    _controllerState.WalkBackward = (int)(Single.Min(ControllerYMax, vRel.Y-ControllerYTolerance) / ControllerYMax *
                                                          ControllerWalkBackwardFast);
                    _controllerState.WalkForward = 0;
                }

                if (vRel.X < -ControllerXTolerance)
                {
                    _controllerState.TurnLeft = (int)(Single.Min(ControllerXMax, -vRel.X-ControllerXTolerance) / ControllerXMax * ControllerTurnLeftRight);
                    _controllerState.TurnRight = 0;
                }
                else if (vRel.X > ControllerXTolerance)
                {
                    _controllerState.TurnRight = (int)(Single.Min(ControllerXMax, vRel.X-ControllerXTolerance) / ControllerXMax * ControllerTurnLeftRight);
                    _controllerState.TurnLeft = 0;
                }
            }
            else
            {
                var viewSize = _vViewSize;
                if (_lastTouchPosition == default)
                {
                    _lastTouchPosition = _currentMousePosition;
                }

                _vMouseMove += ((_currentMousePosition - _lastTouchPosition) / viewSize.Y) * 900f *
                               TouchLookMoveSensitivity;
            }
        }
    }
    
    
    public float ControllerWalkForwardFast { get; set; } = 255f;
    public float ControllerWalkBackwardFast { get; set; } = 255f;
    public float ControllerWalkForwardNormal { get; set; } = 200f;
    public float ControllerWalkBackwardNormal { get; set; } = 200f;
    public float ControllerFlyUpNormal { get; set; } = 200f;
    public float ControllerFlyDownNormal { get; set; } = 200f;
    public float ControllerTurnLeftRight { get; set; } = 200f;


    public float ControllerYMax { get; set; } = 0.2f;
    public float ControllerYTolerance { get; set; } = 0.05f;
    public float ControllerXMax { get; set; } = 0.15f;
    public float ControllerXTolerance { get; set; } = 0.05f;

    
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
                    float zoomWay = relY / ControllerTouchZoomFull * (16+128);
                    float newZoom = (float)_zoomAtPress-zoomWay;
                    _controllerState.ZoomState = (sbyte)Single.Min(16, Single.Max(-128, newZoom));
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
                _controllerState.WalkForward = 0;
                _controllerState.WalkBackward = 0;
                _controllerState.TurnRight = 0;
                _controllerState.TurnLeft = 0;

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
                    _vMouseMove += new Vector2(xOffset, yOffset);
                }
                _lastMousePosition = _currentMousePosition;
            }
        }
    }


    private void _handleMouseWheel(Event ev)
    {
        
        /*
         *  Translate mouse wheel to zooming in/out. 
         */
        var y = ev.Position.Y;
        lock (_lo)
        {
            int currentZoomState = _controllerState.ZoomState;
            currentZoomState += (int) y;
            currentZoomState = Int32.Max(-128, currentZoomState);
            currentZoomState = Int32.Min(16, currentZoomState);
            _controllerState.ZoomState = (sbyte) currentZoomState;
        }
    }

    
    private void _handleMouseReleased(Event ev)
    {
        if (ev.Code != "0")
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
    

    private void _handleAdditionalTouchPressed(Event ev)
    {
        lock (_lo)
        {
            _zoomAtPress = _controllerState.ZoomState;
        }

        _touchCheckDebugClick(ev);
    }
    
    
    private void _handleMousePressed(Event ev)
    {
        if (ev.Code != "0")
        {
            return;
        }

        lock (_lo)
        {
            _mousePressPosition = ev.Position;
            _currentMousePosition = ev.Position;
            _isMouseButtonClicked = true;
        }
        if (engine.GlobalSettings.Get("splash.touchControls") != "false")
        {
            _handleAdditionalTouchPressed(ev);
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
        if (null == viewSize || viewSize == "") viewSize = "320x200";

        int x=320, y=200;

        int xpos = viewSize.IndexOf('x');
        if (xpos != -1)
        {
            string ypart = viewSize.Substring(xpos + 1);
            if (Int32.TryParse(viewSize.Substring(0, xpos), out x)) {}
            if (Int32.TryParse(ypart, out y)) {}
        }

        lock (_lo)
        {
            _vViewSize = new(x,y);
        }
    }
    

    private void _onViewSizeChanged(Event ev)
    {
        _refreshViewSize();
    }

    
    private void _onLogicalFrame(object? sender, float dt)
    {
        if (engine.GlobalSettings.Get("splash.touchControls") != "false")
        {
            _touchMouseController();
        }
        else
        {
            _desktopMouseController();
        }
    }
    

    public void GetMouseMove(out Vector2 vMouseMove)
    {            
        lock (_lo)
        {
            vMouseMove = _vMouseMove;
            _vMouseMove = new Vector2(0f, 0f);
        }
    }


    public void GetControllerState(out ControllerState controllerState)
    {
        lock (_lo)
        {
            controllerState = _controllerState;
        }
    }
    
    
    public void InputPartOnInputEvent(Event ev)
    {
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_PRESSED)) _handleMousePressed(ev);
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_RELEASED)) _handleMouseReleased(ev);
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_WHEEL)) _handleMouseWheel(ev);
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_MOVED)) _handleMouseMoved(ev);
        if (ev.Type.StartsWith(Event.INPUT_KEY_PRESSED)) _onKeyDown(ev);
        if (ev.Type.StartsWith(Event.INPUT_KEY_RELEASED)) _onKeyUp(ev);
    }

    
    public void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        I.Get<InputEventPipeline>().RemoveInputPart(this);
        I.Get<SubscriptionManager>().Unsubscribe(Event.VIEW_SIZE_CHANGED, _onViewSizeChanged);
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        I.Get<SubscriptionManager>().Subscribe(Event.VIEW_SIZE_CHANGED, _onViewSizeChanged);
        I.Get<InputEventPipeline>().AddInputPart(0f, this);
        _refreshViewSize();
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}