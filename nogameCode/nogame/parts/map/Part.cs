using System.Numerics;
using engine;
using engine.ross;

namespace nogame.parts.map;


/**
 * Scene part "map" for the main gameplay scene.
 *
 * Displays a full-screen map, using the OSD camera.
 *
 * On activation, makes the map entities visible
 */
public class Part : IPart
{
    private object _lo = new();

    private engine.Engine _engine;
    
    private bool _createdResources = false;
    
    private DefaultEcs.Entity _eMapContainer;
    private DefaultEcs.Entity _eMap;
    private MemoryFramebuffer _memoryFramebuffer;

    // For now, let it use the OSD camera.
    public uint MapCameraMask = 0x00010000;
    public uint MapWidth = 1024;
    public uint MapHeight = 1024;

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

        _memoryFramebuffer = new engine.ross.MemoryFramebuffer(MapWidth, MapHeight);
        
        /*
         * Render the actual map data.
         */
        Implementations.Get<builtin.map.IMapProvider>().WorldMapCreateBitmap(_memoryFramebuffer);
        
        _eMapContainer = _engine.CreateEntity("nogame.parts.map.mapContainer");
        
        _eMap = _engine.CreateEntity("nogame.parts.map.map");
        
        engine.joyce.Mesh meshFramebuffer = engine.joyce.mesh.Tools.CreatePlaneMesh(
            new Vector2(16f, 16f));
        meshFramebuffer.UploadImmediately = true;
        engine.joyce.Texture textureFramebuffer = new(_memoryFramebuffer);
        textureFramebuffer.DoFilter = false;
        engine.joyce.Material materialFramebuffer = new();
        materialFramebuffer.UploadImmediately = true;
        materialFramebuffer.EmissiveTexture = textureFramebuffer;
        materialFramebuffer.HasTransparency = false;

        engine.joyce.InstanceDesc jInstanceDesc = new();
        jInstanceDesc.Meshes.Add(meshFramebuffer);
        jInstanceDesc.MeshMaterials.Add(0);
        jInstanceDesc.Materials.Add(materialFramebuffer);
        _eMap.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            
        _engine.GetATransform().SetTransforms(
            _eMap, false, MapCameraMask,
            new Quaternion(0f,0f,0f,1f),
            new Vector3(0f, 0f, -1f));

    }
    
    
    public void PartDeactivate()
    {
        _engine.RemovePart(this);
        _engine.GetATransform().SetVisible(_eMap, false);
    }

    
    public void PartActivate(in Engine engine0, in IScene scene0)
    {
        _engine = engine0;
        _needResources();
        _engine.GetATransform().SetVisible(_eMap, true);
        _engine.AddPart(-200, scene0, this);
    }


    public Part()
    {
        
    }
}