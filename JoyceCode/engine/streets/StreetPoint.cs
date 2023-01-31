using Android.Text.Style;
using Android.Views.Inspectors;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.streets
{
    public class StreetPoint
    {
        static private void trace( in string message )
        {
            Console.WriteLine( message );
        }
        static private int _nextId;
        public int Id;

        public Vector2 Pos { get; private set; }
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
        public void Invalidate()
        {
            _angleArray = null;
            _sectionArray = null;
        }

        public string ToString()
        {
            return $"{{ #{Id}: {Pos.ToString()} ({Creator})}}";
        }

        public void SetPos(float x, float y)
        {
#if false
            pos.x = x;
            pos.y = y;
#else
            Pos.x = (int)(x * 10f) / 10f;
            Pos.y = (int)(y * 10f) / 10f;
#endif
            Invalidate();
            if (null != _listStartingStrokes)
            {
                foreach(var stroke in _listStartingStrokes )
                {
                    stroke.invalidate();
                }
            }
            if (null != _listEndingStrokes)
            {
                foreach(var stroke in _listEndingStrokes )
                {
                    stroke.invalidate();
                }
            }
        }


        /**
         * Return a sorted array of strokes.
         * 
         * @return Array<Stroke>
         */
        public List<Stroke> GetAngleArray()
        {
            if(null != _angleArray) {
                return _angleArray;
            }
            _angleArray = new List<Stroke>();
            if (null != _listStartingStrokes)
            {
                foreach(var stroke in _listStartingStrokes)
                {
                    if (null == stroke)
                    {
                        throw new InvalidOperationException( "StreetPoint:getAngleArray(): Refusing to add null stroke." );
                    }
                    _angleArray.Add(stroke);
                }
            }
            if (null != _listEndingStrokes)
            {
                foreach(var stroke in _listEndingStrokes)
                {
                    if (null == stroke)
                    {
                        throw new InvalidOperationException( "StreetPoint:getAngleArray(): Refusing to add null stroke." );
                    }
                    _angleArray.Add(stroke);
                }
            }
            _angleArray.Sort( (a, b) => {
                // If any of the strokes is ending here, we meed to invert the angle.
                var aAngle = geom.Angles.snorm(a.GetAngleSP(this));
                var bAngle = geom.Angles.snorm(b.GetAngleSP(this));

                var diff = /* geom.Angles.snorm( */ aAngle - bAngle; // );
                if (diff < 0.0) return -1;
                else if (diff > 0.0) return 1;
                else return 0;
            });

            foreach(var stroke in _angleArray)
            {
                if (null == stroke)
                {
                    throw new InvalidOperationException( "StreetPoint.getAngleArray(): After sorting, angle array contains a null." );
                }
            }

            return _angleArray;
        }


        private void traceAngles()
        {
            trace( "angles:" );
            foreach( var stroke in _angleArray )
            {
                var angle = geom.Angles.snorm(stroke.GetAngleSP(this));
                trace( $"getAngleArray(): angle={angle} ({angle * 180.0 / Math.PI})");
            }
        }


        /**
         * Given the angle from another incoming stroke, find the next outgoing
         * stroke.
         */
        public Stroke GetNextAngle(in Stroke strokeCurrent, float angle, bool clockwise)
        {
            float minAngle = (float) Math.PI * 2.0f;
            Stroke minStroke = null;
            Stroke nullStroke = null;
            if (null == strokeCurrent)
            {
                throw new InvalidOperationException( "StreetPoint.getNextAngle(): Called without current stroke." );
            }

            var debugPoint = false;

            /*
             * Also, per API, we take the angle of an incoming stroke.
             * However, we want to find the next outgoing stroke, so we need 
             * to inverse the angle, as we will compute outgoing angles
             * in this function.
             */
            var myAngle = geom.Angles.snorm(angle + Math.PI);

            if (!clockwise)
            {
                throw new InvalidOperationException( "'StreetPoint:getNextAngle(): Anti-Clockwise not implemented yet." );
            }

            /*
             * Start with the outgoing strokes.
             */
            if (null != _listStartingStrokes)
            {
                foreach(var stroke in _listStartingStrokes )
                {
                    var currAngle = geom.Angles.snorm(stroke.Angle);
                    /*
                     * Note, that we need to use the unsigned angle.
                     */
                    var diffAngle = geom.Angles.unorm(currAngle - myAngle);

                    bool isStart = (strokeCurrent != null) && (strokeCurrent == stroke);

                    if (debugPoint)
                    {
                        string strStart;
                        if( isStart )
                        {
                            strStart = "START";
                        } else
                        {
                            strStart = "";
                        }
                        trace($"getNextAngle({pos}, {myAngle}, {stroke.B.Pos.X}): OUT {strStart} {currAngle} diffAngle {diffAngle}");
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
                foreach(var stroke in _listEndingStrokes )
                {
                    /*
                     * Note the offset.
                     */
                    float currAngle = geom.Angles.snorm(stroke.angle + (float)Math.PI);
                    /*
                     * Note, that we need to use the unsigned angle for minimizing
                     * angle.
                     */
                    float diffAngle = geom.Angles.unorm(currAngle - myAngle);

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
                        trace($"getNextAngle({Pos}, {myAngle}, {stroke.A.Pos.X}): IN {strStart} {currAngle} diffAngle {diffAngle}");
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

        public void RemoveStartingStroke(in Stroke s)
        {
            if (this != s.A)
            {
                throw new InvalidOperationException( "StreetPoint.RemoveStartingStroke(): Stroke start is not me." );
            }
            if (null == s.B)
            {
                throw new InvalidOperationException( "'StreetPoint.removeStartingStroke(): Stroke has no end point." );
            }
            if (null == _listStartingStrokes)
            {
                throw new InvalidOperationException( "StreetPoint: No Starting list yet." );
            }
            Invalidate();
            _listStartingStrokes.Remove(s);
        }


        public void AddStartingStroke(Stroke s)
        {
            if (null == s.B)
            {
                throw new InvalidOperationException( "StreetPoint.addStartingStroke(): Stroke had no end point." );
            }
            if (this != s.A)
            {
                throw new InvalidOperationException( "StreetPoint.addStartingStroke(): Stroke start is not me." );
            }
            if (null == _listStartingStrokes)
            {
                _listStartingStrokes = new List<Stroke>();
            }
            Invalidate();
            if (0 != _listStartingStrokes.FindAll( a => a == s ).Count ) {
                throw new InvalidOperationException( $"StreetPoint.addStartingStroke(): Stroke {s.ToString()} already attached." );
            }
            _listStartingStrokes.Add(s);
        }

#if false


public function removeEndingStroke(s: Stroke) {
    if (this != s.b)
    {
        throw 'StreetPoint.removeEndingStroke(): Stroke end is not me.';
    }
    if (null == s.a)
    {
        throw 'StreetPoint.removeEndingStroke(): Stroke has no start point.';
    }
    if (null == _listEndingStrokes)
    {
        throw 'StreetPoint: No Ending list yet.';
    }
    invalidate();
    _listEndingStrokes.remove(s);
}


public function addEndingStroke(s: Stroke) {
    if (null == s.a)
    {
        throw 'StreetPoint.addEndingStroke(): Stroke had no start point.';
    }
    if (this != s.b)
    {
        throw 'StreetPoint.addEndingStroke(): Stroke end is not me.';
    }
    if (null == _listEndingStrokes)
    {
        _listEndingStrokes = new List<Stroke>();
    }
    invalidate();
    if (!_listEndingStrokes.filter(function(a) { return a == s; }).isEmpty() ) {
        throw 'StreetPoint.addEndingStroke(): Stroke ${s.toString()} already attached.';
    }
    _listEndingStrokes.add(s);
}


public function hasStrokes(): Bool
{
    if (
        (
            null == _listStartingStrokes || _listStartingStrokes.isEmpty()
        ) && (
            null == _listEndingStrokes || _listEndingStrokes.isEmpty()
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


/**
 * Return an array of of points describing the intersection of each stroke with 
 * the previous one.
 * 
 * @return Array<geom.Point>
 */
public function getSectionArray(): Array<geom.Point> {
    if (_sectionArray != null)
    {
        return _sectionArray;
    }

    var myVerbose = false;
    if (false && 115 == id)
    {
        myVerbose = true;
    }
    // trace( 'getSectionArray(): Called.' );

    _sectionArray = new Array<Point>();
    _sectionStrokeMap = new Map<Int, geom.Point>();

    /*
     * Make sure we have the array of sorted strokes.
     */
    getAngleArray();

    /*
    * A street point with a single street does not have any section array. 
    */
    if (_angleArray.length < 2)
    {
        return _sectionArray;
    }

    /*
     * A street point with two strokes does use the generic intersection, unless
     * they are perfectly collinear. They do not, however make up a polygon later
     * on but come next to each others.
     */

    if (myVerbose)
    {
        traceAngles();
    }

    var idx: Int = _angleArray.length - 1;
    /*
     * Iterate through our point array, intersecting the adjacent sides of the
     * previous and the current stroke. 
     * 
     * Note: As we use the infinite lines, the in/out orientation does not matter.
     */
    for (curr in _angleArray )
    {
        // trace( 'getSectionArray(): curr.angle is ${curr.angle}, ${curr.angle+Math.PI}.' );
        var prev = _angleArray[idx % _angleArray.length];
        if (curr != _angleArray[(idx + 1) % _angleArray.length])
        {
            throw 'StreetPoint.getSectionArray(): Mismatch of angle array.';
        }

        var sp = null;
        if (prev.a == this)
        {
            sp = new geom.Line(prev.a.pos.clone(), prev.b.pos.clone());
        }
        else
        {
            sp = new geom.Line(prev.b.pos.clone(), prev.a.pos.clone());
        }
        var sc = null;
        if (curr.a == this)
        {
            sc = new geom.Line(curr.a.pos.clone(), curr.b.pos.clone());
        }
        else
        {
            sc = new geom.Line(curr.b.pos.clone(), curr.a.pos.clone());
        }

        /*
         * Normals. Turn them in a way that, well
         * If I move from outside to this street point, the normal shall point
         * to the right side. That means, the normal points from the current to
         * the previous stroke.
         */
        var np = sp.normal();
        // if( prev.b == this ) { np.x = -np.x; np.y = -np.y; }
        var nc = sc.normal();
        // if( curr.b == this ) { nc.x = -nc.x; nc.y = -nc.y; }

        /*
         * Copy paste from generate street operator.
         */
        var prevHalfStreetWidth = prev.streetWidth() / 2.0;
        var currHalfStreetWidth = curr.streetWidth() / 2.0;

        /*
         * Scale each of the normals to properly move the line.
         * Compute the offets.
         */
        var opx = np.x * (-prevHalfStreetWidth);
        var opy = np.y * (-prevHalfStreetWidth);

        var ocx = nc.x * (currHalfStreetWidth);
        var ocy = nc.y * (currHalfStreetWidth);

        sp.move(opx, opy);
        sc.move(ocx, ocy);

        var i = sp.intersectInfinite(sc);

        var doUseSide = false;
        if (null == i)
        {
            if (myVerbose) trace('no inrtersect');
            doUseSide = true;
        }
        else
        {
            /*
             * If the intersection is too far away from the streetpoint, these are pretty in-line 
             * streets, so take their common border.
             */
            var dx = i.x - pos.x;
            var dy = i.y - pos.y;
            var dist2 = dx * dx + dy * dy;
            if (dist2 > 4000. )
            {
                if (myVerbose) trace('farout intersect');
                /*
                 * If this intersection is too far away, we use the point offset by the (angle)
                 * average of both normals.
                 */
                var n = new Point(nc.x - np.x, nc.y - np.y);
                n.unit();
                var averW = (prevHalfStreetWidth + currHalfStreetWidth) / 2.0;
                i = new Point(pos.x + n.x * averW, pos.y + n.y * averW);
#if 0
                    var dist = Math.sqrt(dist2);
                    var prevAngle = prev.getAngleSP(this);
                    var currAngle = curr.getAngleSP(this);
                    var junctionAngle = (currAngle - prevAngle)*180./Math.PI;
                    trace( 'When intersecting prev $sp and curr $sc (normals prev $np curr $nc): Far out intersection $i' );
                    trace( 'prevHalfStreetWidth is $prevHalfStreetWidth currHalfStreetWidth is $currHalfStreetWidth');
                    traceAngles();
                    throw ( 'getSectionArray(): Far out intersection $i {between ${prev.toStringSP(this)} and ${curr.toStringSP(this)}: $dist, $junctionAngle deg' );
                    // doUseSide = true;   
# end
                } else {
                    if( myVerbose ) trace( 'close intersect' );
                }
            }

            if( doUseSide ) {
                // trace( 'getSectionArray(): no intersection.' );
                /*
                 * If the streets are parallel and the sides in line, use the offset
                 * street point itself as an intersection, using the average street width.
                 */
                var averHalfStreetWidth = (prevHalfStreetWidth+currHalfStreetWidth)/2.0;
                var osx = nc.x * averHalfStreetWidth;
                var osy = nc.y * averHalfStreetWidth;
                i = new Point( pos.x+osx, pos.y+osy );
            }

            if( myVerbose ) {
                trace('Adding point $i');
            }
            _sectionArray.push(i);
            /*
             * Now also add this point to the stroke lookup. Obviously, it involves
             * two strokes, curr and prev, so add both associations.
             */
            var ids: Int = (curr.sid%10000)+10000*(prev.sid%10000);
            _sectionStrokeMap.set(ids, i);
            // trace('StreetPoint.getSectionArray(): sp $id Storing stroke $ids ${curr.sid} and ${prev.sid}');
            ++idx;
        }

        return _sectionArray;
    }


    /**
     * Return the intersection point involving the given stroke.
     * 
     * @param stroke 
     * @return geom.Point
     */
    public function getSectionPointByStroke( curr: Stroke, prev: Stroke ): geom.Point
    {
        if( null==_sectionArray ) {
            getSectionArray();
        }
        var ids: Int = (curr.sid%10000)+10000*(prev.sid%10000);
        // trace('StreetPoint.getSectionPointByStroke(): sp $id Obtaining stroke $ids ${curr.sid} and ${prev.sid}');
        if( !_sectionStrokeMap.exists( ids ) ) {
            // trace('StreetPoint.getSectionPointByStroke(): Not found.');
            return null;
        }
        // trace('StreetPoint.getSectionPointByStroke(): Returning point.');
        return _sectionStrokeMap[ids];
    }


    public function pushCreator(s: String): Void {
        creator = creator + ":" + s;
    }

    public function new () {
        id = _nextId++;
        pos = new Point( 0., 0. );
        inStore = false;
        _listStartingStrokes = null;
        _listEndingStrokes = null;
        _angleArray = null;
        _sectionArray = null;
        _sectionStrokeMap = null;
        creator = "";
    }
    }
#endif
}
