using System.Numerics;
using engine;

namespace nogame.osd;

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
     * Default draw context.
     */
    private engine.draw.Context _drawContext;
    
    private uint _fbpos = 0;

    private readonly uint _width = 512;
    private readonly uint _height = 256;
    
    private void _updateFramebuffer()
    {
        ++_fbpos;
        var dc = _drawContext;
        dc.FillColor = 0x00000000;
        _framebuffer.ClearRectangle(dc, new Vector2(0,0), new Vector2(_width-1,_height-1));
        dc.FillColor = 0xffffffff;
        uint xofs = (2*_fbpos) % 300;
        _framebuffer.FillRectangle(dc, new Vector2(xofs+30, 30), new Vector2(xofs+30+20, 30+20));
        _framebuffer.FillRectangle(dc, new Vector2(30, 130), new Vector2(30+20, 130+20));
        dc.TextColor = 0xff22aaee;
        _framebuffer.DrawText(dc,
            new Vector2( 30, 100), new Vector2(45, _height-50 ),
            "Systems activated.");
    }
        
    private void _testFramebuffer()
    {
            
        _drawContext = new engine.draw.Context();
        _framebuffer = new engine.ross.MemoryFramebuffer(_width, _height);

        {
            _eFramebuffer = _engine.CreateEntity("Framebuffer");
            var posFramebuffer = new Vector3(0f, 0f, 50f);

            // engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreateCubeMesh(4f);
            engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreatePlaneMesh(
                new Vector2(16f, 8f));
            meshFramebuffer.UploadImmediately = true;
            engine.joyce.Texture textureFramebuffer = new(_framebuffer);
            textureFramebuffer.DoFilter = false;
            engine.joyce.Material materialFramebuffer = new();
            materialFramebuffer.UploadImmediately = true;
            materialFramebuffer.EmissiveTexture = textureFramebuffer;
            materialFramebuffer.HasTransparency = true;

            engine.joyce.InstanceDesc jInstanceDesc = new();
            jInstanceDesc.Meshes.Add(meshFramebuffer);
            jInstanceDesc.MeshMaterials.Add(0);
            jInstanceDesc.Materials.Add(materialFramebuffer);
            _eFramebuffer.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            
            _aTransform.SetTransforms(
                _eFramebuffer, true, 0x00010000,
                new Quaternion(0f,0f,0f,1f),
                new Vector3(0f, 0f, 0f));
            //_aTransform.SetPosition(_eFramebuffer, posFramebuffer);
            //_aTransform.SetVisible(_eFramebuffer, true);
            //_aTransform.SetCameraMask(_eFramebuffer, 0x00010000);

            
        }
    }

    
    public void PartDeactivate()
    {
        lock (_lo)
        {
            _engine.RemovePart(this);
            _scene = null;
        }
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

        _engine.AddPart(-100f, scene0, this);
        
        _testFramebuffer();
        _updateFramebuffer();
    }

    public Part()
    {
    }
}