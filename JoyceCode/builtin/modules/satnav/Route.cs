using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules.satnav.desc;
using engine;
using engine.world;
using SharpNav;
using SharpNav.Pathfinding;

namespace builtin.modules.satnav;


public class Route : IDisposable
{
    private Engine _engine;
    
    public NavMap NavMap { get; }

    private IWaypoint _a;
    private IWaypoint _b;

    private LocalPathfinder _pathfinder;

    
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
    public void Search(Action<List<NavLane>> onPath)
    {
        var listLanes = _pathfinder.Pathfind();
        _engine.Run(() => onPath(listLanes));
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

        var navCursors = await Task.WhenAll(
            NavMap.TopCluster.TryCreateCursor(v3StartCenter),
            NavMap.TopCluster.TryCreateCursor(v3EndCenter)
        );
        
        /*
         * Plan the initial route.
         */
        _pathfinder = new LocalPathfinder(navCursors[0].Lane.Start, navCursors[1].Lane.End);
    }


    public void Dispose()
    {
    }


    public Route(NavMap nm, IWaypoint a, IWaypoint b)
    {
        _engine = I.Get<Engine>();
        NavMap = nm;
        _a = a;
        _b = b;
    }
}
