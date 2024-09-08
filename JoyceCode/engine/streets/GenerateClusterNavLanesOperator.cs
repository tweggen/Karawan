using System;
using System.Collections.Generic;
using builtin.modules.satnav.desc;
using engine.world;
using static engine.Logger;

namespace engine.streets;

public class GenerateClusterNavLanesOperator : IClusterOperator
{
    private class Context
    {
        public ClusterDesc ClusterDesc;
        public NavClusterContent Ncc;
    }

    
    private void _computeNavClusterContent(ClusterDesc cd, NavClusterContent ncc)
    {

        SortedDictionary<int, NavJunction> dictJunctions = new();
        foreach (var streetPoint in cd.StrokeStore().GetStreetPoints())
        {
            NavJunction nj = new();
            dictJunctions[streetPoint.Id] = nj;
            ncc.Junctions.Add(nj);
        }

        foreach (var stroke in cd.StrokeStore().GetStrokes())
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
                ncc.Lanes.Add(nlForth);

                NavLane nlBack = new()
                {
                    Start = njB,
                    End = njA,
                    Length = stroke.Length
                };
                ncc.Lanes.Add(nlBack);
            }
            catch (Exception e)
            {
                Trace($"Exception adding navlane: {e}");
            }
        }
    }
    

    public void ClusterOperatorApply(ClusterDesc clusterDesc)
    {
        var ctx = new Context()
        {
            ClusterDesc = clusterDesc,
            Ncc = new()
        };

        
        Trace($"Creating navlanes for {clusterDesc.Name}");
        
        _computeNavClusterContent(ctx.ClusterDesc, ctx.Ncc);
    }

}