using System.Numerics;
using engine;
using engine.draw.systems;
using engine.joyce;

namespace nogame.parts.osd;

public class Part : engine.IPart
{
    private readonly object _lo = new();
    private engine.Engine _engine = null;
    private engine.IScene _scene = null;

    private DefaultEcs.World _ecsWorld;
    private engine.transform.API _aTransform;

    /**
     * This is the standard framebuffer we draw into.
     */
    private engine.draw.IFramebuffer _framebuffer;
    
    /**
     * This is the 3d mesh textured with this framebuffer. 
     */
    DefaultEcs.Entity _eFramebuffer;

    /**
     * OSD rendering system
     */
    private engine.draw.systems.RenderOSDSystem _renderOSDSystem;
    
    /**
     * Default draw context.
     */
    private engine.draw.Context _drawContext;
    
    private uint _fbpos = 0;

    private readonly uint _width = 768;
    private readonly uint _height = 768*9/16;
    

    private void _setupOSD()
    {
            
        _drawContext = new engine.draw.Context();
        _framebuffer = new engine.ross.SkiaSharpFramebuffer("fbOsd", _width, _height);

        {
            _eFramebuffer = _engine.CreateEntity("Framebuffer");
            
            // engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreateCubeMesh(4f);
            engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreatePlaneMesh(
                new Vector2(2f, 2f*9f/16f));
            meshFramebuffer.UploadImmediately = true;
            engine.joyce.Texture textureFramebuffer = new(_framebuffer);
            textureFramebuffer.DoFilter = false;
            engine.joyce.Material materialFramebuffer = new();
            materialFramebuffer.UploadImmediately = true;
            materialFramebuffer.EmissiveTexture = textureFramebuffer;
            materialFramebuffer.HasTransparency = true;

            var jInstanceDesc = InstanceDesc.CreateFromMatMesh(new MatMesh(materialFramebuffer, meshFramebuffer));
            _eFramebuffer.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            
            _aTransform.SetTransforms(
                _eFramebuffer, true, 0x00010000,
                new Quaternion(0f,0f,0f,1f),
                new Vector3(0f, 0f, 0f));
        }
    }

    private uint _frameCounter = 0;
    private readonly uint _renderSubDiv = 3;
    private float _dtTotal = 0f;
    private void _onPhysical(object? sender, float dt)
    {
        ++_frameCounter;
        _dtTotal += dt;
        if (_frameCounter < _renderSubDiv)
        {
            return;
        }

        var dtTotal = _dtTotal;
        _dtTotal = 0f;
        _frameCounter = 0;
        _renderOSDSystem.Update(dtTotal);
    }

    
    public void PartDeactivate()
    {
        _engine.RemovePart(this);
        lock (_lo)
        {
            _scene = null;
        }
        _renderOSDSystem.Dispose();
        _renderOSDSystem = null;
    }

    
    public void PartActivate(in Engine engine0, in IScene scene0)
    {
        lock (_lo)
        {
            _engine = engine0;
            _scene = scene0;
        }

        /*
         * local shortcuts.
         */
        _ecsWorld = _engine.GetEcsWorld();
        _aTransform = _engine.GetATransform();

        _renderOSDSystem = new RenderOSDSystem(_engine);

        _engine.AddPart(-100f, scene0, this);

        _engine.PhysicalFrame += _onPhysical;
        
        _setupOSD();
        _renderOSDSystem.SetFramebuffer(_framebuffer);
        // _updateFramebuffer();
    }

    public Part()
    {
    }
}