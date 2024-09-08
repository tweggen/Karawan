using System.Threading.Tasks;
using builtin.modules.satnav.desc;
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
    private Task<NavClusterContent> _createClusterNavContentAsync(NavCluster ncTop, string id)
    {
        
    }

    
    /**
     * Create the top level cluster content by creating sub-NavClusters
     * for each of our clusters.
     */
    private Task<NavClusterContent> _createTopClusterContentAsync(NavCluster ncTop, string id)
    {
        var clusterList = I.Get<ClusterList>().GetClusterList();
        NavClusterContent ncc = new();
        
        foreach (var clusterDesc in clusterList)
        {
            NavCluster nc = new()
            {
                Id = clusterDesc.IdString,
                AABB = clusterDesc.AABB,
                ParentCluster = ncTop,
                CreateClusterContentAsync = _createClusterNavContentAsync,
                Content = null
            };
        }
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
            CreateClusterContentAsync = _createTopClusterContentAsync
        };
        
        I.Get<NavMap>().TopCluster = ncTop;
        

        Trace("GenerateNavMapOperator: Done.");
    });
    

    public GenerateNavMapOperator()
    {
    }
}