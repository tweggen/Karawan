using System;
using System.Collections.Generic;
using engine;
using engine.world;
using SharpNav;

namespace builtin.modules.satnav;


public class MapDb : IDisposable
{
    private ObjectFactory<int, Ref<NavMesh>> _factoryMeshes;


    public Ref<NavMesh> FindNavMeshForCluster(ClusterDesc cd)
    {
        int id = cd.Id;

        return _factoryMeshes.FindAdd(id, (_) =>
        {
            /*
             * Create a navmesh for the given cluster id.
             */
            return null;
        });
    }


    public void Dispose()
    {
        _factoryMeshes.Dispose();
    }
    
    
    public MapDb()
    {
        _factoryMeshes = new ();
    }
}