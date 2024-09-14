using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using builtin.modules.satnav.desc;
using DefaultEcs;
using engine;
using engine.world;
using static engine.Logger;


namespace builtin.modules.satnav;

public class GenerateNavMapOperator : engine.world.IWorldOperator
{
    /**
     * Create the content for the individual clusters below the top
     * level cluster.
     */
    private Task<NavClusterContent> _createClusterNavContentAsync(ClusterDesc clusterDesc, NavCluster ncTop)
    {
        Trace($"Loading cluster {clusterDesc.Name}");
        
        NavClusterContent ncc = new NavClusterContent()
        {
            Cluster = ncTop
        };
        
        SortedDictionary<int, NavJunction> dictJunctions = new();
        foreach (var streetPoint in clusterDesc.StrokeStore().GetStreetPoints())
        {
            NavJunction nj = new()
            {
                Position = streetPoint.Pos3 + clusterDesc.Pos with { Y = clusterDesc.AverageHeight + MetaGen.ClusterNavigationHeight },
                StartingLanes = new(),
                EndingLanes = new()
            };
            dictJunctions[streetPoint.Id] = nj;
            ncc.Junctions.Add(nj);
        }

        foreach (var stroke in clusterDesc.StrokeStore().GetStrokes())
        {
            try
            {
                var njA = dictJunctions[stroke.A.Id];
                var njB = dictJunctions[stroke.B.Id];

                NavLane nlForth = new()
                {
                    Start = njA,
                    End = njB,
                    Length = stroke.Length
                };
                njA.StartingLanes.Add(nlForth);
                njB.EndingLanes.Add(nlForth);
                ncc.Lanes.Add(nlForth);

                NavLane nlBack = new()
                {
                    Start = njB,
                    End = njA,
                    Length = stroke.Length
                };
                njA.EndingLanes.Add(nlBack);
                njB.StartingLanes.Add(nlBack);
                ncc.Lanes.Add(nlBack);
            }
            catch (Exception e)
            {
                Trace($"Exception adding navlane: {e}");
            }
        }

        return Task.FromResult(ncc);
    }

    
    /**
     * Create the top level cluster content by creating sub-NavClusters
     * for each of our clusters.
     */
    private Task<NavClusterContent> _createTopClusterContentAsync(NavCluster ncTop)
    {
        Trace($"Loading top level cluster");

        NavClusterContent ncc = new NavClusterContent()
        {
            Cluster = ncTop
        };
        
        var clusterList = ClusterList.Instance().GetClusterList();

        foreach (var clusterDesc in clusterList)
        {
            NavCluster nc = new()
            {
                Id = clusterDesc.IdString,
                AABB = clusterDesc.AABB,
                ParentCluster = ncTop,
                CreateClusterContentAsync = (NavCluster nc) => _createClusterNavContentAsync(clusterDesc, nc),
                Content = null
            };

            ncc.Clusters.Add(nc);
        }

        return Task.FromResult(ncc);
    }
    
    
    public string WorldOperatorGetPath()
    {
        return "builtin.modules.satnav/GenerateNavMapOperator";
    }


    public System.Func<Task> WorldOperatorApply() => new (async () =>
    {
        NavCluster ncTop = new()
        {
            Id = "Top",
            CreateClusterContentAsync = _createTopClusterContentAsync,
            AABB = MetaGen.AABB
        };
        
        I.Get<NavMap>().TopCluster = ncTop;
        

        Trace("GenerateNavMapOperator: Done.");
    });
    

    public GenerateNavMapOperator()
    {
    }
}