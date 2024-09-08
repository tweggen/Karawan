using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace builtin.modules.satnav.desc;


/**
 * A navcluster gathers a subcluster that can be traversed.
 *
 * It might be grouped based on geometrical or logical properties.
 */
public class NavCluster
{
    /**
     * The unique id of this cluster.
     */
    public string Id;
    
    /**
     * Clusters may be set up dynamically, this one contains the link to
     * the parent one.
     */
    public NavCluster? ParentCluster;
        
    /*
     * Here comes the list of lanes connecting this cluster to another
     * cluster.
     */
    
    /**
     * Lanes connecting this cluster to another cluster.
     */
    public SortedDictionary<string, NavJunction> StartingLanes = new();
    public SortedDictionary<string, NavJunction> StoppingLanes = new();

    /**
     * A list of junctions by which this cluster is connected with
     * adjacent clusters.
     *
     * The proxy junctions also should be part of the standard navigable
     * #cluster content.
     */
    public List<NavJunction> ProxyJunctions = new();


    public Func<Task<NavClusterContent>> CreateClusterContentAsync()
    {
        return new(async () =>
        {
            return null;
        });
    }

    /**
     * The actual content of this cluster, it may be loaded on demand.
     */
    public NavClusterContent? Content;
}