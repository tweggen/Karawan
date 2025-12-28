using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        pod = new();
        
        /*
        * Look what we need to place it.
        */
        switch (plad.ReferenceObject)
        {
            case PlacementDescription.Reference.StreetPoint:
                lookupStreetPoint = true;
                lookupCluster = true;
                if (plad.WhichQuarter != PlacementDescription.QuarterSelection.IgnoreQuarter)
                {
                    lookupQuarter = true;
                }
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
        Fragment? fragment = null;
        ClusterDesc? cd = null;
        Quarter? q = null;
        StreetPoint? sp = null;
        
        /*
         * now lookup in inverse order.
         */

        /*
         * Do we have a fragment constraint?
         */
        if (plad.WhichFragment == PlacementDescription.FragmentSelection.CurrentFragment && pc != null)
        {
            fragment = pc.CurrentFragment;
        }
        
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

            /*
             * If the result is supposed to come from a given fragment and the cluster is outside the
             * fragment, return.
             */
            if (fragment != null)
            {
                if (!cd.AABB.Intersects(fragment.AABB)) return false;
            }

            v3ReferenceAccu += cd.Pos;
            pod.ClusterDesc = cd;
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
                    /*
                     * If we have a fragment constraint, we can only chose from the
                     * quarters inside this fragment.
                     */
                    
                    IReadOnlyList<Quarter> listQuarters;
                    if (fragment != null)
                    {
                        listQuarters = cd.QuarterStore().QueryQuarters(fragment.AABB, 0, 0);
                    } else
                    {
                        listQuarters = cd.QuarterStore().GetQuarters();
                    }

                    int l = listQuarters.Count;
                    if (0==l) return false;
                    q = listQuarters[_rnd.GetInt(l)];
                    break;
                }
                
                case PlacementDescription.QuarterSelection.CurrentQuarter:
                    if (pc == null) return false;
                    if (pc.CurrentQuarter == null) return false;
                    q = pc.CurrentQuarter;

                    /*
                     * Accept only quarters that are inside the fragment.
                     */
                    if (fragment != null)
                    {
                        if (!fragment.AABB.Contains(q.GetCenterPoint3())) return false;
                    }
                    break;
                
                case PlacementDescription.QuarterSelection.NearbyQuarter:
                    ErrorThrow<NotImplementedException>("Selecting a nearby quarter is not implemented yet.");
                    break;
                
                default:
                    return false;
            }

            v3ReferenceAccu += q.GetCenterPoint3();
            pod.Quarter = q;
            pod.QuarterName = q.GetDebugString();
        }

        /*
         * By convention, we ignore fragment constraints in street point lookups.
         */
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
             * Are we randomly chosing street points in the acceptable range, or are we restricted
             * to a given quarter?
             */

            if (null == q)
            {
                /*
                 * OK, no quarter selected, so select any StreetPoint that matches
                 * the criteria (i.e. fragment condition)
                 */
                IReadOnlyList<StreetPoint> listStreetPoints;
                if (null != fragment)
                {
                    listStreetPoints = cd.StrokeStore().QueryStreetPoints(fragment.AABB);
                } 
                else
                {
                    listStreetPoints = cd.StrokeStore().GetStreetPoints();
                }
                int l = listStreetPoints.Count;
                if (0 == l) return false;
                sp = listStreetPoints[_rnd.GetInt(l)];

                pod.QuarterDelimIndex = -1;
                pod.QuarterDelimPos = 0f;
                pod.QuarterDelim = default;
            }
            else
            {
                /*
                 * There is a quarter selected, so select any streetpoint from that
                 * quarter. The fragment constraint in that case applies to the quarter, not to the
                 * streetpoint.
                 */
                var quarterDelims = q.GetDelims();
                if (null == quarterDelims || quarterDelims.Count <= 1)
                {
                    return false;
                }

                int nDelims = quarterDelims.Count;
                int idxDelim = (int)(_rnd.GetFloat() * nDelims);
                var delim = quarterDelims[idxDelim];
                sp = delim.StreetPoint;

                pod.QuarterDelimIndex = idxDelim;
                pod.QuarterDelim = delim;
                pod.QuarterDelimPos = 0f;
            }

            if (null == sp) return false;

            pod.StreetPoint = sp;
            pod.StreetPointId = sp.Id;
        }

        /*
         * If we shall reference a streetpoint or a quarter, we need to add its position.
         * But not both, because both are relative to the quarter.
         */
        switch (plad.ReferenceObject)
        {
            case PlacementDescription.Reference.StreetPoint:
                v3ReferenceAccu += sp.Pos3;
                break;
            
            case PlacementDescription.Reference.Quarter:
                v3ReferenceAccu += q.GetCenterPoint3();
                break;
            
            default:
                break;
        }
        
        pod.Position = v3ReferenceAccu;

        return true;
    }
}