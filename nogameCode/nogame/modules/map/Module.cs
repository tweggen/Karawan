using System;
using System.Numerics;
using engine;
using engine.joyce;
using engine.news;


namespace nogame.modules.map;


struct DisplayMapParams
{
    public float CurrentZoomState = 16f;
    public bool IsVisible = false;

    public void Slerp(DisplayMapParams other, float amount)
    {
        IsVisible = other.IsVisible;

        CurrentZoomState = (other.CurrentZoomState * amount) + (CurrentZoomState * (1f - amount));
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
    private object _lo = new();

    private engine.Engine _engine;
    
    private bool _createdResources = false;
    
    private DefaultEcs.Entity _eMap;
   

    /*
     * Map display parameters
     */
    private bool _displayParamsChanged = true;
    private DisplayMapParams _requestedMapParams = new();
    private DisplayMapParams _visibleMapParams = new();

    
    // For now, let it use the OSD camera.
    public uint MapCameraMask = 0x00010000;


    private void _updateMapParams()
    {
        DisplayMapParams dmp;
        lock (_lo)
        {
            _visibleMapParams.Slerp(_requestedMapParams, 0.05f);
            dmp = _visibleMapParams;
        }

        float effectiveSize = Single.Exp2(dmp.CurrentZoomState / 4f);

        _engine.GetATransform().SetTransforms(
            _eMap, dmp.IsVisible, MapCameraMask,
            new Quaternion(0f,0f,0f,1f),
            new Vector3(0f, 0f, -1f),
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

        engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreatePlaneMesh(
            "mapmesh",
            new Vector2(1f, 1f));
        meshFramebuffer.UploadImmediately = true;
        engine.joyce.Texture textureFramebuffer = 
            Implementations.Get<nogame.map.MapFramebuffer>().Texture;

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
            currentZoomState = Int32.Max(1, Int32.Min(64, currentZoomState));
            _requestedMapParams.CurrentZoomState = (float) currentZoomState;
            _displayParamsChanged = true;
        }

        ev.IsHandled = true;
    }

    
    public void InputPartOnInputEvent(Event ev)
    {
        // if (ev.Type.StartsWith(Event.INPUT_MOUSE_PRESSED)) _handleMousePressed(ev);
        // if (ev.Type.StartsWith(Event.INPUT_MOUSE_RELEASED)) _handleMouseReleased(ev);
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_WHEEL)) _handleMouseWheel(ev);
        // if (ev.Type.StartsWith(Event.INPUT_MOUSE_MOVED)) _handleMouseMoved(ev);
        // if (ev.Type.StartsWith(Event.INPUT_KEY_PRESSED)) _onKeyDown(ev);
        // if (ev.Type.StartsWith(Event.INPUT_KEY_RELEASED)) _onKeyUp(ev);
        bool displayParamsChanged;
        lock (_lo)
        {
            displayParamsChanged = _displayParamsChanged;
        }

        if (_displayParamsChanged)
        {
            _engine.QueueMainThreadAction(_updateMapParams);
        }
    }


    public void _onLogicalFrame(object? sender, float dt)
    {
        _updateMapParams();
    }
    

    public void Dispose()
    {
        _eMap.Dispose();
        _createdResources = false;
        _displayParamsChanged = true;
    }
    
    
    public void ModuleDeactivate()
    {
        lock(_lo) {
            _requestedMapParams.IsVisible = false;
            _displayParamsChanged = true;
        }
        _engine.QueueMainThreadAction(_updateMapParams);

        Implementations.Get<InputEventPipeline>().RemoveInputPart(this);
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
            _displayParamsChanged = true;
        }

        Implementations.Get<InputEventPipeline>().AddInputPart(500, this);
    }
}
