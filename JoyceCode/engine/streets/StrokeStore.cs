using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.geom;
using Octree;
using static engine.Logger;

namespace engine.streets;


/**
 * The street graph of one cluster, with its spatial indices.
 *
 * MULTILAYER: strokes and junctions carry a Level. The four neighbourhood queries
 * below all skip entries on a different level, so two streets on different decks can
 * cross without meeting - that crossing is the overpass. Everything stays in ONE pair
 * of octrees and the level is filtered out of the results, rather than keeping an
 * octree per level: for the ground-only case that shipping configurations use, no
 * entry is ever skipped and the cost is exactly what it was. Per-level indices would
 * only pay off once several busy decks exist, and can be added then without touching
 * any caller.
 */
public class StrokeStore
{
    private static readonly engine.Dc _dc = engine.Dc.StreetGen;

    private List<Stroke> _listStrokes = new();
    private List<StreetPoint> _listPoints = new();

    private Octree.PointOctree<StreetPoint> _octreeSP;
    private Octree.BoundsOctree<Stroke> _octreeStrokes;
    private HashSet<long> _setStrokes = new();

    private bool _traceStrokes;

    /*
     * Id sequences for this network.
     *
     * An Id packs the cluster into its high 16 bits and a sequence number into its low
     * 16, so the sequence only has to be unique within one street network - which is
     * exactly the scope of one store. It used to come from a process-global counter,
     * which meant that after 65535 points anywhere in the process the low half wrapped
     * and two points in the SAME network could share an Id, quietly corrupting the
     * adjacency set behind AreConnected and colliding on the LiteDB primary key. The
     * test suite alone gets through that budget some fifty times over.
     *
     * Deliberately per store rather than a static keyed on cluster id: several networks
     * for the same cluster are built concurrently (the test suite does it constantly),
     * and any shared counter that gets reset between them corrupts whichever run is
     * still in flight.
     */
    private int _nextPointLocalId;
    private int _nextStrokeLocalId;

    /**
     * Set while re-adding a cluster that came back from the cache, whose ids are
     * already meaningful and must survive.
     */
    private bool _keepStoredIds;


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

            if (stroke.Level != cand.Level)
            {
                /*
                 * Different decks. They cross on the map and not in the world, which is
                 * exactly what an overpass is. No junction, no split.
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
                if (cand != spNot && cand != sp0 && cand.Level == sp0.Level)
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
        if (_traceStrokes) Trace(_dc, $"Testing point {sp.Pos.ToString()}");
        float closestDist = 100000f; // 100km
        Stroke closestStroke = null;

        foreach (var stroke in strokesToIterate)
        {

            /*
             * Skip stroke's end points.
             */
            if (sp == stroke.A || sp == stroke.B)
            {
                if (_traceStrokes) Trace(_dc, $"Skipping stroke {stroke.ToString()}, because point is part of stroke.");
                continue;
            }

            if (stroke.Level != sp.Level)
            {
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
                Trace(_dc,
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
        if (_traceStrokes) Trace(_dc, $"Testing stroke {stroke.ToString()}");
        float closestDist = 100000f; // 100km
        StreetPoint? closestPoint = null;

        foreach (var sp0 in pointsToSearch)
        {

            /*
             * Skip stroke's end points.
             */
            if (sp0 == stroke.A || sp0 == stroke.B)
            {
                if (_traceStrokes) Trace(_dc, $"Skipping point {sp0.Pos.X}, {sp0.Pos.Y}, because its part of this stroke.");
                continue;
            }

            if (sp0.Level != stroke.Level)
            {
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
                Trace(_dc, $"Stroke in range for {si.Pos.X}, {si.Pos.Y}, length {stroke.Length} distance {closestDist}");
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

        if (_traceStrokes) Trace(_dc, $"Adding point {sp}.");

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
        _assignLocalId(sp);

        _octreeSP.Add(sp, sp.Pos3);

        sp.InStore = true;
        _listPoints.Add(sp);
    }


    /**
     * Re-add a stroke that came back from the cluster cache, keeping the ids it was
     * stored with. Everything else must go through AddStroke, which hands out fresh
     * network-local ids.
     */
    public void AddStoredStroke(in Stroke stroke)
    {
        _keepStoredIds = true;
        try
        {
            AddStroke(stroke);
        }
        finally
        {
            _keepStoredIds = false;
        }
    }


    public void AddStroke(in Stroke stroke)
    {
        if (_traceStrokes) Trace(_dc, $"Adding stroke {stroke}");

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

        _assignLocalSid(stroke);

        stroke.Store = this;
        _listStrokes.Add(stroke);
        _setStrokes.Add((long)stroke.A.Id | ((long)stroke.B.Id << 32));
        _setStrokes.Add((long)stroke.B.Id | ((long)stroke.A.Id << 32));
        _computeStrokeBoundingBox(stroke, out var bb);
        _octreeStrokes.Add(stroke, bb);
    }


    /**
     * Give a point its identity within this network, unless it already has one from
     * storage.
     */
    private void _assignLocalId(in StreetPoint sp)
    {
        if (_keepStoredIds)
        {
            /*
             * Keep the sequence ahead of what came back, so anything added afterwards
             * cannot collide with it.
             */
            int stored = sp.Id & 0xffff;
            if (stored > _nextPointLocalId) _nextPointLocalId = stored;
            return;
        }

        if (_nextPointLocalId >= 0xffff)
        {
            ErrorThrow(
                $"This street network has run out of street point ids: an Id carries only "
                + $"16 bits of sequence, so one cluster cannot hold more than 65535 points.",
                m => new InvalidOperationException(m));
        }

        /*
         * Round-trips through ClusterId's setter, which is what does the packing.
         */
        sp.Id = ++_nextPointLocalId;
        sp.ClusterId = sp.ClusterId;
    }


    private void _assignLocalSid(in Stroke stroke)
    {
        if (_keepStoredIds)
        {
            int stored = stroke.Sid & 0xffff;
            if (stored > _nextStrokeLocalId) _nextStrokeLocalId = stored;
            return;
        }

        if (_nextStrokeLocalId >= 0xffff)
        {
            ErrorThrow(
                $"This street network has run out of stroke ids: an Sid carries only 16 "
                + $"bits of sequence.",
                m => new InvalidOperationException(m));
        }

        stroke.Sid = ++_nextStrokeLocalId;
        stroke.ClusterId = stroke.ClusterId;
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


    /**
     * Ramps whose bounding box comes within maxDistance of the given stroke.
     *
     * Deliberately ignores Level: a ramp is the one thing that occupies two decks at
     * once, so the caller decides which of its ends matter.
     */
    public IEnumerable<Stroke> GetRampsNear(in Stroke stroke, float maxDistance)
    {
        List<Stroke> found = new();

        _computeStrokeBoundingBox(stroke, out var bb);
        bb = new Octree.BoundingBox(bb.Center, bb.Size + 2f * maxDistance * Vector3.One);

        List<Stroke> nearby = new();
        if (!_octreeStrokes.GetCollidingNonAlloc(nearby, bb))
        {
            return found;
        }

        foreach (var cand in nearby)
        {
            if (cand.Kind != StrokeKind.Ramp || cand == stroke)
            {
                continue;
            }

            if (cand.Distance(stroke.A.Pos) <= maxDistance
                || cand.Distance(stroke.B.Pos) <= maxDistance
                || stroke.Distance(cand.A.Pos) <= maxDistance
                || stroke.Distance(cand.B.Pos) <= maxDistance)
            {
                found.Add(cand);
            }
        }

        return found;
    }


    public List<Stroke> GetStrokes()
    {
        return _listStrokes;
    }

    
    public IReadOnlyList<StreetPoint> QueryStreetPoints(
        in AABB aabb
    )
    {
        List<StreetPoint> listStreetPoints = new();
        foreach (var sp in _listPoints)
        {
            if (aabb.Contains(sp.Pos3))
            {
                listStreetPoints.Add(sp);
            }
        }

        return listStreetPoints.AsReadOnly();
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
