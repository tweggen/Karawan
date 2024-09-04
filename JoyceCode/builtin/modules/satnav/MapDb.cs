using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using engine;
using engine.world;
using engine.world.components;
using SharpNav;
using SharpNav.Geometry;
using static engine.Logger;

namespace builtin.modules.satnav;


public class MapDB : AModule
{
    private ObjectFactory<int, Ref<NavMesh>> _factoryMeshes;


    private static IEnumerable<Triangle3> _fromJoyceMesh(engine.joyce.Mesh jMesh)
    {
        Triangle3 tri;
        int nTris = jMesh.Indices.Count / 3;

        for (int i = 0; i < nTris; i++)
        {
            var indA = jMesh.Indices[i * 3];
            var indB = jMesh.Indices[i * 3 + 1];
            var indC = jMesh.Indices[i * 3 + 2];

            tri.A = jMesh.Vertices[(int)indA];
            tri.B = jMesh.Vertices[(int)indB];
            tri.C = jMesh.Vertices[(int)indC];

            yield return tri;
        }
    }

    private Ref<NavMesh> _createNavMesh(ClusterDesc cd)
    {
        List<engine.joyce.Mesh> listMeshes = new();
        var enumMeshes = _engine.GetEcsWorld().GetEntities()
            .With<ClusterId>()
            .With<engine.joyce.components.NavMesh>().AsEnumerable();

        int nNavMeshEntities = 0;
        foreach (var eNavMesh in enumMeshes)
        {
            ++nNavMeshEntities;
            var fragmentId = eNavMesh.Get<FragmentId>();
            Trace($"navmesh for fragmentid = {fragmentId}");
            listMeshes.AddRange(eNavMesh.Get<engine.joyce.components.NavMesh>().Meshes);
        }

        List<IEnumerable<Triangle3>> listTriEnums = new();

        int nTris = 0;
        foreach (var jMesh in listMeshes)
        {
            nTris += jMesh.Indices.Count / 3;
            listTriEnums.Add(_fromJoyceMesh(jMesh));
        }

        IEnumerable<Triangle3> listTris = listTriEnums.SelectMany(tri => tri);
        
        var settings = NavMeshGenerationSettings.Default;
        settings.AgentHeight = 1.7f;
        // settings.AgentWidth = 0.6f;

        var navMesh = NavMesh.Generate(listTris, settings);
        return new Ref<NavMesh>(navMesh);
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