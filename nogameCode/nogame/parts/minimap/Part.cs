
using System.Numerics;
using engine;
using engine.ross;

namespace nogame.parts.minimap;


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
    
    private DefaultEcs.Entity _eMiniMap;

    // For now, let it use the OSD camera.
    public uint MapCameraMask = 0x00010000;

    private engine.joyce.Material _materialMiniMap;
    
    
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

        {
            _eMiniMap = _engine.CreateEntity("nogame.parts.map.miniMap");
            _materialMiniMap = new();
            _materialMiniMap.EmissiveTexture =
                Implementations.Get<nogame.map.MapFramebuffer>().Texture;
            _materialMiniMap.HasTransparency = false;

            _engine.GetATransform().SetTransforms(
                _eMiniMap, true, MapCameraMask,
                new Quaternion(0f, 0f, 0f, 0f),
                new Vector3(-5f, 5f, -1f));
        }

    }

    private int _updateMinimapFrameCount = 0;
    private readonly int _updateMinimapCount = 4;

    private void _createNewMiniMap()
    {
        Vector2 uv0, u, v;
        uv0 = new Vector2(0.5f - 0.1f, 0.5f - 0.1f);
        u = new Vector2(0.2f, 0f);
        v = new Vector2(0f, 0.2f);

        float width = 4f;
        float height = 4f;
        Vector3 pos0 = new(-width/2f, -height/2, 0f);
        Vector3 x = new(width, 0f, 0f);
        Vector3 y = new(0f, height, 0f);
        
        engine.joyce.Mesh meshMiniMap = engine.joyce.Mesh.CreateListInstance();
        engine.joyce.mesh.Tools.AddQuadXYUV(meshMiniMap, pos0, x,y, uv0, u, v);
        engine.joyce.InstanceDesc jMiniMapInstanceDesc = new();
        jMiniMapInstanceDesc.Meshes.Add(meshMiniMap);
        jMiniMapInstanceDesc.MeshMaterials.Add(0);
        jMiniMapInstanceDesc.Materials.Add(_materialMiniMap);
        _eMiniMap.Set(new engine.joyce.components.Instance3(jMiniMapInstanceDesc));
    }

    public void _onLogicalFrame(object? sender, float dt)
    {
        if (0 != _updateMinimapFrameCount)
        {
            return;
        }
        ++_updateMinimapFrameCount;
        if (_updateMinimapFrameCount != _updateMinimapCount)
        {
            return;
        }

        _updateMinimapFrameCount = 0;

        _createNewMiniMap();

    }
    
    
    public void PartDeactivate()
    {
        _engine.RemovePart(this);
    }

    
    public void PartActivate(in Engine engine0, in IScene scene0)
    {
        _engine = engine0;
        _needResources();
        _engine.AddPart(-190, scene0, this);
        _engine.LogicalFrame += _onLogicalFrame;
    }
}
