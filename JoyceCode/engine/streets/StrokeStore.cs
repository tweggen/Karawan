﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Octree;
using static engine.Logger;

namespace engine.streets;


public class StrokeStore
{
    private List<Stroke> _listStrokes = new();
    private List<StreetPoint> _listPoints = new();

    private Octree.PointOctree<StreetPoint> _octreeSP;
    private Octree.BoundsOctree<Stroke> _octreeStrokes;
    private HashSet<long> _setStrokes = new();

    private bool _traceStrokes;


    static private void _computeStrokeBoundingBox(in Stroke stroke, out Octree.BoundingBox bb)
    {
        /*
         * Now the API demands this to be the all coordinates maximum vector.
         */
        Vector3 vSize = stroke.B.Pos3 - stroke.A.Pos3;
        vSize = new Vector3(Single.Abs(vSize.X), Single.Abs(vSize.Y), Single.Abs(vSize.Z));
        bb = new Octree.BoundingBox((stroke.B.Pos3 + stroke.A.Pos3) / 2f, vSize);
    }

    /**
     * Look for a stroke that intersects with the given stroke.
     * @param cand
     *   The stroke we shall look to be intersected with
     * @param refSP
     *   This is the reference point when describing the intersection.
     */
    public StrokeIntersection IntersectsMayTouchClosest(in Stroke cand, in StreetPoint refSP)
    {
        // TXWTODO: This gives the same result.
        _computeStrokeBoundingBox(cand, out var bb);
        if (!_octreeStrokes.GetCollidingNonAlloc(_tmpStrokeList, bb))
        {
            return null;
        }

        List<Stroke> strokesToCheck = _tmpStrokeList;
        _tmpStrokeList = new();
        StrokeIntersection closestIntersection = null;
        float closestDist2 = 100000000.0f;

        foreach (var stroke in strokesToCheck)
        {
            if (cand == stroke)
            {
                /*
                 * We do not want to intersect with ourselves.
                 */
                continue;
            }

            var si = stroke.Intersects(cand);
            if (null == si)
            {
                /*
                 * No collision record? Then this is not a collision.
                 */
                continue;
            }

            /*
             * We have a collision. But is this probably just the end of the
             * stroke ending at this point.
             */
            // TXWTODO: We are just checking the candidate's endpoint, not the ones from the store. Shouldn't we also do them?
            if (Vector2.DistanceSquared(si.Pos, cand.A.Pos) < 0.005f
                || Vector2.DistanceSquared(si.Pos, cand.B.Pos) < 0.005f
                || Vector2.DistanceSquared(si.Pos, stroke.A.Pos) < 0.005f
                || Vector2.DistanceSquared(si.Pos, stroke.B.Pos) < 0.005f
               )
            {
                continue;
            }

            {
                /*
                 * This is an intersection with this stroke.
                 */
                float dist2 = Vector2.DistanceSquared(si.Pos, refSP.Pos);
                if (dist2 < closestDist2)
                {
                    closestDist2 = dist2;
                    closestIntersection = si;
                }

            }
        }

        return closestIntersection;
    }


    public StreetPoint? FindClosestBelowButNot(
        StreetPoint sp0,
        float minDist,
        in StreetPoint spNot
    )
    {
        return _findClosestToCoordBelowButNot(
            sp0.Pos.X, sp0.Pos.Y, sp0, minDist, spNot
        );
    }


    private StreetPoint? _findClosestToCoordBelowButNot(
        float x, float y,
        in StreetPoint sp0,
        float minDist,
        in StreetPoint spNot)
    {
        // This does not modify the result.
        if (_octreeSP.GetNearbyNonAlloc(sp0.Pos3, minDist, _tmpListNearby))
        {
            bool haveSome = false;
            int l = _tmpListNearby.Count;
            float closestDist2 = minDist * minDist * 10f;
            StreetPoint? closestSP = null;
            for (int i = 0; i < l; ++i)
            {
                StreetPoint cand = _tmpListNearby[i];
                if (cand != spNot && cand != sp0)
                {
                    if (null == closestSP)
                    {
                        closestSP = cand;
                        closestDist2 = (cand.Pos - sp0.Pos).LengthSquared();
                    }
                    else
                    {
                        float myDist2 = (cand.Pos - sp0.Pos).LengthSquared();
                        if (myDist2 < closestDist2)
                        {
                            closestSP = cand;
                            closestDist2 = myDist2;
                        }
                    }
                }
            }

            _tmpListNearby.Clear();

            return closestSP;
        }
        else
        {
            return null;
        }
    }


    public StreetPoint GetStreetPoint(int id)
    {
        // TXWTODO: Inefficient!!!
        return _listPoints.FirstOrDefault(sp => sp.Id == id);
    }


    public Stroke GetStroke(int sid)
    {
        // TXWTODO: Inefficient
        return _listStrokes.FirstOrDefault(stroke => stroke.Sid == sid);
    }


    private List<Stroke> _tmpStrokeList = new();

    
    /**
     * Return the closest stroke to the given street point,
     * which is closer than maxDistance.
     */
    public StrokeIntersection? GetClosestStroke(
        in StreetPoint sp, float maxDistance)
    {
        // TXWTODO: This gives the same result.
        /*
         * Optimized: iterate only through strokes within a reasonable neighbourhood.
         *
         * This means we look for bounding boxes intersecting streetpoint plus distance.
         */
        if (!_octreeStrokes.GetCollidingNonAlloc(_tmpStrokeList,
                new BoundingBox(sp.Pos3, 2f * maxDistance * Vector3.One)))
        {
            /*
             * Nothing found? Short circuit.
             */
            return null;
        }

        List<Stroke> strokesToIterate = _tmpStrokeList;
        _tmpStrokeList = new();
        if (_traceStrokes) Trace($"Testing point {sp.Pos.ToString()}");
        float closestDist = 100000f; // 100km
        Stroke closestStroke = null;

        foreach (var stroke in strokesToIterate)
        {

            /*
             * Skip stroke's end points.
             */
            if (sp == stroke.A || sp == stroke.B)
            {
                if (_traceStrokes) Trace($"Skipping stroke {stroke.ToString()}, because point is part of stroke.");
                continue;
            }

            var dist = stroke.Distance(sp.Pos);

            if (dist < closestDist)
            {
                closestDist = dist;
                closestStroke = stroke;
            }
        }

        if (null != closestStroke)
        {
            var si = new StrokeIntersection(
                pos: sp.Pos,
                streetPoint: sp,
                strokeExists: closestStroke,
                scaleExists: closestDist
            );
            if (_traceStrokes)
                Trace(
                    $"Stroke in range for {si.Pos.X}, {si.Pos.Y}, length {closestStroke.Length} distance {closestDist}");
            return si;
        }
        else
        {
            return null;
        }
    }


    /**
     * Return the point that is closest to the given stroke.
     */
    public StrokeIntersection GetClosestPoint(in Stroke stroke, float maxDistance)
    {
        // This does not modify the result.
        /*
         * To opimize, we raycast into the octree for points.
         * Due to the nature of
         */
        if (!_octreeSP.GetNearbyNonAlloc(new Octree.Ray(stroke.A.Pos3, stroke.B.Pos3 - stroke.A.Pos3), maxDistance,
                _tmpListNearby))
        {
            return null;
        }

        List<StreetPoint> pointsToSearch = _tmpListNearby;
        _tmpListNearby = new();
        if (_traceStrokes) Trace($"Testing stroke {stroke.ToString()}");
        float closestDist = 100000f; // 100km
        StreetPoint? closestPoint = null;

        foreach (var sp0 in pointsToSearch)
        {

            /*
             * Skip stroke's end points.
             */
            if (sp0 == stroke.A || sp0 == stroke.B)
            {
                if (_traceStrokes) Trace($"Skipping point {sp0.Pos.X}, {sp0.Pos.Y}, because its part of this stroke.");
                continue;
            }

            float dist = stroke.Distance(sp0.Pos);

            if (dist < closestDist)
            {
                closestDist = dist;
                closestPoint = sp0;
            }
        }

        if (null != closestPoint)
        {
            var si = new StrokeIntersection(
                pos: closestPoint.Pos,
                streetPoint: closestPoint,
                strokeExists: stroke,
                scaleExists: closestDist
            );
            if (_traceStrokes)
                Trace($"Stroke in range for {si.Pos.X}, ${si.Pos.Y}, length {stroke.Length} distance $closestDist");
            return si;
        }
        else
        {
            return null;
        }
    }


    /**
     * Remove the given stroke
     */
    public void Remove(in Stroke stroke)
    {
        if (null == stroke.Store)
        {
            ErrorThrow("StrokeStore: Stroke not in any store.", m => new InvalidOperationException(m));
        }

        if (this != stroke.Store)
        {
            ErrorThrow("StrokeStore: Stroke not in this store.", m => new InvalidOperationException(m));
        }

        _listStrokes.Remove(stroke);
        _octreeStrokes.Remove(stroke);
        _setStrokes.Remove((long)stroke.A.Id | ((long)stroke.B.Id << 32));
        _setStrokes.Remove((long)stroke.B.Id | ((long)stroke.A.Id << 32));

        stroke.Store = null;
        stroke.A.RemoveStartingStroke(stroke);
        stroke.B.RemoveEndingStroke(stroke);
    }


    private List<StreetPoint> _tmpListNearby = new();

    
    private void AddPoint(in StreetPoint sp)
    {
        if (sp.InStore)
        {
            ErrorThrow($"Unable to add point {sp.ToString()}: Already in store.",
                m => new InvalidOperationException(m));
        }

        if (_traceStrokes) Trace($"Adding point {sp}.");

#if DEBUG
        /*
         * For debugging purposes, find a considerably close point.
         * Which obviously must not be the point itself.
         */
        if (_octreeSP.GetNearbyNonAlloc(sp.Pos3, 0.00000001f, _tmpListNearby))
        {
            int contains = _tmpListNearby.Contains(sp) ? 1 : 0;
            if (_tmpListNearby.Count - contains > 0)
            {
                StreetPoint spFirst = _tmpListNearby[0];
                _tmpListNearby.Clear();
                ErrorThrow($"Refusing to add point {sp.ToString()}, found considerably close points {spFirst}.",
                    m => new InvalidOperationException(m));
            }
        }
#endif
        _octreeSP.Add(sp, sp.Pos3);

        sp.InStore = true;
        _listPoints.Add(sp);
    }


    public void AddStroke(in Stroke stroke)
    {
        if (_traceStrokes) Trace($"Adding stroke {stroke}");

        if (stroke.Store != null)
        {
            if (stroke.Store == this)
            {
                ErrorThrow($"Stroke already in this store.", m => new InvalidOperationException(m));
            }
            else
            {
                ErrorThrow($"Stroke already in other store.", m => new InvalidOperationException(m));
            }
        }

        if (!stroke.A.InStore)
        {
            AddPoint(stroke.A);
        }

        stroke.A.AddStartingStroke(stroke);

        if (!stroke.B.InStore)
        {
            AddPoint(stroke.B);
        }

        stroke.B.AddEndingStroke(stroke);

        stroke.Store = this;
        _listStrokes.Add(stroke);
        _setStrokes.Add((long)stroke.A.Id | ((long)stroke.B.Id << 32));
        _setStrokes.Add((long)stroke.B.Id | ((long)stroke.A.Id << 32));
        _computeStrokeBoundingBox(stroke, out var bb);
        _octreeStrokes.Add(stroke, bb);
    }


    public bool AreConnected(in StreetPoint sp0, in StreetPoint sp1)
    {
#if true
        return
            _setStrokes.Contains((long)sp0.Id | ((long)sp1.Id << 32))
            || _setStrokes.Contains((long)sp1.Id | ((long)sp0.Id << 32));

#else
            foreach(var stroke in _listStrokes)
            {
                if (stroke.A == sp0 && stroke.B == sp1
                    || stroke.B == sp0 && stroke.A == sp1)
                {
                    if (_traceStrokes) Trace( $"Already connected in {stroke.ToString()}.");
                    return true;
                }
            }
            return false;
#endif
    }


    public List<Stroke> GetStrokes()
    {
        return _listStrokes;
    }


    public List<StreetPoint> GetStreetPoints()
    {
        return _listPoints;
    }


    public void ClearTraversed()
    {
        foreach (var stroke in _listStrokes)
        {
            stroke.TraversedAB = false;
            stroke.TraversedBA = false;
        }
    }


    /**
     * Validate the set of street points if all street points meet the required conditions.
     * Required conditions are
     * - street point has connected strokes.
     */
    public void PolishStreetPoints()
    {
        List<int> deadPoints = new();
        int l = _listPoints.Count;
        
        /*
         * Note that we are adding the streetpoints from the last to the first.
         */
        for (int i = l-1; i >= 0; --i)
        {
            var sp = _listPoints[i];
            if (false
                || !sp.HasStrokes())
            {
                deadPoints.Add(i);
            }
        }

        foreach (var idx in deadPoints)
        {
            Trace($"Removing point @{idx} in cluster.");
            _listPoints.RemoveAt(idx);
        }
    }
    
    
    // should be : Trace: Cluster Yelukhdidru has 480 street points, 818 street segments.
    public StrokeStore(float clusterSize)
    {
        _octreeSP = new(clusterSize, Vector3.Zero, 2);
        _octreeStrokes = new(clusterSize, Vector3.Zero, 5f, 1f);
    }
}
