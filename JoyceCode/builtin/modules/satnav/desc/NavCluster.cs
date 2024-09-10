using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using engine.geom;
using static engine.Logger;

namespace builtin.modules.satnav.desc;


/**
 * A navcluster gathers a subcluster that can be traversed.
 *
 * It might be grouped based on geometrical or logical properties.
 */
public class NavCluster
{
    private object _lo = new();
    
    /**
     * The unique id of this cluster.
     */
    public string Id;

    public AABB AABB;
    
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
    

    public async Task<NavCursor> TryCreateCursor(Vector3 v3Position)
    {
        NavClusterContent ncc = null;
        
        lock (_lo)
        {
            if (!AABB.Contains(v3Position))
            {
                return NavCursor.Nil;
            }

            if (Content == null)
            {
                if (null == CreateClusterContentAsync)
                {
                    return NavCursor.Nil;
                }

                if (null == _semCreate)
                {
                    /*
                     * We do not have content, so create the content creation
                     * semaphore.
                     */
                    _semCreate = new SemaphoreSlim(1);
                }
            }
            else
            {
                ncc = Content;
            }
        }

        if (ncc != null)
        {
            return await ncc.TryCreateCursor(v3Position);
        }

        _semCreate.Wait();

        lock (_lo)
        {
            ncc = Content;
        }

        if (null == ncc)
        {
            try
            {
                ncc = await CreateClusterContentAsync(this);
                ncc.Recompile();

                lock (_lo)
                {
                    Content = ncc;
                }
            }
            catch (Exception e)
            {
                Error($"Error creating cluster content for {Id} : {e}");
            }
        }

        /*
         * Looks like we did do not have cluster content for this one.
         * So load it, then try returning the cursor.
         */
        _semCreate.Release();

        if (ncc != null)
        {
            return await ncc.TryCreateCursor(v3Position);
        }
        else
        {
            return NavCursor.Nil;
        }
    }
    

    private SemaphoreSlim _semCreate = null;
    
    /**
     * How to create the NavCluster content if required
     */
    public Func<NavCluster, Task<NavClusterContent>> CreateClusterContentAsync;

    /**
     * The actual content of this cluster, it may be loaded on demand.
     */
    public NavClusterContent? Content;
}