using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using engine;
using engine.world;
using engine.news;
using static engine.Logger;

namespace builtin.map;

/**
 * This viewer requests the 2d fragment data for rendering the map.
 *
 * The current implementation looks for the map rectangle.
 * If it is below a certain threshold, all fragments within are requested.
 */
public class MapViewer : AModule, IViewer
{
    private engine.geom.AABB _aabbMap = new();

    private object _lo = new();
    
    /**
     * We remember the most recent AABB.
     */
    private SortedDictionary<string, engine.geom.AABB> _mapRanges = new ();
    
    /**
     * Report and predict visibility for the player's entity.
     */
    public void GetVisibleFragments(ref IList<FragmentVisibility> lsVisib)
    {
        /*
         * Iterate through all reported map ranges and report them. 
         */
        lock (_lo)
        {
            foreach (var kvp in _mapRanges)
            {
                engine.geom.AABB aabb = kvp.Value;
                
                /*
                 * Generate requests one fragment in excess. 
                 */
                aabb.Extend(MetaGen.FragmentSize);
                //Trace($"Found {kvp.Value}");

                var v3Min = aabb.AA / MetaGen.FragmentSize;
                var v3Max = aabb.BB / MetaGen.FragmentSize;
                int xmin = (int) Single.Floor(v3Min.X + 0.5f);
                int xmax = (int) Single.Floor(v3Max.X + 0.5f);
                int zmin = (int) Single.Floor(v3Min.Z + 0.5f);
                int zmax = (int) Single.Floor(v3Max.Z + 0.5f);
                for (int z = zmin; z <= zmax; ++z)
                {
                    for (int x = xmin; x <= xmax; ++x)
                    {
                        lsVisib.Add(new ()
                        {
                            How = FragmentVisibility.Visible2dNow,
                            I = (short)x,
                            K = (short)z
                        });
                    }
                }
            }
        }
    }


    private void _handleMapRange(Event ev)
    {
        MapRangeEvent mrev = ev as MapRangeEvent;
        
        /*
         * mrev AABB contains the map range we want to display.
         * We just keep it in our dictionary.
         */
        lock (_lo)
        {
            if (mrev.AABB.IsEmpty())
            {
                _mapRanges.Remove(ev.Code);
            }
            else
            {
                _mapRanges[ev.Code] = mrev.AABB;
            }
        }
    }


    public override void ModuleDeactivate()
    {
        I.Get<engine.world.MetaGen>().Loader.RemoveViewer(this);
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate()
    {
        base.ModuleActivate();
        
        I.Get<SubscriptionManager>().Subscribe(Event.MAP_RANGE_EVENT, _handleMapRange);
        _engine.AddModule(this);

        I.Get<engine.world.MetaGen>().Loader.AddViewer(this);
    }
}
