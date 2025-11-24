using System;
using System.Numerics;
using builtin.tools;
using engine.streets;
using engine.world;
using static engine.Logger;

namespace engine;

public class Placer
{
    private Lazy<ClusterList> _clusterList = new(I.Get<ClusterList>());
    private Lazy<engine.world.Loader> _worldLoader = new(I.Get<engine.world.MetaGen>().Loader);


    /**
     * Try to place something according to the placement description.
     * Depending on the current load state, and what is loaded and generated,
     * this may or may not be possible.
     */
    public bool TryPlacing(
        RandomSource _rnd, 
        in PlacementContext? pc, 
        in PlacementDescription plad, 
        out PositionDescription pod)
    {
        bool lookupCluster = false;
        bool lookupQuarter = false;
        bool lookupStreetPoint = false;
        pod = null;
        
        /*
        * Look what we need to place it.
        */
        switch (plad.ReferenceObject)
        {
            case PlacementDescription.Reference.StreetPoint:
                lookupStreetPoint = true;
                lookupCluster = true;
                break; 
            case PlacementDescription.Reference.Quarter:
                lookupQuarter = true;
                lookupCluster = true;
                break;
            case PlacementDescription.Reference.Cluster:
                lookupCluster = true;
                break; 
            default:
            case PlacementDescription.Reference.World:
                /*
                * Trivial case, no reference required.  
                */
                break;
        }

        
        Vector3 v3ReferenceAccu = Vector3.Zero;
        ClusterDesc? cd = null;
        Quarter? q = null;
        StreetPoint? sp = null;
        
        /*
         * now lookup in inverse order.
         */
        if (lookupCluster)
        {
            ClusterList clusterList = _clusterList.Value;
            
            switch (plad.WhichCluster)
            {
                case PlacementDescription.ClusterSelection.AnyCluster:
                {
                    var listClusters = clusterList.GetClusterList();
                    int l = listClusters.Count;
                    if (0 == l) return false;
                    cd = listClusters[_rnd.GetInt(l)];
                    break;
                }
                case PlacementDescription.ClusterSelection.CurrentCluster:
                    if (pc == null) return false;
                    if (pc.CurrentCluster == null) return false;
                    cd = pc.CurrentCluster;
                    break;
                case PlacementDescription.ClusterSelection.ConnectedCluster:
                    ErrorThrow<NotImplementedException>("Selecting a connected cluster is not implemented yet.");
                    break;
                default:
                    return false;
            }

            v3ReferenceAccu += cd.Pos;
            pod.ClusterId = cd.IdString;
            pod.ClusterName = cd.Name;
        }

        if (lookupQuarter)
        {
            /*
            * Of course, a quarter always requires a previously selected cluster. 
            */
            if (cd == null)
            {
                return false;
            }

            switch (plad.WhichQuarter)
            {
                case PlacementDescription.QuarterSelection.AnyQuarter:
                {
                    var listQuarters = cd.QuarterStore().GetQuarters();
                    int l = listQuarters.Count;
                    if (0==l) return false;
                    q = listQuarters[_rnd.GetInt(l)];
                    break;
                }
                case PlacementDescription.QuarterSelection.CurrentQuarter:
                    if (pc == null) return false;
                    if (pc.CurrentQuarter == null) return false;
                    q = pc.CurrentQuarter;
                    break;
                case PlacementDescription.QuarterSelection.NearbyQuarter:
                    ErrorThrow<NotImplementedException>("Selecting a nearby quarter is not implemented yet.");
                    break;
                default:
                    return false;
            }

            v3ReferenceAccu += q.GetCenterPoint3();
            pod.QuarterName = q.GetDebugString();
        }

        if (lookupStreetPoint)
        {
            /*
             * Also a streetpoint refers to a cluster.
             */
            if (cd == null)
            {
                return false;
            }

            /*
             * Today we support random streetpoint with requested
             * attribute only.
             */
            {
                var listStreetPoints = cd.StrokeStore().GetStreetPoints();
                int l = listStreetPoints.Count;
                if (0 == l) return false;
                sp = listStreetPoints[_rnd.GetInt(l)];
            }

            v3ReferenceAccu += sp.Pos3;
            pod.StreetPointId = sp.Id;
        }
        
        pod.Position = v3ReferenceAccu;

        return true;
    }
}