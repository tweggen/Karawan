using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace builtin.modules.satnav.desc;


/**
 * This represents the actual navigable graph of a cluster.
 */
public class NavClusterContent
{
    /**
     * The clusters contained inside this cluster.
     */
    public List<NavCluster> Clusters = new();
    
    /**
     * The actual junctions contained in this cluster.
     */
    public List<NavJunction> Junctions = new();
    
    /**
     * The lanes of this cluster.
     */
    public List<NavLane> Lanes = new();


    public Task<NavCursor> TryCreateCursor(Vector3 v3Position)
    {
        return Task.FromResult(new NavCursor());
    }
}
