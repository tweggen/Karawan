using System;
using System.Numerics;

namespace engine.streets
{
    public class Stroke
    {
        static private void trace(string message)
        {
            Console.WriteLine(message);
        }
        static private int _nextId = 10000;

        public int Sid;

        public StrokeStore? Store;

        /**
         * The creator of this stroke.
         */
        public string Creator { get; private set; }

        /**
         * The weight of this stroke: Import streets will have a higher weight, 
         * some side alleys will have lower weight.
         */
        public float Weight;


        /**
         * Wether this one goes in primary or secondary direction.
         */
        public bool IsPrimary;


        /**
         * The point this stroke is coming from.
         */
        public StreetPoint A { get; set; }


        /**
         * The point this stroke is going to.
         */
        public StreetPoint B { get; set; }


        /**
         * Several graph algorithms will try to traveres throught the 
         * strokes. They need to remember which ones they already 
         * encountered.
         */
        public bool TraversedAB;

        /**
         * Several graph algorithms will try to traveres throught the 
         * strokes. They need to remember which ones they already 
         * encountered.
         */
        public bool TraversedBA;


        /**
         * Whether the geometry is valid.
         */
        private bool _isValid;


        private float _angle;
        /**
         * The angle of the street.
         */
        public float Angle 
        {
            get
            {
                if (!_isValid) _updateGeom();
                return _angle;
            }
        }


        private float _length;
        /**
         * The length of the stroke.
         */
        public float Length 
        {
            get
            {
                if (!_isValid) _updateGeom();
                return _length;
            }
        }


        /**
         * The normal for this stroke. Right-hand-side, if travelling 
         * from a to b.
         */
        private Vector2 _normal;
        public Vector2 Normal
        {
            get
            {
                if (!_isValid) _updateGeom();
                return _normal;
            }
        }

        /**
         * The unit vector for this stroke
         */
        private Vector2 _unit;
        public Vector2 Unit 
        { 
            get
            {
                if (_isValid) _updateGeom();
                return _unit;
            }
        }


        private void _updateGeom()
        {
            float dx = B.Pos.X - A.Pos.X;
            float dy = B.Pos.Y - A.Pos.Y;

            _length = (float) Math.Sqrt(dx* dx+dy* dy);
            if(_length<0.000001) {
                throw new InvalidOperationException( "Stroke._updateGeom(): Zero length stroke." );
            }

            /*
             * Make it a unit vector to have better NaN thesholds
             */
            var prevPhi = _angle;
            dx = dx / _length;
            dy = dy / _length;
            float phi = (float) Math.Atan2(dy, dx);
            if(Math.Abs(prevPhi-phi) > 0.00000001 ) {
                _angle = phi;
            }
            _unit = new Vector2(dx, dy);
            _normal = new Vector2(dy, -dx);
            _isValid = true;
        }


        public void invalidate()
        {
            _angle = -1000f;
            _unit = new Vector2();
            _normal = new Vector2();
            _length = 0f;
            _isValid = false;
        }

        /**
         * Debug function to test, whether stroke was valid
         */
        public bool isValid()
        {
            return _isValid;
        }

        public StreetPoint set_a(StreetPoint sp)
        {
            if (null != Store)
            {
                throw new InvalidOperationException( "Stroke: Tried to exchange endpoint while in graph." );
            }
            if (A != null)
            {
                A.Invalidate();
                A = null;
            }
            invalidate();
            A = sp;
            if (sp != null)
            {
                sp.Invalidate();
            }
            return sp;
        }


        public StreetPoint set_b(StreetPoint sp)
        {
            if (null != Store)
            {
                throw new InvalidOperationException( "Stroke: Tried to exchange endpoint while in graph." );
            }
            if (B != null)
            {
                B.Invalidate();
                B = null;
            }
            invalidate();
            B = sp;
            if (sp != null)
            {
                sp.Invalidate();
            }
            return sp;
        }


        public float GetAngleSP(in StreetPoint sp)
        {
            var isEnding = false;
            if (sp == B)
            {
                isEnding = true;
            }
            else if (sp == A)
            {
                // Just checking
            }
            else
            {
                throw new InvalidOperationException( $"Stroke.getAngleSP(): Invalid stroke #{Sid} that does not contain street point {sp.Id}" );
            }
            return Angle + (isEnding ? (float)Math.PI : 0f);
        }


        /**
         * strokewidth currently ranges from 0.5 to 1.5, with most branches around 0.65
         * We use (weight-0.5)^2 * 4, that is output range from 0. to 4.
         * Minimal width 6m, maximal 22m, 
         */
        public float StreetWidth()
        {
#if false
            return 20f;
#else
            var w = Weight;
            //w = w - 0.5;
            if (w < 0f ) w = 0f;
            return 7.5f + w * w * 10f;
#endif
        }


        /**
         * Stolen from https://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect
         */
        public StrokeIntersection intersects(in Stroke o)
        {
            if (null == o)
            {
                throw new InvalidOperationException( "Stroke: intersect arg is null." );
            }
            var p0_x = A.Pos.X;
            var p0_y = A.Pos.Y;
            var p1_x = B.Pos.X;
            var p1_y = B.Pos.Y;

            var p2_x = o.A.Pos.X;
            var p2_y = o.A.Pos.Y;
            var p3_x = o.B.Pos.X;
            var p3_y = o.B.Pos.Y;

            var i_x = 0f;
            var i_y = 0f;
            var does = false;

            float s1_x, s1_y, s2_x, s2_y;

            s1_x = p1_x - p0_x; s1_y = p1_y - p0_y;
            s2_x = p3_x - p2_x; s2_y = p3_y - p2_y;

            var d = (-s2_x * s1_y + s1_x * s2_y);
            if (d < 0.0000001f && d > -0.0000001f)
            {
                // trace('Stroke.intersects(): near-zero determinant. No intersection.');
                return null;
            }

            float s, t;
            s = (-s1_y * (p0_x - p2_x) + s1_x * (p0_y - p2_y)) / d;
            t = (s2_x * (p0_y - p2_y) - s2_y * (p0_x - p2_x)) / d;

            // if (s >= 0 && s <= 1 && t >= 0 && t <= 1) {
            if (s >= 0f && s <= 1f && t >= 0f && t <= 1f)
            {
                // Collision detected
                i_x = p0_x + (t * s1_x);
                i_y = p0_y + (t * s1_y);

                var intersection = new StrokeIntersection();
                intersection.strokeCand = o;
                intersection.scaleCand = s;
                intersection.strokeExists = this;
                intersection.scaleExists = t;
                intersection.pos = new Vector2(i_x, i_y);

                return intersection;
            }

            return null; // No collision
        }


        /**
         * Compute the distance of the given point to this stroke.
         */
        public float distance(float px, float py)
        {
            var abx = B.Pos.X - A.Pos.X;
            var aby = B.Pos.Y - A.Pos.Y;
            var acx = px - A.Pos.X;
            var acy = py - A.Pos.Y;

            var dotproduct = abx * acx + aby * acy;

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

            var crossproduct = abx * acy - aby * acx;
            if (Length < 0.0000001f && Length > -0.0000001f)
            {
                trace("Stroke.distance(): near null division.");
                return 1000000000f;
            }
            var dist = Math.Abs(crossproduct) / Length;
            // trace( 'dist=$dist');

            /*
             * Now look, whether this stroke is in range at all.
             */

            /*
            * Compute the distance between A and the projection of C on AB.
            * (pythagoras)
            */
            float ac2 = acx * acx + acy * acy;
            float ad2 = ac2 - dist * dist;
            float ad = (float) Math.Sqrt(ad2);

            if (ad >= Length)
            {
                //trace( 'Skipping point ${sp0.pos.x}, ${sp0.pos.y}, because its beyond stroke.');
                return 1000000000f;
            }

            return dist;
        }

        private void _copyMetaFrom(in Stroke o) 
        {
            IsPrimary = o.IsPrimary;
            Weight = o.Weight;
            invalidate();
        }


        /**
         * Create a copy of this object.
         *
         * The points are used as in this stroke.
         * Howevere, the stroke is not added to the stroke store and therefore
         * also not added to the StreetPoints.
         */
        public Stroke CreateUnattachedCopy()
        {
            var stroke = new Stroke();
            stroke.A = A;
            stroke.B = B;
            stroke._copyMetaFrom(this);
            return stroke;
        }


        /**
         * Create a new stroke from the given street point.
         * Compute the coordinates for the target streetpoint.
         */
        public static Stroke CreateByAngleFrom(
            in StreetPoint a0,
            in StreetPoint b0,
            float angle0,
            float length0,
            bool isPrimary0,
            float weight0)
        {
            /*
             * Round the angle to avoid rounding artefacts
             */
            {
                var angle0int = (int)(angle0 * 180f/ (float)Math.PI);
                if (angle0int > 180)
                {
                    angle0int -= 360;
                }
                else if (angle0int < -180)
                {
                    angle0int += 180;
                }
                angle0 = angle0int * (float)Math.PI / 180f;
            }
            var stroke = new Stroke();

            b0.SetPos(
                a0.Pos.X + (float) Math.Cos(angle0) * length0,
                a0.Pos.Y + (float) Math.Sin(angle0) * length0
            );
            stroke.A = a0;
            stroke.B = b0;

            stroke.IsPrimary = isPrimary0;
            stroke.Weight = weight0;

            return stroke;
        }


        public string ToString()
        {
            var ax = A.Pos.X;
            var ay = A.Pos.Y;
            var bx = B.Pos.X;
            var by = B.Pos.Y;
            return $"{Sid}: ^{Angle} ({A.ToString()}-({B.ToString()}) ('{Creator}')";
        }


        public string ToStringSP(in StreetPoint sp)
        {
            var ax = A.Pos.X;
            var ay = A.Pos.Y;
            var bx = B.Pos.X;
            var by = B.Pos.Y;
            float relativeAngle = Angle;
            if (B == sp)
            {
                relativeAngle += (float) Math.PI;
            }
            else if (A == sp)
            {
                // nothing
            }
            else
            {
                throw new InvalidOperationException( $"Stroke.toStringSP(): Inconsistent angle, sp not in stroke." );
            }
            return $"{Sid}: ^{relativeAngle} ({A.ToString()}-({B.ToString()}) ('{Creator}')";
        }


        public void pushCreator(in string s)
        {
            Creator = Creator + ":" + s;
        }


        private Stroke()
        {
            Sid = ++_nextId;
            A = null; //new StreetPoint();
            B = null; //new StreetPoint();
            Store = null;
            _angle = -1000f;
            _length = 0f;
            TraversedAB = false;
            TraversedBA = false;
            _unit = new Vector2();
            _normal = new Vector2();
            _isValid = false;
            Creator = "";
        }
    }
}
