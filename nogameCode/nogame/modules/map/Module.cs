using System;
using System.Numerics;
using builtin.controllers;
using engine;
using engine.joyce;
using engine.news;


namespace nogame.modules.map;


struct DisplayMapParams
{
    static public float MAX_ZOOM_STATE = 32f;
    static public float MIN_ZOOM_STATE = 0f;
    static public float MAP_STEP_SIZE = (1f / 8f);
    public float CurrentZoomState = 16f;

    static public float MAP_MOVE_PER_FRAME = (0.16f);
    
    /**
     * Relative position of the map: (0f, 0f) would be upper left in the middle
     * of the screen, (1f, 1f) would be lower right in the middle of the screen.
     */
    public Vector2 Position = new (0.5f, 0.5f);
    
    /**
     * Shall the map be visible or not.
     */
    public bool IsVisible = false;

    public void Slerp(DisplayMapParams other, float amount)
    {
        IsVisible = other.IsVisible;

        CurrentZoomState = (other.CurrentZoomState * amount) + (CurrentZoomState * (1f - amount));
        Position = (other.Position * amount) + (Position * (1f - amount));
    }

    public DisplayMapParams()
    {
    }
}


/**
 * Scene part "map" for the main gameplay scene.
 *
 * Displays a full-screen map, using the OSD camera.
 *
 * On activation, makes the map entities visible
 */
public class Module : AModule, IInputPart
{
    private static float MY_Z_ORDER = 500f;
    
    private object _lo = new();

    private engine.Engine _engine;
    
    private bool _createdResources = false;
    
    private DefaultEcs.Entity _eMap;
   

    /*
     * Map display parameters
     */
    private DisplayMapParams _requestedMapParams = new();
    private DisplayMapParams _visibleMapParams = new();

    
    // For now, let it use the OSD camera.
    public uint MapCameraMask = 0x00010000;


    private void _updateMapParams()
    {
        DisplayMapParams dmp;
        lock (_lo)
        {
            _visibleMapParams.Slerp(_requestedMapParams, 0.2f);
            dmp = _visibleMapParams;
        }

        float effectiveSize = Single.Exp2(dmp.CurrentZoomState / 4f);

        I.Get<engine.joyce.TransformApi>().SetTransforms(
            _eMap, dmp.IsVisible, MapCameraMask,
            new Quaternion(0f,0f,0f,1f),
            new Vector3(
                dmp.Position.X * effectiveSize - effectiveSize/2f, 
                dmp.Position.Y * effectiveSize - effectiveSize/2f,
                -1f),
            effectiveSize * Vector3.One);
    }
    
    
    private void _needResources()
    {
        lock (_lo)
        {
            if (_createdResources)
            {
                return;
            }

            _createdResources = true;
        }

        engine.joyce.Mesh meshFramebuffer = 
            engine.joyce.mesh.Tools.CreatePlaneMesh(
                "mapmesh",
                new Vector2(1f, 1f));
        meshFramebuffer.UploadImmediately = true;
        engine.joyce.Texture textureFramebuffer = 
            I.Get<nogame.map.MapFramebuffer>().Texture;

        {
            _eMap = _engine.CreateEntity("nogame.parts.map.map");
            engine.joyce.Material materialFramebuffer = new();
            materialFramebuffer.UploadImmediately = true;
            materialFramebuffer.EmissiveTexture = textureFramebuffer;
            materialFramebuffer.HasTransparency = false;

            var jInstanceDesc = InstanceDesc.CreateFromMatMesh(new MatMesh(materialFramebuffer, meshFramebuffer), 50000f);
            _eMap.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            _updateMapParams();
        }
    }


    private void _applyZoomState(sbyte zoomState)
    {
        lock (_lo)
        {
            float newZoomState = //(((float)-zoomState) + 32f)
                (((float)zoomState) + 128f)
                * (DisplayMapParams.MAX_ZOOM_STATE - DisplayMapParams.MIN_ZOOM_STATE)
                / (128f + 32f) + DisplayMapParams.MIN_ZOOM_STATE;
            newZoomState = Int32.Max((int)DisplayMapParams.MIN_ZOOM_STATE, Int32.Min((int)DisplayMapParams.MAX_ZOOM_STATE, (int) newZoomState));
            _requestedMapParams.CurrentZoomState = (float) newZoomState;
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
            int currentZoomState = (int) _requestedMapParams.CurrentZoomState;
            currentZoomState += (int)y;
            currentZoomState = Int32.Max((int)DisplayMapParams.MIN_ZOOM_STATE, Int32.Min((int)DisplayMapParams.MAX_ZOOM_STATE, currentZoomState));
            _requestedMapParams.CurrentZoomState = (float) currentZoomState;
        }

        ev.IsHandled = true;
    }


    private void _applyDeltaMove(Vector2 vDelta)
    {
        /*
         * The size of the map is...
         *
         * mapsize * 2 ^ (CurrentZoomState/4)
         *
         * If CurrentZoomState is MIN, the map fits the screen.
         *
         * Let's say we want to step by 1/8 of the visible map on every
         * keypress.
         */
        lock (_lo)
        {
            vDelta *= DisplayMapParams.MAP_STEP_SIZE / Single.Exp2(_visibleMapParams.CurrentZoomState / 4f);
            _requestedMapParams.Position = 
                Vector2.Min(Vector2.One,
                    Vector2.Max(Vector2.Zero,
                        _requestedMapParams.Position + vDelta));
        }
    }
    

    private void _handleKeyDown(Event ev)
    {
        lock (_lo)
        {
            DisplayMapParams dmp = _requestedMapParams;
            bool haveChange = false;
            Vector2 vDelta = Vector2.Zero;
            
            switch (ev.Code)
            {
                case "W":
                    vDelta.Y = 1f;
                    haveChange = true;
                    break;
                case "A":
                    vDelta.X = 1f;
                    haveChange = true;
                    break;
                case "S":
                    vDelta.Y = -1f;
                    haveChange = true;
                    break;
                case "D":
                    vDelta.X = -1f;
                    haveChange = true;
                    break;
                default:
                    break;
            }

            if (haveChange)
            {
                _applyDeltaMove(vDelta);
                ev.IsHandled = true;
            }
        }
    }

    
    private void _handleKeyUp(Event ev)
    {
    }

    
    public void InputPartOnInputEvent(Event ev)
    {
        // if (ev.Type.StartsWith(Event.INPUT_MOUSE_PRESSED)) _handleMousePressed(ev);
        // if (ev.Type.StartsWith(Event.INPUT_MOUSE_RELEASED)) _handleMouseReleased(ev);
        // if (ev.Type.StartsWith(Event.INPUT_MOUSE_WHEEL)) _handleMouseWheel(ev);
        // if (ev.Type.StartsWith(Event.INPUT_MOUSE_MOVED)) _handleMouseMoved(ev);
        // if (ev.Type.StartsWith(Event.INPUT_KEY_PRESSED)) _handleKeyDown(ev);
        // if (ev.Type.StartsWith(Event.INPUT_KEY_RELEASED)) _handleKeyUp(ev);
    }

    
    private void _handleController()
    {
        I.Get<InputController>().GetControllerState(out var controllerState);

        Vector2 vDelta = Vector2.Zero;
        vDelta.X += (float)controllerState.TurnRight / 200f * DisplayMapParams.MAP_MOVE_PER_FRAME;
        vDelta.X -= (float)controllerState.TurnLeft / 200f * DisplayMapParams.MAP_MOVE_PER_FRAME;
        vDelta.Y += (float)controllerState.WalkForward / 200f * DisplayMapParams.MAP_MOVE_PER_FRAME;
        vDelta.Y -= (float)controllerState.WalkBackward / 200f * DisplayMapParams.MAP_MOVE_PER_FRAME;

        _applyDeltaMove(vDelta);

        sbyte zoomState = controllerState.ZoomState;
        _applyZoomState(zoomState);
    }


    public void _onLogicalFrame(object? sender, float dt)
    {
        if (MY_Z_ORDER == I.Get<InputEventPipeline>().GetFrontZ())
        {
            _handleController();
        }
        _updateMapParams();
    }
    

    public void Dispose()
    {
        _eMap.Dispose();
        _createdResources = false;
    }
    
    
    public void ModuleDeactivate()
    {
        lock(_lo) {
            _requestedMapParams.IsVisible = false;
        }
        _engine.QueueMainThreadAction(_updateMapParams);

        I.Get<InputEventPipeline>().RemoveInputPart(this);
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _engine.RemoveModule(this);
    }

    
    public void ModuleActivate(Engine engine0)
    {
        _engine = engine0;

        _needResources();
        _engine.AddModule(this);
        _engine.OnLogicalFrame += _onLogicalFrame;

        lock(_lo) {
            _requestedMapParams.IsVisible = true;
        }

        I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
    }
}
