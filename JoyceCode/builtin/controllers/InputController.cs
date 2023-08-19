using System;
using System.Numerics;
using engine;
using engine.news;

namespace builtin.controllers;


/**
 * Translate input events to the Game Controller data structure, providing a more
 * semantic input representation that can be polled.
 *
 * COnsumes input 
 */
public class InputController : engine.IPart
{
    private object _lo = new();
    
    private engine.Engine _engine;

    private Vector2 _vViewSize = Vector2.Zero;
    private Vector2 _vMouseMove = Vector2.Zero;
    private Vector2 _mousePressPosition = Vector2.Zero;
    private Vector2 _currentMousePosition = Vector2.Zero;
    private bool _isMouseButtonClicked = false;
    private Vector2 _lastMousePosition;


    public ControllerState _controllerState = new();


    private float _touchLookMoveSensitivity = 1f;


    private void _onKeyDown(Event ev)
    {
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
                _controllerState.TurnLeft = (int)ControllerTurnLeftRight;
                break;
            case "D":
                _controllerState.TurnRight = (int)ControllerTurnLeftRight;
                break;
            case "(tab)":
                _controllerState.ShowMap = true;
                break;
            case "(escape)":
                _controllerState.PauseMenu = true;
                break;
            default:
                break;
        }
    }
    

    private void _onKeyUp(Event ev)
    {
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
            case "(tab)":
                _controllerState.ShowMap = false;
                break;  
            case "(escape)":
                _controllerState.PauseMenu = false;
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
                if (vRel.Y < 0)
                {
                    /*
                     * The user dragged up compare to the press position
                     */
                    _controllerState.WalkForward = (int)(Single.Min(ControllerYMax, -vRel.Y) / ControllerYMax *
                                                         ControllerWalkForwardFast);
                    _controllerState.WalkBackward = 0;
                }
                else if (vRel.Y > 0)
                {
                    /*
                     * The user dragged down compared to the press position.
                     */
                    _controllerState.WalkBackward = (int)(Single.Min(ControllerYMax, vRel.Y) / ControllerYMax *
                                                          ControllerWalkBackwardFast);
                    _controllerState.WalkForward = 0;
                }

                if (vRel.X < 0)
                {
                    _controllerState.TurnLeft =
                        (int)(Single.Min(ControllerXMax, -vRel.X) / ControllerXMax * ControllerTurnLeftRight);
                    _controllerState.TurnRight = 0;
                }
                else if (vRel.X > 0)
                {
                    _controllerState.TurnRight = (int)(Single.Min(ControllerXMax, vRel.X) * ControllerTurnLeftRight);
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
                               _touchLookMoveSensitivity;
            }
        }
    }
    
    
    private readonly float ControllerWalkForwardFast = 255f;
    private readonly float ControllerWalkBackwardFast = 255f;
    private readonly float ControllerWalkForwardNormal = 200f;
    private readonly float ControllerWalkBackwardNormal = 200f;
    private readonly float ControllerFlyUpNormal = 200f;
    private readonly float ControllerFlyDownNormal = 200f;
    private readonly float ControllerTurnLeftRight = 200f;


    private readonly float ControllerYMax = 0.2f; 
    private readonly float ControllerXMax = 0.13f;

    private Vector2 _lastTouchPosition = default;

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

                _handleTouchMove(
                    new Vector2(
                        _mousePressPosition.X / viewSize.X, 
                        _mousePressPosition.Y / viewSize.Y),
                    new Vector2(relX, relY));

                _lastTouchPosition = _currentMousePosition;
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
                    var xOffset = (_currentMousePosition.X - _lastMousePosition.X) * _touchLookMoveSensitivity;
                    var yOffset = (_currentMousePosition.Y - _lastMousePosition.Y) * _touchLookMoveSensitivity;
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
            currentZoomState -= (int) y;
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
            if (Int32.TryParse(viewSize, out x)) {}
            if (Int32.TryParse(ypart, out x)) {}
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
    
    
    public void PartOnInputEvent(Event ev)
    {
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_PRESSED)) _handleMousePressed(ev);
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_RELEASED)) _handleMouseReleased(ev);
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_WHEEL)) _handleMouseWheel(ev);
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_MOVED)) _handleMouseMoved(ev);
        if (ev.Type.StartsWith(Event.INPUT_KEY_PRESSED)) _onKeyDown(ev);
        if (ev.Type.StartsWith(Event.INPUT_KEY_RELEASED)) _onKeyUp(ev);
    }

    
    public void PartDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _engine.RemovePart(this);
    }
    

    public void PartActivate(in Engine engine0, in IScene scene0)
    {
        _engine = engine0;
        Implementations.Get<SubscriptionManager>().Subscribe(Event.VIEW_SIZE_CHANGED, _onViewSizeChanged);
        _refreshViewSize();
        _engine.AddPart(10000, scene0, this);
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}