using System;
using System.Numerics;

namespace engine.geom
{
    public class Line
    {
        Vector2 a;
        Vector2 b;


        public String ToString()
        {
            return $"({a}-{b})";
        }

        public float Length()
        {
            float abx = b.X - a.X;
            float aby = b.Y - a.Y;

            return Math.Sqrt(abx* abx+aby* aby );
        }


    public function normal(): Point {
        var abx = b.x - a.x;
    var aby = b.y - a.y;
    var l = Math.sqrt(abx * abx + aby * aby);
        if(l<0.0000001 && l>-0.0000001) {
            trace('Line.normal(): Near null division');
            return new Point( 1.0, 0.0 );
}
return new Point(aby / l, -(abx / l));
    }

    public function move(px: Float, py: Float) {
    a.x += px;
    a.y += py;
    b.x += px;
    b.y += py;
}


/**
 * Compute the intersection of two infinite lines.
 * 
 * @param o 
 *     The line we might intersect to
 * @return 
 *     If the lines intersect, the point of intersection.
 */
public function intersectInfinite(o: Line ): Point
{
    /*
     * Create homogenous line coordinates.
     */
    var g1 = b.y - a.y;
    var g2 = a.x - b.x;
    var g3 = a.y * b.x - a.x * b.y;

    var h1 = o.b.y - o.a.y;
    var h2 = o.a.x - o.b.x;
    var h3 = o.a.y * o.b.x - o.a.x * o.b.y;

    /*
     * Intersection by Cramer
     */
    var d = g1 * h2 - h1 * g2;
    if (Math.abs(d) < 0.0000001)
    {
        // No close intersection.
        // trace('Line.intersectInfinite(): near-zero determinant. No intersection.');
        return null;
    }
    var px = (g2 * h3 - g3 * h2) / d;
    var py = (g3 * h1 - g1 * h3) / d;

    return new Point(px, py);
}

/**
 * Compute the intersection of this line with another.
 * 
 * @param o 
 *     The line we shall test to intersect with.
 * 
 * @return Point of intersection or null
 */
public function intersect(o: Line ): Point
{
    var p = intersectInfinite(o);
    if (null == p)
    {
        /*
         * Parallel lines.
         */
        return null;
    }

    /*
     * Check, where it is on the infinite line.
     * Compute dot product of ab * ap. If this is between zero
     * and ab*ab, p is in the right interval.
     */
    var abx = b.x - a.x;
    var aby = b.y - a.y;
    var apx = p.x - a.x;
    var apy = p.y - a.y;
    var dot = abx * apx + aby * apy;
    if (dot < 0.0 || dot > (abx * abx + aby * aby))
    {
        return null;
    }

    /*
     * This really is an intersection.
     */
    return p;
}

public function new(a0: Point, b0: Point)
{
    a = a0;
        b = b0;
    }
}    }
}
