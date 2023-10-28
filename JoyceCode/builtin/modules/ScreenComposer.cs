using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.joyce;

namespace builtin.modules;


/*
 * Define one layer of screen composing.
 */
internal class ScreenLayer
{
    public string Name;
    public Matrix4x4 Transformation;
    public InstanceDesc Instance;
    public engine.joyce.Renderbuffer Renderbuffer;
    public Entity ELayer;
}


/*
 * Implement a screen composer.
 *
 * Adds a camera and a couple of layers to render the textures given.
 */
public class ScreenComposer : AModule
{
    private Entity _eCamera;


    private SortedDictionary<string, ScreenLayer> _mapLayers = new();
    private List<ScreenLayer> _listLayers = new();

    public uint CameraMask { get; set; } = 0x00400000;

    
    private void _createLayer(ScreenLayer l)
    {
        _engine.QueueEntitySetupAction($"ScreenLayer_{l.Name}", e =>
        {
            l.ELayer = e;
            var mesh = engine.joyce.mesh.Tools.CreatePlaneMesh(
                $"ScreenLayer_{l.Name}", 
                new(2f, 2f * (float)l.Renderbuffer.Height / (float) l.Renderbuffer.Width));
            var material = new Material(){
                HasTransparency = true,
                EmissiveTexture = new Texture(l.Renderbuffer.TextureName)
                //AlbedoColor = 0xffffffff,
                //EmissiveColor = 0xffffffff
            };
            l.Instance = InstanceDesc.CreateFromMatMesh(new MatMesh(material, mesh), 1000f);
            e.Set(new engine.joyce.components.Instance3(l.Instance));
            e.Set(new engine.joyce.components.Transform3ToWorld(CameraMask, l.Transformation));
        });
    }


    private void _addLayer(int targetIndex, ScreenLayer l)
    {
        lock (_lo)
        {
            _listLayers.Insert(targetIndex, l);
            _mapLayers.Add(l.Name, l);
        }
        _createLayer(l);
    }


    private void _removeLayer(ScreenLayer l)
    {
        lock (_lo)
        {
            _listLayers.Remove(l);
            _mapLayers.Remove(l.Name);
        }
    }

    private ScreenLayer _findLayer(string name)
    {
        lock (_lo)
        {
            return _mapLayers[name];
        }
    }
    
        
    public virtual void Dispose()
    {
        base.Dispose();
    }
    

    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public void SetLayerVisible(string name, bool isVisible)
    {
    }
    
    
    public void RemoveLayer(string name)
    {
        var l = _findLayer(name);
        _removeLayer(l);
    }
    
    
    public void AddLayer(string name, int targetIndex, Renderbuffer jRenderbuffer)
    {
        ScreenLayer l = new()
        {
            Name = name,
            Renderbuffer = jRenderbuffer,
            Transformation = Matrix4x4.CreateScale(1f, -1f, 1f)
        };
        _addLayer(targetIndex, l);
    }
    
    
    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);

        _engine.AddModule(this);

        _eCamera = _engine.CreateEntity("ScreenLayerCamera");
        _eCamera.Set(new engine.joyce.components.Camera3()
        {
            Angle = 0f,
            /*
             * Configure a camera with the width of 1 and a centered aspect ratio
             * as the screen has.
             */
            NearFrustum = 1f / Single.Tan(30f * Single.Pi / 180f), 
            FarFrustum = 100f,
            CameraMask = this.CameraMask,
            /*
             * Render directly on screen
             */
            Renderbuffer = null
        });
        I.Get<engine.joyce.TransformApi>().SetPosition(_eCamera, new Vector3(0f, 0f, 14f));
    }
}