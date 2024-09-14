using System;
using System.Numerics;
using static engine.Logger;

namespace engine.geom
{
    public class Line
    {
        public Vector2 A;
        public Vector2 B;


        public override String ToString()
        {
            return $"({A}-{B})";
        }

        public float Length()
        {
            float abx = B.X - A.X;
            float aby = B.Y - A.Y;

            return (float)Math.Sqrt(abx*abx+aby*aby);
        }


        public Vector2 Normal() 
        {
            float abx = B.X - A.X;
            float aby = B.Y - A.Y;
            float l = (float)Math.Sqrt(abx * abx + aby * aby);
            if (l<0.0000001f && l>-0.0000001f) {
                Warning( "Near null division" );
                return new Vector2( 1.0f, 0.0f );
            }
            return new Vector2(aby / l, -(abx / l));
        }
        

        public void Move(float px, float py)
        {
            A.X += px;
            A.Y += py;
            B.X += px;
            B.Y += py;
        }


        /**
         * Compute the intersection of two infinite lines.
         * 
         * @param o 
         *     The line we might intersect to
         * @return 
         *     If the lines intersect, the point of intersection.
         */
        public Nullable<Vector2> IntersectInfinite(in Line o)
        {
            /*
             * Create homogenous line coordinates.
             */
            var g1 = B.Y - A.Y;
            var g2 = A.X - B.X;
            var g3 = A.Y * B.X - A.X * B.Y;

            var h1 = o.B.Y - o.A.Y;
            var h2 = o.A.X - o.B.X;
            var h3 = o.A.Y * o.B.X - o.A.X * o.B.Y;

            /*
             * Intersection by Cramer
             */
            var d = g1 * h2 - h1 * g2;
            if ((float)Math.Abs(d) < 0.0000001f)
            {
                // No close intersection.
                // trace('Line.intersectInfinite(): near-zero determinant. No intersection.');
                return null;
            }
            var px = (g2 * h3 - g3 * h2) / d;
            var py = (g3 * h1 - g1 * h3) / d;

            return new Vector2(px, py);
        }

        /**
         * Compute the intersection of this line with another.
         * 
         * @param o 
         *     The line we shall test to intersect with.
         * 
         * @return Point of intersection or null
         */
        public Nullable<Vector2> Intersect(in Line o)
        {
            var p0 = IntersectInfinite(o);
            if (null == p0)
            {
                /*
                 * Parallel lines.
                 */
                return null;
            }
            var p = p0.Value;

            /*
             * Check, where it is on the infinite line.
             * Compute dot product of ab * ap. If this is between zero
             * and ab*ab, p is in the right interval.
             */
            var abx = B.X - A.X;
            var aby = B.Y - A.Y;
            var apx = p.X - A.X;
            var apy = p.Y - A.Y;
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
        
        
        public static float Distance(in Vector3 a, in Vector3 b, in Vector3 p)
        {
            Vector3 vAC = p - a;
            Vector3 vAB = b - a;

            float distA = vAC.Length();
            float distB = (p - b).Length();
            float distPointMin = Single.Min(distA, distB);

            float dotproduct = Vector3.Dot(vAC, vAB);

            /*
             * If the dot product is negative, the point is "before" point a anyway. However,
             * it still could be too close to a. We should, however, already have checked the proximity
             * of each of the points to each other.
             */
            if (dotproduct < 0f)
            {
                // trace( 'Skipping point ${sp0.pos.x}, ${sp0.pos.y}, because its on the wrong side.');

                // TXWTODO: Compute end of line distance?
                return distA;
            }

            float length2 = vAB.LengthSquared();
            float length = Single.Sqrt(length2);
            Vector3 v3Cross = Vector3.Cross(vAB, vAC);
            float dist = v3Cross.Length() / length;

            /*
             * Now look, whether this stroke is in range at all.
             */

            /*
             * Compute the distance between A and the projection of C on AB.
             * (pythagoras)
             */
            float ac2 = vAC.LengthSquared();
            float ad2 = ac2 - dist * dist;

            if (ad2 >= length2)
            {
                return distB;
            }

            return dist;
        }

        
        public static float Distance(in Vector2 a, in Vector2 b, in Vector2 p)
        {
            float acx = p.X - a.X;
            float acy = p.Y - a.Y;
            Vector2 vAB = b - a;

            float dotproduct = vAB.X * acx + vAB.Y * acy;

            /*
             * If the dot product is negative, the point is "before" point a anyway. However,
             * it still could be too close to a. We should, however, already have checked the proximity
             * of each of the points to each other.
             */
            if (dotproduct < 0f)
            {
                // trace( 'Skipping point ${sp0.pos.x}, ${sp0.pos.y}, because its on the wrong side.');

                // TXWTODO: Compute end of line distance?
                return 1000000000f;
            }

            float length2 = vAB.LengthSquared();
            float length = Single.Sqrt(length2);
            float crossproduct = vAB.X * acy - vAB.Y * acx;
            float dist = Single.Abs(crossproduct) / length;

            /*
             * Now look, whether this stroke is in range at all.
             */

            /*
             * Compute the distance between A and the projection of C on AB.
             * (pythagoras)
             */
            float ac2 = acx * acx + acy * acy;
            float ad2 = ac2 - dist * dist;

            if (ad2 >= length2)
            {
                return 1000000000f;
            }

            return dist;
        }

        
        public Line(Vector2 a0, Vector2 b0)
        {
            A = a0;
            B = b0;
        }    
    }
}
