using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules.satnav.desc;
using engine.navigation;
using engine;
using static engine.Logger;


namespace builtin.modules.satnav;


public class Route : IDisposable
{
    private static readonly engine.Dc _dc = engine.Dc.Satnav;
    private Engine _engine;

    public NavMap NavMap { get; }

    /**
     * What is travelling this route.
     *
     * Both the cursors and the pathfinder take it, and they have to take the SAME one: a
     * cursor filters the lanes near a point by type and returns Nil rather than handing
     * back a lane of the wrong kind, so a car cursor into a pedestrian pathfinder does not
     * merely route badly, it fails to find a start at all.
     */
    public TransportationType TransportType { get; }

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
        I.Get<Engine>().Run(async () =>
        {
            try
            {
                /*
                 * The planning itself is in RoutePlan, which needs no engine and is
                 * therefore exercised. What is left here is the thread hopping.
                 */
                var listLanes = await RoutePlan.PlanAsync(
                    NavMap.TopCluster, _a.GetLocation(), _b.GetLocation(), TransportType);

                if (null != listLanes && listLanes.Count > 0)
                {
                    _engine.Run(() => onPath(listLanes));
                }
            }
            catch (Exception e)
            {
                Error(_dc, $"Exception tracing path: {e}");
            }
        });
    }


    public void Suspend()
    {
        
    }
    
    
    public async void Activate()
    {
    }


    public void Dispose()
    {
    }


    public Route(NavMap nm, IWaypoint a, IWaypoint b, TransportationType transportType)
    {
        _engine = I.Get<Engine>();
        NavMap = nm;
        _a = a;
        _b = b;
        TransportType = transportType;
    }
}
