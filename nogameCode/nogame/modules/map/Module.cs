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

    public float MiniMapSize { get; set; } = 0.12f;

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


    /**
     * Identifiers of the desired map modes.
     * The mop modes actually differ in the way the map camera is windowed.
     * When displayed as a fullscreen map, the map is displayed full screen, when
     * displayed as a minimap, the map is windowed.
     */
    public enum Modes {
        /**
         * No map is displayed at all.
         */
        MapNone,
        
        /**
         * The map is completely visible, usually at a small scale.
         * The map is not clipped at all.
         */
        MapFullscreen,
        
        /**
         * A small windowed view on the map is shown, usually at a detailed scale.-
         * The map is clipped and may be rotated.
         */
        MapMini,
    };


    private Modes _mode = Modes.MapNone;
    public Modes Mode
    {
        get
        {
            lock (_lo)
            {
                return _mode;
            }
        }
        set
        {
            Modes oldMode;
            lock (_lo)
            {
                if (_mode == value)
                {
                    return;
                }

                oldMode = _mode;

                _mode = value;
            }

            _changeModeTo(oldMode, value);
        }
    }
    

    
    private float _scaleF(in DisplayMapParams dmp)
    {
        float x = (dmp.CurrentZoomState - DisplayMapParams.MIN_ZOOM_STATE)
            / DisplayMapParams.MAX_ZOOM_STATE - DisplayMapParams.MIN_ZOOM_STATE;
        return (1024f * (x * x * x) + 3) / (engine.world.MetaGen.MaxHeight);
    }
    

    private void _updateMapParams()
    {
        DisplayMapParams dmp;
        Modes mode;
        lock (_lo)
        {
            _visibleMapParams.Slerp(_requestedMapParams, 0.2f);
            dmp = _visibleMapParams;
            mode = _mode;
        }

        I.Get<TransformApi>().SetVisible(_eCamMap, dmp.IsVisible);
        I.Get<TransformApi>().SetVisible(_eMap, true);
        I.Get<TransformApi>().SetCameraMask(_eMap, MapCameraMask);

        // TXWTODO: We better should consider the zoom state.
        Vector3 v3CamPos = Vector3.Zero;

        ref var cCamera3 = ref _eCamMap.Get<Camera3>();
        /*
         * Now, depending on the mode, setup the camera.
         */
        switch (mode)
        {
            case Modes.MapFullscreen:
                float scale = _scaleF(dmp);
                cCamera3.Scale = scale;
                cCamera3.UL = Vector2.Zero;
                cCamera3.LR = Vector2.One;
                v3CamPos = new(
                    (dmp.Position.X - 0.5f) * (MetaGen.MaxSize.X),
                    CameraY,
                    (dmp.Position.Y - 0.5f) * (MetaGen.MaxSize.Y)
                );
                break;
            case Modes.MapMini:
            {
                var ePlayer = _engine.GetPlayerEntity();
                if (ePlayer.IsAlive && ePlayer.Has<engine.joyce.components.Transform3ToWorld>())
                {
                    lock (_engine.Simulation)
                    {
                        ref var prefPlayer = ref ePlayer.Get<engine.physics.components.Body>().Reference;
                        v3CamPos = prefPlayer.Pose.Position with { Y = CameraY };
                    }
                }

                cCamera3.Scale = (2f * 1024f + 3f) / MetaGen.MaxHeight;
                cCamera3.UL = new Vector2(0.05f, 0.05f);
                cCamera3.LR = new Vector2(0.05f + MiniMapSize, 0.05f + MiniMapSize * 16f / 9f);
            }
                break;
        }

        I.Get<TransformApi>().SetTransforms(_eCamMap,
            dmp.IsVisible, MapCameraMask,
            Quaternion.CreateFromAxisAngle(new Vector3(1f, 0f, 0f), 3f * Single.Pi / 2f),
            v3CamPos
        );

        _computeAABB();
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
            cCamMap.Scale = 2f / engine.world.MetaGen.MaxHeight;
            cCamMap.CameraMask = MapCameraMask;
            cCamMap.CameraFlags = engine.joyce.components.Camera3.Flags.PreloadOnly;
            _eCamMap.Set(cCamMap);
            /*
             * Let the camera be well over every object
             */
            I.Get<TransformApi>().SetTransforms(_eCamMap, false, MapCameraMask,
                Quaternion.CreateFromAxisAngle(new Vector3(1f, 0f, 0f), 3f * Single.Pi / 2f),
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

            var jInstanceDesc =
                InstanceDesc.CreateFromMatMesh(new MatMesh(materialFramebuffer, meshFramebuffer), 100000f);
            _eMap.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            I.Get<TransformApi>().SetTransforms(_eMap,
                true, MapCameraMask,
                Quaternion.CreateFromAxisAngle(Vector3.UnitX, 3f * Single.Pi / 2f),
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
        if (halfX * halfY > 9000000f)
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
        lock (_lo)
        {
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
        Modes mode;
        lock (_lo)
        {
            mode = _mode;
        }

        if (mode != Modes.MapFullscreen)
        {
            return;
        }
        
        /*
         *  Translate mouse wheel to zooming in/out.
         */
        /*
         * size y contains the delta.
         */
        int y = (int)ev.Position.Y;
        lock (_lo)
        {
            int currentZoomState = (int)_requestedMapParams.CurrentZoomState;
            currentZoomState += DisplayMapParams.ZOOM_STEPS * y;
            currentZoomState = Int32.Max((int)DisplayMapParams.MIN_ZOOM_STATE,
                Int32.Min((int)DisplayMapParams.MAX_ZOOM_STATE, currentZoomState));
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
        Modes mode;
        lock (_lo)
        {
            mode = _mode;
        }

        if (mode != Modes.MapFullscreen)
        {
            return;
        }


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
        bool shallHandleController = false;

        lock (_lo)
        {
            if (_mode == Modes.MapFullscreen)
            {
                shallHandleController = true;
            }
        }
        if (shallHandleController)
        {
            _handleController();
        }

        _updateMapParams();
    }


    private bool _haveInputPart = false;

    private void _noInputPart()
    {
        lock (_lo)
        {
            if (!_haveInputPart) return;
            _haveInputPart = false;
            I.Get<InputEventPipeline>().RemoveInputPart(this);
        }
    }


    private void _needInputPart()
    {
        lock (_lo)
        {
            if (_haveInputPart) return;
            _haveInputPart = true;
            I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        }
    }

    
    private void _changeModeToNone(Modes oldMode, Modes newMode)
    {
        lock(_lo) {
            _requestedMapParams.IsVisible = false;
        }
        _engine.QueueMainThreadAction( () =>
        {
            
            _updateMapParams();
            
            /*
             * Also reset the currently active map area.
             */
            lock (_lo)
            {
                _aabbMap.Reset();
            }
            _notifyAABB();
        
        });

        _noInputPart();
        _engine.OnLogicalFrame -= _onLogicalFrame;
    }


    private void _changeModeToFullscreen(Modes oldMode, Modes newMode)
    {
        DefaultEcs.Entity eEmpty = default;
        /*
         * Trigger creation of world map entities if not created yet.
         */
        I.Get<IMapProvider>().WorldMapCreateEntities(eEmpty, MapCameraMask);
        
        _needResources();

        _engine.OnLogicalFrame += _onLogicalFrame;

        lock(_lo) {
            _requestedMapParams.IsVisible = true;
        }

        if (newMode == Modes.MapFullscreen)
        {
            _needInputPart();
        }
        else
        {
            _noInputPart();
        }

        _computeAABB();
    }


    private void _changeMiniToFull(Modes oldMode, Modes newMode)
    {
        _needInputPart();   
        _computeAABB();
    }


    private void _changeFullToMini(Modes oldMode, Modes newMode)
    {
        _noInputPart();
        _computeAABB();
    }
    

    private void _changeModeTo(Modes oldMode, Modes newMode)
    {
        switch (oldMode)
        {
            case Modes.MapNone:
                switch (newMode)
                {
                    case Modes.MapMini:
                    case Modes.MapFullscreen:
                        _changeModeToFullscreen(oldMode, newMode);
                        break;
                    default:
                        break;
                }

                break;
            case Modes.MapFullscreen:
            case Modes.MapMini:
                switch (newMode)
                {
                    case Modes.MapNone:
                        _changeModeToNone(oldMode, newMode);
                        break;
                    case Modes.MapFullscreen:
                        _changeMiniToFull(oldMode, newMode);
                        break;
                    case Modes.MapMini:
                        _changeFullToMini(oldMode, newMode);
                        break;
                    default:
                        break;
                }

                break;
        }

    }
    

    public void Dispose()
    {
        _eMap.Dispose();
        _createdResources = false;
    }
    
    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }

    
    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);
    }
}
