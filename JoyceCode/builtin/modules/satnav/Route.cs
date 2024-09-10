using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.modules.satnav.desc;
using engine;
using engine.world;
using SharpNav;
using SharpNav.Pathfinding;

namespace builtin.modules.satnav;

public class Route : IDisposable
{
    public NavMap NavMap { get; }

    private IWaypoint _a;
    private IWaypoint _b;

    
    public IWaypoint A
    {
        get
        {
            return _a;
        }
    }

    
    public IWaypoint B
    {
        get
        {
            return _b;
        }
    }


    private static float _heuristicWeight(NavJunction njStart, NavJunction njEnd)
    {
        /*
         * We use the orthogonal distance 
         */
        Vector3 v3Diff = njEnd.Position - njStart.Position;
        return Single.Abs(v3Diff.X) + Single.Abs(v3Diff.Y) + Single.Abs(v3Diff.Z);
    }
    
    
    /**
     * Iterate though the list to find the target navJunction.
     *
     * First, find the first common parent of source and target.
     * The look, within the scope of the parent, how we can migrate
     * from the source topmost to the destination topmost cluster.
     *
     * Then, find the route from the children to the parent proxy
     * junctions.
     */
    private void Search()
    {
        
    }


    public void Suspend()
    {
        
    }
    
    
    public async void Activate()
    {
        Vector3 v3StartCenter = _a.GetLocation();
        // Vector3 v3StartExtents = new(10f, 10f, 10f);
        
        Vector3 v3EndCenter = _b.GetLocation();
        // Vector3 v3EndExtents = new(10f, 10f, 10f);

        var ncuStart = await NavMap.TopCluster.TryCreateCursor(v3StartCenter);
        var ncuEnd = await NavMap.TopCluster.TryCreateCursor(v3EndCenter);
        
        /*
         * Plan the initial route.
         */
        // var nrInitial = await NavMap
    }


    public void Dispose()
    {
    }


    public Route(NavMap nm, IWaypoint a, IWaypoint b)
    {
        NavMap = nm;
        _a = a;
        _b = b;
    }
}
