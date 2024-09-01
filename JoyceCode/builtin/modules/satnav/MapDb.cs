using System;
using System.Collections.Generic;
using engine;
using engine.world;
using engine.world.components;
using SharpNav;
using SharpNav.Geometry;

namespace builtin.modules.satnav;


public class MapDB : AModule
{
    private ObjectFactory<int, Ref<NavMesh>> _factoryMeshes;


    private Ref<NavMesh> _createNavMesh(ClusterDesc cd)
    {
#if false

        return new Ref<NavMesh>();
#else
        List<engine.joyce.Mesh> listMeshes = new();
        var enumMeshes = _engine.GetEcsWorld().GetEntities()
            .With<ClusterId>()
            .With<engine.joyce.components.NavMesh>().AsEnumerable();
        foreach (var eNavMesh in enumMeshes)
        {
            listMeshes.AddRange(eNavMesh.Get<engine.joyce.components.NavMesh>().Meshes);
        }
        

        var settings = NavMeshGenerationSettings.Default;
        settings.AgentHeight = 1.7f;
        // settings.AgentWidth = 0.6f;

        var navMesh = NavMesh.Generate(tris, settings);
        return new Ref<NavMesh>(navMesh);
        
#endif
    }
    

    /**
     * Needs to run in logical thread!!
     */
    public Ref<NavMesh> FindNavMeshForCluster(ClusterDesc cd)
    {
        int id = cd.Id;

        return _factoryMeshes.FindAdd(id, (_) =>
        {
            /*
             * Create a navmesh for the given cluster id.
             */
            return _createNavMesh(cd);
        });
    }

    
    public void Dispose()
    {
        _factoryMeshes.Dispose();
    }
    
    
    public MapDB()
    {
        _factoryMeshes = new ();
    }
}