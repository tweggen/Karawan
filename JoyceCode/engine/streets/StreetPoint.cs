using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using static engine.Logger;

namespace engine.streets;

public class StreetPoint
{
    private static object _classLo = new();
    static private int _nextId = 1;

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


    public Vector2 Pos
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        private set;
    }

    
    [LiteDB.BsonIgnore]
    public Vector3 Pos3
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new Vector3(Pos.X, 0f, Pos.Y);
    }
    

    public string Creator { get; private set; }

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
        Trace("angles:");
        foreach (var stroke in _angleArray)
        {
            var angle = geom.Angles.Snorm(stroke.GetAngleSP(this));
            Trace($"getAngleArray(): angle={angle} ({angle * 180.0 / Math.PI})");
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

                        Trace(
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

                        Trace(
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

            geom.Line sp = null;
            if (prev.A == this)
            {
                sp = new geom.Line(prev.A.Pos, prev.B.Pos);
            }
            else
            {
                sp = new geom.Line(prev.B.Pos, prev.A.Pos);
            }

            geom.Line sc = null;
            if (curr.A == this)
            {
                sc = new geom.Line(curr.A.Pos, curr.B.Pos);
            }
            else
            {
                sc = new geom.Line(curr.B.Pos, curr.A.Pos);
            }

            /*
             * Normals. 
             * If I move from outside to this street point, the normal shall point
             * to the left side. That means, the normal points from the current to
             * the previous stroke.
             */
            Vector2 np = sp.Normal();
            // if( prev.b == this ) { np.x = -np.x; np.y = -np.y; }
            Vector2 nc = sc.Normal();
            // if( curr.b == this ) { nc.x = -nc.x; nc.y = -nc.y; }

            /*
             * Copy paste from generate street operator.
             */
            float prevHalfStreetWidth = prev.StreetWidth() / 2.0f;
            float currHalfStreetWidth = curr.StreetWidth() / 2.0f;

            /*
             * Scale each of the normals to properly move the line.
             * Compute the offets.
             */
            float opx = np.X * (-prevHalfStreetWidth);
            float opy = np.Y * (-prevHalfStreetWidth);

            float ocx = nc.X * (currHalfStreetWidth);
            float ocy = nc.Y * (currHalfStreetWidth);

            sp.Move(opx, opy);
            sc.Move(ocx, ocy);

            Nullable<Vector2> i0 = sp.IntersectInfinite(sc);
            Vector2 i;
            Vector2 newI;

            bool doUseSide = false;
            if (null == i0)
            {
                if (myVerbose) Trace("no intersect");
                doUseSide = true;
                // Please the compiler and assign newI a value that later is overridden.
                newI.X = newI.Y = 0;
            }
            else
            {
                i = i0.Value;

                /*
                 * If the intersection is too far away from the streetpoint, these are pretty in-line
                 * streets, so take their common border.
                 */
                float dx = i.X - Pos.X;
                float dy = i.Y - Pos.Y;
                float dist2 = dx * dx + dy * dy;
                if (dist2 > 4000f)
                {
                    if (myVerbose) Trace("farout intersect");
                    /*
                     * If this intersection is too far away, we use the point offset by the 
                     * average of both normals.
                     */
                    var n = new Vector2(nc.X - np.X, nc.Y - np.Y);
                    n = n / n.Length();
                    var averW = (prevHalfStreetWidth + currHalfStreetWidth) / 2f;
                    newI = new Vector2(Pos.X + n.X * averW, Pos.Y + n.Y * averW);
                }
                else
                {
                    if (myVerbose) Trace("close intersect");
                    newI = i;
                }
            }

            if (doUseSide)
            {
                // Trace( 'getSectionArray(): no intersection.' );
                /*
                 * If the streets are parallel and the sides in line, use the offset
                 * street point itself as an intersection, using the average street width.
                 */
                float averHalfStreetWidth = (prevHalfStreetWidth + currHalfStreetWidth) / 2f;
                var osx = nc.X * averHalfStreetWidth;
                var osy = nc.Y * averHalfStreetWidth;
                newI = new Vector2(Pos.X + osx, Pos.Y + osy);
            }

            if (myVerbose)
            {
                Trace($"Adding point $newI");
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
            Id = _nextId++;
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