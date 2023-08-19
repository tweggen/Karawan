using System.Numerics;
using engine;
using engine.joyce;


namespace nogame.modules.map;


/**
 * Scene part "map" for the main gameplay scene.
 *
 * Displays a full-screen map, using the OSD camera.
 *
 * On activation, makes the map entities visible
 */
public class Module : IModule
{
    private object _lo = new();

    private engine.Engine _engine;
    
    private bool _createdResources = false;
    
    private DefaultEcs.Entity _eMap;
   

    // For now, let it use the OSD camera.
    public uint MapCameraMask = 0x00010000;


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
            _engine.GetATransform().SetTransforms(
                _eMap, false, MapCameraMask,
                new Quaternion(0f,0f,0f,1f),
                new Vector3(0f, 0f, -1f));
        }
    }


    public void Dispose()
    {
        _eMap.Dispose();
        _createdResources = false;
    }
    
    
    public void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _engine.GetATransform().SetVisible(_eMap, false);
    }

    
    public void ModuleActivate(Engine engine0)
    {
        _engine = engine0;
        _needResources();
        _engine.GetATransform().SetVisible(_eMap, true);
        _engine.AddModule(this);
    }

}