using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using engine.joyce;
using engine.joyce.components;
using engine.world;
using static engine.Logger;

namespace engine.streets;


public class StreetPoint
{
    private static readonly engine.Dc _dc = engine.Dc.StreetGen;

    private static object _classLo = new();

    /**
     * Provisional identity for a point that is not in a store yet.
     *
     * Points are compared and keyed on Id well before they join a network - the
     * section maps do it, and so do tests that build a junction by hand - so a fresh
     * point cannot simply have no id. StrokeStore replaces this with a sequence number
     * local to the network when the point is added; see StrokeStore._assignLocalId.
     */
    static private int _nextProvisionalId = 1;



    private object _lo = new();

    
    [LiteDB.BsonId]
    public int Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        set;
    }

    
    private int _clusterId = -1;
    public  int ClusterId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _clusterId;
        set
        {
            _clusterId = value;

            Id = (_clusterId<<16) | (Id & 0xffff);
        }
    }


    /**
     * Hand out the next sequence number within a cluster. Never returns 0, so that a
     * zero low half unambiguously means "no identity yet".
     */
    public Vector2 Pos
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        set;
    }

    
    /**
     * INDEX space: the coordinate the octrees are keyed on, deliberately planar even
     * for a junction on a raised deck. See StreetLevels for why, and use
     * LevelElevation when you want the height.
     */
    [LiteDB.BsonIgnore]
    [JsonIgnore]
    public Vector3 Pos3
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new Vector3(Pos.X, 0f, Pos.Y);
    }
    

    /**
     * Which deck this junction sits on. 0 is the ground, +1 a bridge deck above it,
     * -1 a tunnel below. Additive for persistence: a cluster cached before multilayer
     * existed deserialises with every junction on the ground, which is what it was.
     */
    public sbyte Level { get; set; }


    /**
     * Height of this junction's deck above the ground surface at this spot. Zero on
     * the ground, which is every junction until multilayer rulesets are enabled.
     */
    [LiteDB.BsonIgnore]
    [JsonIgnore]
    public float LevelElevation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => StreetLevels.ElevationOf(Level);
    }


    public string Creator { get; set; }

    /*
     * Setup insync with the StrokeStore._listPoints
     */
    public bool InStore;

    private List<Stroke> _listStartingStrokes;
    private List<Stroke> _listEndingStrokes;

    private List<Stroke> _angleArray;

    /**
     * This one contains an array of the intersection of each of
     * the strokes in the _angleArray with the previous one.
     */
    private List<Vector2> _sectionArray;

    /**
     * While generating quarters, we need to look up the intersection points
     * based on the strokes involved.
     */
    private Dictionary<int, Vector2> _sectionStrokeMap;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _invalidateNoLock()
    {
        _angleArray = null;
        _sectionArray = null;
    }

    public void Invalidate()
    {
        lock (_lo)
        {
            _invalidateNoLock();
        }
    }

    public override string ToString()
    {
        return $"{{ #{Id}: {Pos.ToString()} ({Creator})}}";
    }

    public void SetPos(in Vector2 pos)
    {
        SetPos(pos.X, pos.Y);
    }

    public void SetPos(float x, float y)
    {
        lock (_lo)
        {
#if false
            pos.x = x;
            pos.y = y;
#else
            Pos = new Vector2((int)(x * 10f) / 10f, (int)(y * 10f) / 10f);
#endif
            _invalidateNoLock();
            if (null != _listStartingStrokes)
            {
                foreach (var stroke in _listStartingStrokes)
                {
                    stroke.Invalidate();
                }
            }

            if (null != _listEndingStrokes)
            {
                foreach (var stroke in _listEndingStrokes)
                {
                    stroke.Invalidate();
                }
            }
        }
    }


    private void _computeAngleArrayNoLock()
    {
        _angleArray = new List<Stroke>();
        if (null != _listStartingStrokes)
        {
            foreach (var stroke in _listStartingStrokes)
            {
                if (null == stroke)
                {
                    throw new InvalidOperationException(
                        "StreetPoint:getAngleArray(): Refusing to add null stroke.");
                }

                _angleArray.Add(stroke);
            }
        }

        if (null != _listEndingStrokes)
        {
            foreach (var stroke in _listEndingStrokes)
            {
                if (null == stroke)
                {
                    throw new InvalidOperationException(
                        "StreetPoint:getAngleArray(): Refusing to add null stroke.");
                }

                _angleArray.Add(stroke);
            }
        }

        _angleArray.Sort((a, b) =>
        {
            // If any of the strokes is ending here, we meed to invert the angle.
            var aAngle = geom.Angles.Snorm(a.GetAngleSP(this));
            var bAngle = geom.Angles.Snorm(b.GetAngleSP(this));

            var diff = /* geom.Angles.snorm( */ aAngle - bAngle; // );
            if (diff < 0.0) return -1;
            else if (diff > 0.0) return 1;
            else return 0;
        });

        foreach (var stroke in _angleArray)
        {
            if (null == stroke)
            {
                throw new InvalidOperationException(
                    "StreetPoint.getAngleArray(): After sorting, angle array contains a null.");
            }
        }
    }


    private List<Stroke> _getAngleArrayNoLock()
    {
        if (null != _angleArray)
        {
            return _angleArray;
        }

        _computeAngleArrayNoLock();
        return _angleArray;
    }


    /**
     * Return a sorted array of strokes.
     *
     * @return Array<Stroke>
     */
    public List<Stroke> GetAngleArray()
    {
        lock (_lo)
        {
            return _getAngleArrayNoLock();
        }
    }


    private void _traceAnglesNoLock()
    {
        Trace(_dc, $"angles:");
        foreach (var stroke in _angleArray)
        {
            var angle = geom.Angles.Snorm(stroke.GetAngleSP(this));
            Trace(_dc, $"getAngleArray(): angle={angle} ({angle * 180.0 / Math.PI})");
        }
    }


    /**
     * Given the angle from another incoming stroke, find the next outgoing
     * stroke.
     */
    public Stroke GetNextAngle(in Stroke strokeCurrent, float angle, bool clockwise)
    {
        lock (_lo)
        {
            float minAngle = (float)Math.PI * 2.0f;
            Stroke minStroke = null;
            Stroke nullStroke = null;
            if (null == strokeCurrent)
            {
                throw new InvalidOperationException("StreetPoint.getNextAngle(): Called without current stroke.");
            }

            var debugPoint = false;

            /*
             * Also, per API, we take the angle of an incoming stroke.
             * However, we want to find the next outgoing stroke, so we need
             * to inverse the angle, as we will compute outgoing angles
             * in this function.
             */
            var myAngle = geom.Angles.Snorm(angle + (float)Math.PI);

            if (!clockwise)
            {
                throw new InvalidOperationException(
                    "'StreetPoint:getNextAngle(): Anti-Clockwise not implemented yet.");
            }

            /*
             * Start with the outgoing strokes.
             */
            if (null != _listStartingStrokes)
            {
                foreach (var stroke in _listStartingStrokes)
                {
                    var currAngle = geom.Angles.Snorm(stroke.Angle);
                    /*
                     * Note, that we need to use the unsigned angle.
                     */
                    var diffAngle = geom.Angles.Unorm(currAngle - myAngle);

                    bool isStart = (strokeCurrent != null) && (strokeCurrent == stroke);

                    if (debugPoint)
                    {
                        string strStart;
                        if (isStart)
                        {
                            strStart = "START";
                        }
                        else
                        {
                            strStart = "";
                        }

                        Trace(_dc,
                            $"getNextAngle({Pos}, {myAngle}, {stroke.B.Pos.X}): OUT {strStart} {currAngle} diffAngle {diffAngle}");
                    }

                    if (isStart)
                    {
                        /*
                         * This must be the same storke.
                         */
                        nullStroke = stroke;
                    }
                    else if (minAngle > diffAngle)
                    {
                        /*
                         * A new smaller one.
                         */
                        minStroke = stroke;
                        minAngle = diffAngle;
                    }

                }
            }


            /*
             * Now the incoming strokes. Their angles need to be inversed.
             */
            if (null != _listEndingStrokes)
            {
                foreach (var stroke in _listEndingStrokes)
                {
                    /*
                     * Note the offset.
                     */
                    float currAngle = geom.Angles.Snorm(stroke.Angle + (float)Math.PI);
                    /*
                     * Note, that we need to use the unsigned angle for minimizing
                     * angle.
                     */
                    float diffAngle = geom.Angles.Unorm(currAngle - myAngle);

                    bool isStart = (strokeCurrent != null) && (strokeCurrent == stroke);

                    if (debugPoint)
                    {
                        string strStart;
                        if (isStart)
                        {
                            strStart = "START";
                        }
                        else
                        {
                            strStart = "";
                        }

                        Trace(_dc,
                            $"getNextAngle({Pos}, {myAngle}, {stroke.A.Pos.X}): IN {strStart} {currAngle} diffAngle {diffAngle}");
                    }

                    if (isStart)
                    {
                        /*
                         * This must be the same storke.
                         */
                        nullStroke = stroke;
                    }
                    else if (minAngle > diffAngle)
                    {
                        /*
                         * A new smaller one.
                         */
                        minStroke = stroke;
                        minAngle = diffAngle;
                    }

                }
            }

            // Return null or myself.
            return minStroke;
        }
    }

    public void RemoveStartingStroke(in Stroke s)
    {
        lock (_lo)
        {
            if (this != s.A)
            {
                throw new InvalidOperationException("StreetPoint.RemoveStartingStroke(): Stroke start is not me.");
            }

            if (null == s.B)
            {
                throw new InvalidOperationException(
                    "'StreetPoint.removeStartingStroke(): Stroke has no end point.");
            }

            if (null == _listStartingStrokes)
            {
                throw new InvalidOperationException("StreetPoint: No Starting list yet.");
            }

            _invalidateNoLock();
            _listStartingStrokes.Remove(s);
        }
    }


    public void AddStartingStroke(Stroke s)
    {
        lock (_lo)
        {
            if (null == s.B)
            {
                throw new InvalidOperationException("StreetPoint.addStartingStroke(): Stroke had no end point.");
            }

            if (this != s.A)
            {
                throw new InvalidOperationException("StreetPoint.addStartingStroke(): Stroke start is not me.");
            }

            if (null == _listStartingStrokes)
            {
                _listStartingStrokes = new List<Stroke>();
            }

            _invalidateNoLock();
            if (0 != _listStartingStrokes.FindAll(a => a == s).Count)
            {
                throw new InvalidOperationException(
                    $"StreetPoint.addStartingStroke(): Stroke {s.ToString()} already attached.");
            }

            _listStartingStrokes.Add(s);
        }
    }


    public void RemoveEndingStroke(in Stroke s)
    {
        lock (_lo)
        {
            if (this != s.B)
            {
                throw new InvalidOperationException($"StreetPoint.removeEndingStroke(): Stroke end is not me.");
            }

            if (null == s.A)
            {
                throw new InvalidOperationException(
                    $"StreetPoint.removeEndingStroke(): Stroke has no start point.");
            }

            if (null == _listEndingStrokes)
            {
                throw new InvalidOperationException($"StreetPoint: No Ending list yet.");
            }

            _invalidateNoLock();
            _listEndingStrokes.Remove(s);
        }
    }


    public void AddEndingStroke(Stroke s)
    {
        lock (_lo)
        {
            if (null == s.A)
            {
                throw new InvalidOperationException($"StreetPoint.addEndingStroke(): Stroke had no start point.");
            }

            if (this != s.B)
            {
                throw new InvalidOperationException($"StreetPoint.addEndingStroke(): Stroke end is not me.");
            }

            if (null == _listEndingStrokes)
            {
                _listEndingStrokes = new List<Stroke>();
            }

            _invalidateNoLock();
            if (0 != _listEndingStrokes.FindAll(a => a == s).Count)
            {
                throw new InvalidOperationException(
                    $"StreetPoint.addEndingStroke(): Stroke {s.ToString()} already attached.");
            }

            _listEndingStrokes.Add(s);
        }
    }


    public bool HasStrokes()
    {
        lock (_lo)
        {
            if (
                (
                    null == _listStartingStrokes || 0 == _listStartingStrokes.Count
                ) && (
                    null == _listEndingStrokes || 0 == _listEndingStrokes.Count
                )
            )
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }


    private bool _isDebugPoint()
    {
        Vector2[] arrPoints = { new( 93.1f - (-5.7f), -136.8f - 10f ) };
        foreach (var v2Ref in arrPoints)
        {
            if ((v2Ref - this.Pos).LengthSquared() < 100)
            {
                return true;
            }
        }

        return false;
    }

    private void _computeSectionArrayNoLock()
    {
        var myVerbose = false;
        //var isMyPoint = _isDebugPoint();

        _sectionArray = new List<Vector2>();
        _sectionStrokeMap = new Dictionary<int, Vector2>();

        /*
         * Make sure we have the array of sorted strokes.
         */
        _getAngleArrayNoLock();

        /*
         * A street point with a single street does not have any section array.
         */
        if (_angleArray.Count < 2)
        {
            return;
        }

        /*
         * A street point with two strokes does use the generic intersection, unless
         * they are perfectly collinear. They do not, however make up a polygon later
         * on but come next to each others.
         */

        if (myVerbose)
        {
            _traceAnglesNoLock();
        }

        int idx = _angleArray.Count - 1;
        /*
         * Iterate through our point array, intersecting the adjacent sides of the
         * previous and the current stroke.
         *
         * Note: As we use the infinite lines, the in/out orientation does not matter.
         * The angle array sorts the strokes counterclockwise in R2.
         */
        foreach (var curr in _angleArray)
        {
            // Trace( 'getSectionArray(): curr.angle is ${curr.angle}, ${curr.angle+Math.PI}.' );
            var prev = _angleArray[idx % _angleArray.Count];
            if (curr != _angleArray[(idx + 1) % _angleArray.Count])
            {
                throw new InvalidOperationException($"StreetPoint.getSectionArray(): Mismatch of angle array.");
            }

            /*
             * The two arms' unit directions, both pointing OUT of this junction. Taken from
             * the strokes' own cached unit vectors rather than renormalised here, so that
             * the section point lands on exactly the line every consumer measures against
             * (Stroke.Normal is the same unit vector rotated).
             */
            Vector2 dp = prev.A == this ? prev.Unit : -prev.Unit;
            Vector2 dc = curr.A == this ? curr.Unit : -curr.Unit;

            /*
             * Copy paste from generate street operator.
             */
            float prevHalfStreetWidth = prev.StreetWidth() / 2.0f;
            float currHalfStreetWidth = curr.StreetWidth() / 2.0f;

            /*
             * The mitre of the two carriageway edges, bounded relative to the street width.
             *
             * This used to intersect the two offset lines through geom.Line.IntersectInfinite
             * and substitute an averaged offset whenever the answer came back further than
             * 63.2 m out. Both halves were wrong: the intersection is computed in absolute
             * world coordinates and loses every significant digit when the two arms are
             * nearly collinear - which is when this fires - and testing only the DISTANCE of
             * the answer cannot notice a cancelled intersection that happens to land nearby.
             * See engine.streets.generation.SectionMitre and §7q.
             */
            Vector2 newI = Pos + generation.SectionMitre.OffsetOf(
                dp, dc, prevHalfStreetWidth, currHalfStreetWidth,
                generation.SectionMitre.MitreLimit, out bool isClamped);

            if (isClamped)
            {
                Trace(_dc, $"StreetPoint {Id}: the mitre between strokes {prev.Sid} and "
                           + $"{curr.Sid} was cut back to the mitre limit.");
            }

            if (myVerbose)
            {
                Trace(_dc, $"Adding point $newI");
            }

            _sectionArray.Add(newI);
            /*
             * Now also add this point to the stroke lookup. Obviously, it involves
             * two strokes, curr and prev, so add both associations.
             */
            int ids = (curr.Sid % 10000) + 10000 * (prev.Sid % 10000);
            _sectionStrokeMap.Add(ids, newI);
            // Trace('StreetPoint.getSectionArray(): sp $id Storing stroke $ids ${curr.sid} and ${prev.sid}');
            ++idx;
        }
    }


    private List<Vector2> _getSectionArrayNoLock()
    {
        if (_sectionArray != null)
        {
            return _sectionArray;
        }

        _computeSectionArrayNoLock();
        return _sectionArray;
    }


    /**
     * Return an array of of points describing the intersection of each stroke with
     * the previous one.
     *
     * @return Array<geom.Point>
     */
    public List<Vector2> GetSectionArray()
    {
        lock (_lo)
        {
            return _getSectionArrayNoLock();
        }
    }


    /**
     * Return the intersection point involving the given stroke.
     *
     * @param stroke
     * @return geom.Point
     */
    public Nullable<Vector2> GetSectionPointByStroke(in Stroke curr, in Stroke prev)
    {
        lock (_lo)
        {
            if (null == _sectionArray)
            {
                _getSectionArrayNoLock();
            }

            int ids = (curr.Sid % 10000) + 10000 * (prev.Sid % 10000);
            // Trace('StreetPoint.getSectionPointByStroke(): sp $id Obtaining stroke $ids ${curr.sid} and ${prev.sid}');
            if (!_sectionStrokeMap.ContainsKey(ids))
            {
                // Trace('StreetPoint.getSectionPointByStroke(): Not found.');
                return null;
            }

            // Trace('StreetPoint.getSectionPointByStroke(): Returning point.');
            return _sectionStrokeMap[ids];
        }
    }


    public void PushCreator(in string s)
    {
        Creator = Creator + ":" + s;
    }

    public StreetPoint()
    {
        lock (_classLo)
        {
            Id = _nextProvisionalId++;
        }

        Pos = new Vector2(0f, 0f);
        InStore = false;
        _listStartingStrokes = null;
        _listEndingStrokes = null;
        _angleArray = null;
        _sectionArray = null;
        _sectionStrokeMap = null;
        Creator = "";
    }
}