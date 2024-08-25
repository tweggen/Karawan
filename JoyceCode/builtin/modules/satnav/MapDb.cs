using System;
using System.Collections.Generic;
using engine;
using engine.world;
using SharpNav;

namespace builtin.modules.satnav;


public class MapDB : IDisposable
{
    private ObjectFactory<int, Ref<NavMesh>> _factoryMeshes;


    private Ref<NavMesh> _createNavMesh(ClusterDesc cd)
    {
        return new();
    }
    

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