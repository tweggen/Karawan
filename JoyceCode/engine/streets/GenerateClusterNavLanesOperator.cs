#if false
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

    }
    

    public void ClusterOperatorApply(ClusterDesc clusterDesc)
    {
        var ctx = new Context()
        {
            ClusterDesc = clusterDesc,
            Ncc = new()
            {
                NavCluster = 
            }
        };

        
        Trace($"Creating navlanes for {clusterDesc.Name}");
        
        _computeNavClusterContent(ctx.ClusterDesc, ctx.Ncc);
        ctx.Ncc.Recompile();
    }

}
#endif
