
using System;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.ross;
using engine.world;

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
    private DefaultEcs.Entity _ePlayerEntity;

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
            _materialMiniMap.HasTransparency = true;
            _materialMiniMap.UploadImmediately = true;

            _engine.GetATransform().SetTransforms(
                _eMiniMap, true, MapCameraMask,
                new Quaternion(0f, 0f, 0f, 0f),
                new Vector3(-1f+0.15f, 9f/16f-0.12f, -1f));
        }

    }

    private int _updateMinimapFrameCount = 0;
    private readonly int _updateMinimapCount = 4;

    private void _createNewMiniMap()
    {
        DefaultEcs.Entity ePlayerEntity;
        lock (_lo)
        {
            ePlayerEntity = _ePlayerEntity;
        }

        Vector3 pos;
        
        if (!ePlayerEntity.IsAlive)
        {
            pos = new Vector3(0f, 0f, 0f);
        }
        else
        {
            Matrix4x4 m = ePlayerEntity.Get<engine.transform.components.Transform3ToWorld>().Matrix;
            pos = m.Translation;
        }

        float sourceWidth = 3000f;
        float sourceHeight = 3000f;
        float realPosX = Single.Min(MetaGen.MaxWidth - sourceWidth / 2f, Single.Max(-MetaGen.MaxWidth + sourceWidth / 2f, pos.X));
        float realPosY = Single.Min(MetaGen.MaxHeight - sourceHeight / 2f, Single.Max(-MetaGen.MaxHeight + sourceHeight / 2f, pos.Z));
        float centerUVX = realPosX / (MetaGen.MaxWidth) + 0.5f;
        float centerUVY = realPosY / (MetaGen.MaxHeight) + 0.5f;
        float widthUV = sourceWidth / MetaGen.MaxWidth;
        float heightUV = sourceHeight / MetaGen.MaxHeight;
        
        Vector2 uv0, u, v;
        uv0 = new Vector2(centerUVX - widthUV/2f, centerUVY + heightUV/2f);
        u = new Vector2(widthUV, 0f);
        v = new Vector2(0f, -heightUV);

        float width = 0.2f;
        float height = 0.2f;
        Vector3 pos0 = new(-width/2f, -height/2, 0f);
        Vector3 x = new(width, 0f, 0f);
        Vector3 y = new(0f, height, 0f);
        
        engine.joyce.Mesh meshMiniMap = engine.joyce.Mesh.CreateListInstance();
        meshMiniMap.UploadImmediately = true;
        engine.joyce.mesh.Tools.AddQuadXYUV(meshMiniMap, pos0, x,y, uv0, u, v);
        engine.joyce.InstanceDesc jMiniMapInstanceDesc = new();
        jMiniMapInstanceDesc.Meshes.Add(meshMiniMap);
        jMiniMapInstanceDesc.MeshMaterials.Add(0);
        jMiniMapInstanceDesc.Materials.Add(_materialMiniMap);
        _eMiniMap.Set(new engine.joyce.components.Instance3(jMiniMapInstanceDesc));
    }

    
    public void _onLogicalFrame(object? sender, float dt)
    {
        ++_updateMinimapFrameCount;
        if (_updateMinimapFrameCount != _updateMinimapCount)
        {
            return;
        }

        _updateMinimapFrameCount = 0;

        _createNewMiniMap();
    }


    private void _onPlayerEntityChanged(object? sender, DefaultEcs.Entity newPlayerEntity)
    {
        lock (_lo)
        {
            if (_ePlayerEntity != newPlayerEntity)
            {
                _ePlayerEntity = newPlayerEntity;
            }
        }
    }
    
    public void PartDeactivate()
    {
        _engine.LogicalFrame -= _onLogicalFrame;
        _engine.OnPlayerEntityChanged -= _onPlayerEntityChanged;
        _engine.RemovePart(this);
    }

    
    public void PartActivate(in Engine engine0, in IScene scene0)
    {
        _engine = engine0;
        _needResources();
        _createNewMiniMap();
        _engine.AddPart(-190, scene0, this);
        _engine.LogicalFrame += _onLogicalFrame;
        _engine.OnPlayerEntityChanged += _onPlayerEntityChanged;
    }
}
