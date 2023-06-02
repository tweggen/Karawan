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
     * Default draw context.
     */
    private engine.draw.Context _drawContext;
    
    private uint _fbpos = 0;
    
    private void _updateFramebuffer()
    {
        ++_fbpos;
        var dc = _drawContext;
        dc.FillColor = 0x00000000;
        _framebuffer.ClearRectangle(dc, new Vector2(0,0), new Vector2(399,399));
        dc.FillColor = 0xffffffff;
        uint xofs = (2*_fbpos) % 300;
        _framebuffer.FillRectangle(dc, new Vector2(xofs+30, 30), new Vector2(xofs+70, 70));
        _framebuffer.FillRectangle(dc, new Vector2(30, 130), new Vector2(70, 170));
        dc.TextColor = 0xff22aaee;
        _framebuffer.DrawText(dc,
            new Vector2( 30, 100), new Vector2(45, 350 ),
            "Systems activated.");
    }
        
    private void _testFramebuffer()
    {
        DefaultEcs.Entity eFramebuffer;
            
        _drawContext = new engine.draw.Context();
        _framebuffer = new engine.ross.MemoryFramebuffer(400, 400);

        {
            eFramebuffer = _ecsWorld.CreateEntity();
            var posFramebuffer = new Vector3(0f, 35f, -4f);
            _aTransform.SetPosition(eFramebuffer, posFramebuffer);
            _aTransform.SetVisible(eFramebuffer, true);
            _aTransform.SetCameraMask(eFramebuffer, 0xffffffff);

            // engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreateCubeMesh(4f);
            engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreatePlaneMesh(
                new Vector2(8f, -8f),
                new Vector2(1f, 0f),
                new Vector2(-1f, 0f),
                new Vector2(0f, 1f)); 
            engine.joyce.Texture textureFramebuffer = new(_framebuffer);
            engine.joyce.Material materialFramebuffer = new();
            materialFramebuffer.EmissiveTexture = textureFramebuffer;
            materialFramebuffer.HasTransparency = true;

            engine.joyce.InstanceDesc jInstanceDesc = new();
            jInstanceDesc.Meshes.Add(meshFramebuffer);
            jInstanceDesc.MeshMaterials.Add(0);
            jInstanceDesc.Materials.Add(materialFramebuffer);
            eFramebuffer.Set(new engine.joyce.components.Instance3(jInstanceDesc));
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