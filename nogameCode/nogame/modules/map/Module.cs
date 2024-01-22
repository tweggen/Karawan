using System;
using System.Diagnostics;
using System.Numerics;
using builtin.controllers;
using builtin.map;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.world;

using static engine.Logger;

namespace nogame.modules.map;


struct DisplayMapParams
{
    static public float MAX_ZOOM_STATE = 128f;
    static public float MIN_ZOOM_STATE = 0f;
    static public float MAP_STEP_SIZE = (1f / 8f);
    static public int ZOOM_STEPS = 3;
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
    
    private bool _createdResources = false;
    
    private DefaultEcs.Entity _eMap;
    private DefaultEcs.Entity _eCamMap;
    
    private float _zoomState = 0.2f; 
    public float ZOOM_STEP_FRACTION { get; set; } = 60f;
    public float CameraY { get; set; } = 700f;
    public float MapY { get; set; } = 200f;

    /*
     * Map display parameters
     */
    private DisplayMapParams _requestedMapParams = new();
    private DisplayMapParams _visibleMapParams = new();

    private engine.geom.AABB _aabbMap = new();

    // TXWTODO: This is a guesstimate. Compute it properly.
    private float _viewHeight = 6f;
    private float _viewWidth = 6f * 16f / 9f;
    
    
    static public uint MapCameraMask = 0x00800000;


    private float _scaleF(in DisplayMapParams dmp)
    {
        float x = (dmp.CurrentZoomState-DisplayMapParams.MIN_ZOOM_STATE) 
            / DisplayMapParams.MAX_ZOOM_STATE-DisplayMapParams.MIN_ZOOM_STATE;
        return (1024f * (x*x*x) + 3) / (engine.world.MetaGen.MaxHeight);
    }

    private void _updateMapParams()
    {
        DisplayMapParams dmp;
        lock (_lo)
        {
            _visibleMapParams.Slerp(_requestedMapParams, 0.2f);
            dmp = _visibleMapParams;
        }

        I.Get<TransformApi>().SetVisible(_eCamMap, dmp.IsVisible);
        I.Get<TransformApi>().SetVisible(_eMap, true);
        I.Get<TransformApi>().SetCameraMask(_eMap, MapCameraMask);
        
        // TXWTODO: We better should consider the zoom state.
        Vector3 vCamPos = new(
            (dmp.Position.X-0.5f) * (MetaGen.MaxSize.X), 
            CameraY, 
            (dmp.Position.Y-0.5f) * (MetaGen.MaxSize.Y)
            );
        
        I.Get<TransformApi>().SetTransforms(_eCamMap,
            dmp.IsVisible, MapCameraMask, 
            Quaternion.CreateFromAxisAngle(new Vector3(1f, 0f, 0f), 3f*Single.Pi/2f), 
            vCamPos
            );
        float scale = _scaleF(dmp);
        
        _eCamMap.Get<Camera3>().Scale = scale;
        _computeAABB();

        if (false) {
            var cCamera3 = _eCamMap.Get<Camera3>();
            var mCamToWorld = _eCamMap.Get<engine.joyce.components.Transform3ToWorld>().Matrix;
            //Trace($"mCamToWorld {mCamToWorld}");
            cCamera3.GetViewMatrix(out var mModelView, mCamToWorld);
            cCamera3.GetProjectionMatrix(out var mProj, new Vector2(1920, 1080));
            //var mProj = Matrix4x4.Identity;
            Vector3 v3ProjCamPos = Vector3.Transform(vCamPos, mModelView * mProj);
            Vector3 v3ProjPlanePos = Vector3.Transform(new Vector3(0f, 40f, 0f), mModelView * mProj);
            Vector3 v3ProjMapPos = Vector3.Transform(MapY * Vector3.UnitY, mModelView * mProj);
            //Trace($"{v3ProjCamPos}, ${v3ProjPlanePos}, ${v3ProjMapPos}");
            int a = 1;
        }
        
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
        
        /*
         * Create a map camera
         */
        {
            _eCamMap = _engine.CreateEntity("RootScene.MapCamera");
            var cCamMap = new engine.joyce.components.Camera3();
            cCamMap.Angle = 0f;
            cCamMap.NearFrustum = 5f;
            cCamMap.FarFrustum = 1000f;
            cCamMap.Scale = 2f/engine.world.MetaGen.MaxHeight;
            cCamMap.CameraMask = MapCameraMask;
            cCamMap.CameraFlags = engine.joyce.components.Camera3.Flags.PreloadOnly;
            _eCamMap.Set(cCamMap);
            /*
             * Let the camera be well over every object
             */
            I.Get<TransformApi>().SetTransforms(_eCamMap, false, MapCameraMask,
                Quaternion.CreateFromAxisAngle(new Vector3(1f, 0f, 0f), 3f*Single.Pi/2f),
                new Vector3(0f, CameraY, 0f));
            
            _eCamMap.Get<engine.joyce.components.Camera3>().CameraFlags &=
                ~engine.joyce.components.Camera3.Flags.PreloadOnly;
        }
        
        
        /*
         * The map plane is exactly there where the real world is:
         * We are looking at the real world from a top-down perspective.
         * This way we can attach map icons to real world objects.
         *
         * Note, that the current scope of the map however might be
         * limited.
         */
        engine.joyce.Mesh meshFramebuffer =
            engine.joyce.mesh.Tools.CreatePlaneMesh(
                "mapmesh", /* new Vector2(500f, 500f) */ engine.world.MetaGen.MaxSize);
        meshFramebuffer.UploadImmediately = true;
        engine.joyce.Texture textureFramebuffer = 
            I.Get<nogame.map.MapFramebuffer>().Texture;

        {
            _eMap = _engine.CreateEntity("nogame.parts.map.map");
            engine.joyce.Material materialFramebuffer = new();
            materialFramebuffer.UploadImmediately = true;
            materialFramebuffer.EmissiveTexture = textureFramebuffer;
            materialFramebuffer.HasTransparency = false;

            var jInstanceDesc = InstanceDesc.CreateFromMatMesh(new MatMesh(materialFramebuffer, meshFramebuffer), 100000f);
            _eMap.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            I.Get<TransformApi>().SetTransforms(_eMap,
                true, MapCameraMask,
                Quaternion.CreateFromAxisAngle(Vector3.UnitX, 3f*Single.Pi/2f),
                -MapY * Vector3.UnitY);
        }
        _updateMapParams();
    }


    private void _computeAABB()
    {
        DisplayMapParams dmp;

        lock (_lo)
        {
            dmp = _visibleMapParams;
        }

        float scale = _scaleF(dmp);

        float halfY = (_viewHeight / 2f) / scale;
        float halfX = (_viewWidth / 2f) / scale;

        engine.geom.AABB aabb = new();
        /*
         * Limit the map details to a view of max 3km * 3km
         */
        if (halfX*halfY > 9000000f)
        {
            aabb.Reset();
        }
        else
        {
            Vector3 vCamPos = new(
                (dmp.Position.X - 0.5f) * (MetaGen.MaxSize.X),
                0f,
                (dmp.Position.Y - 0.5f) * (MetaGen.MaxSize.Y)
            );

            aabb.AA = new Vector3(vCamPos.X - halfX, -10000f, vCamPos.Z - halfY);
            aabb.BB = new Vector3(vCamPos.X + halfX, +10000f, vCamPos.Z + halfY);
        }

        lock (_lo)
        {
            if (aabb != _aabbMap)
            {
                _aabbMap = aabb;
                _notifyAABB();
            }
        }
    }
    

    private void _notifyAABB()
    {
        engine.geom.AABB aabb;
        lock (_lo) {
            aabb = _aabbMap;   
        }
        I.Get<EventQueue>().Push(
            new builtin.map.MapRangeEvent("mainmap")
            {
                AABB = aabb
            });
    }


    private void _handleMouseWheel(Event ev)
    {
        /*
         *  Translate mouse wheel to zooming in/out. 
         */
        /*
         * size y contains the delta.
         */
        int y = (int)ev.Position.Y;
        lock (_lo)
        {
            int currentZoomState = (int) _requestedMapParams.CurrentZoomState;
            currentZoomState += DisplayMapParams.ZOOM_STEPS * y;
            currentZoomState = Int32.Max((int)DisplayMapParams.MIN_ZOOM_STATE, Int32.Min((int)DisplayMapParams.MAX_ZOOM_STATE, currentZoomState));
            _requestedMapParams.CurrentZoomState = currentZoomState;
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
            vDelta *= 0.00001f / _scaleF(_visibleMapParams);
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
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_WHEEL)) _handleMouseWheel(ev);
        // if (ev.Type.StartsWith(Event.INPUT_MOUSE_MOVED)) _handleMouseMoved(ev);
        //if (ev.Type.StartsWith(Event.INPUT_KEY_PRESSED)) _handleKeyDown(ev);
        // if (ev.Type.StartsWith(Event.INPUT_KEY_RELEASED)) _handleKeyUp(ev);
    }

    
    private void _handleController()
    {
        I.Get<InputController>().GetControllerState(out var controllerState);

        Vector2 vDelta = Vector2.Zero;
        vDelta.X += (float)controllerState.TurnRight / 200f * DisplayMapParams.MAP_MOVE_PER_FRAME;
        vDelta.X -= (float)controllerState.TurnLeft / 200f * DisplayMapParams.MAP_MOVE_PER_FRAME;
        vDelta.Y -= (float)controllerState.WalkForward / 200f * DisplayMapParams.MAP_MOVE_PER_FRAME;
        vDelta.Y += (float)controllerState.WalkBackward / 200f * DisplayMapParams.MAP_MOVE_PER_FRAME;

        _applyDeltaMove(vDelta);
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
    
    
    public override void ModuleDeactivate()
    {
        lock(_lo) {
            _requestedMapParams.IsVisible = false;
        }
        _engine.QueueMainThreadAction(_updateMapParams);

        I.Get<InputEventPipeline>().RemoveInputPart(this);
        _engine.OnLogicalFrame -= _onLogicalFrame;
        
        /*
         * Also reset the currently active map area.
         */
        lock (_lo)
        {
            _aabbMap.Reset();
        }
        _notifyAABB();
        
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }

    
    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);

        DefaultEcs.Entity eEmpty = default;
        I.Get<IMapProvider>().WorldMapCreateEntities(engine0, eEmpty, MapCameraMask);
        
        _needResources();
        _engine.AddModule(this);
        _engine.OnLogicalFrame += _onLogicalFrame;

        lock(_lo) {
            _requestedMapParams.IsVisible = true;
        }

        I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        _computeAABB();
    }
}
