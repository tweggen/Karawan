using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using static engine.Logger;

namespace engine.streets
{
    public class Stroke
    {
        static private object _classLock = new();
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
        private StreetPoint _a;
        public StreetPoint A 
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _a;
            set { _setA(value); }
        }


        /**
         * The point this stroke is going to.
         */
        private StreetPoint _b;

        public StreetPoint B
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _b;
            set { _setB(value); }
        }


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
        private bool _isLengthValid;
        private bool _isAngleValid;
        private bool _isUnitValid;


        private float _angle;
        /**
         * The angle of the street.
         */
        public float Angle 
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!_isAngleValid) _updateAngle();
                return _angle;
            }
        }


        private float _length;
        private float _length2;
        private Vector2 _vAB;
        /**
         * The length of the stroke.
         */
        public float Length 
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!_isLengthValid) _updateLength();
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!_isUnitValid) _updateUnit();
                return _normal;
            }
        }

        /**
         * The unit vector for this stroke
         */
        private Vector2 _unit;
        public Vector2 Unit 
        { 
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!_isUnitValid) _updateUnit();
                return _unit;
            }
        }


        private void _updateLength()
        {
            _vAB = _b.Pos - _a.Pos;
            _length2 = _vAB.LengthSquared();
            _length = Single.Sqrt(_length2);
            if(_length<0.000001) {
                throw new InvalidOperationException( "Stroke._updateGeom(): Zero length stroke." );
            }

            _isLengthValid = true;
        }


        private void _updateUnit()
        {
            if (!_isLengthValid) _updateLength();
            _unit = _vAB / _length;
            _normal = new Vector2(_unit.Y, -_unit.X);
            
            _isUnitValid = true;
        }
        

        private void _updateAngle()
        {
            if (!_isUnitValid) _updateUnit();
            float prevPhi = _angle;
            float phi = Single.Atan2(_unit.Y, _unit.X);
            if(Single.Abs(prevPhi-phi) > 0.00000001 ) {
                _angle = phi;
            }

            _isAngleValid = true;
        }
        

        private void _setA(StreetPoint sp)
        {
            if (null != Store)
            {
                throw new InvalidOperationException( "Stroke: Tried to exchange endpoint while in graph." );
            }
            if (_a != null)
            {
                _a.Invalidate();
                _a = null;
            }
            Invalidate();
            _a = sp;
            if (sp != null)
            {
                sp.Invalidate();
            }
        }


        private StreetPoint _setB(StreetPoint sp)
        {
            if (null != Store)
            {
                throw new InvalidOperationException( "Stroke: Tried to exchange endpoint while in graph." );
            }
            if (_b != null)
            {
                _b.Invalidate();
                _b = null;
            }
            Invalidate();
            _b = sp;
            if (sp != null)
            {
                sp.Invalidate();
            }
            return sp;
        }

        
        public float GetAngleSP(in StreetPoint sp)
        {
            var isEnding = false;
            if (sp == _b)
            {
                isEnding = true;
            }
            else if (sp == _a)
            {
                // Just checking
            }
            else
            {
                throw new InvalidOperationException( $"Stroke.getAngleSP(): Invalid stroke #{Sid} that does not contain street point {sp.Id}" );
            }
            return Angle + (isEnding ? Single.Pi : 0f);
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
            return 6.5f + w * w * 10f;
#endif
        }


        /**
         * Stolen from https://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect
         */
        public StrokeIntersection? Intersects(in Stroke o)
        {
            if (null == o)
            {
                throw new InvalidOperationException( "Stroke: intersect arg is null." );
            }
            if (!_isLengthValid) _updateLength();
            if (!o._isLengthValid) o._updateLength();
            
            var p0_x = _a.Pos.X;
            var p0_y = _a.Pos.Y;
            //var p1_x = _b.Pos.X;
            //var p1_y = _b.Pos.Y;

            var p2_x = o._a.Pos.X;
            var p2_y = o._a.Pos.Y;
            //var p3_x = o._b.Pos.X;
            //var p3_y = o._b.Pos.Y;

            var i_x = 0f;
            var i_y = 0f;
            var does = false;

            float s1_x, s1_y, s2_x, s2_y;

            //s1_x = p1_x - p0_x; s1_y = p1_y - p0_y;
            s1_x = _vAB.X;
            s1_y = _vAB.Y;
            //s2_x = p3_x - p2_x; s2_y = p3_y - p2_y;
            s2_x = o._vAB.X;
            s2_y = o._vAB.Y;

            var d = (-s2_x * s1_y + s1_x * s2_y);
            if (d < 0.0000001f && d > -0.0000001f)
            {
                // Trace("Stroke.intersects(): near-zero determinant. No intersection.");
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

                var intersection = new StrokeIntersection(
                    pos: new Vector2(i_x, i_y),
                    strokeCand: o,
                    scaleCand: s,
                    strokeExists: this,
                    scaleExists: t
                );

                return intersection;
            }

            return null; // No collision
        }


        /**
         * Compute the distance of the given point to this stroke.
         */
        public float Distance(in Vector2 p)
        {
            if (!_isLengthValid) _updateLength();
            
            //var abx = B.Pos.X - A.Pos.X;
            //var aby = B.Pos.Y - A.Pos.Y;
            float acx = p.X - _a.Pos.X;
            float acy = p.Y - _a.Pos.Y;

            float dotproduct = _vAB.X * acx + _vAB.Y * acy;

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

            float crossproduct = _vAB.X * acy - _vAB.Y * acx;
            float dist = Single.Abs(crossproduct) / _length;

            /*
             * Now look, whether this stroke is in range at all.
             */

            /*
            * Compute the distance between A and the projection of C on AB.
            * (pythagoras)
            */
            float ac2 = acx * acx + acy * acy;
            float ad2 = ac2 - dist * dist;

            if (ad2 >= _length2)
            {
                return 1000000000f;
            }

            return dist;
        }

        private void _copyMetaFrom(in Stroke o) 
        {
            IsPrimary = o.IsPrimary;
            Weight = o.Weight;
            Invalidate();
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
                var angle0int = (int)(angle0 * 180f/ Single.Pi);
                if (angle0int > 180)
                {
                    angle0int -= 360;
                }
                else if (angle0int < -180)
                {
                    angle0int += 180;
                }
                angle0 = angle0int * Single.Pi / 180f;
            }
            var stroke = new Stroke();

            b0.SetPos(
                a0.Pos.X + Single.Cos(angle0) * length0,
                a0.Pos.Y + Single.Sin(angle0) * length0
            );
            stroke.A = a0;
            stroke.B = b0;

            stroke.IsPrimary = isPrimary0;
            stroke.Weight = weight0;

            return stroke;
        }


        public override string ToString()
        {
            return $"{Sid}: ^{Angle} ({A.ToString()}-({B.ToString()}) ('{Creator}')";
        }


        public string ToStringSP(in StreetPoint sp)
        {
            float relativeAngle = Angle;
            if (B == sp)
            {
                relativeAngle += Single.Pi;
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


        public void PushCreator(in string s)
        {
            Creator = Creator + ":" + s;
        }


        public void Invalidate()
        {
            /*
             * Reset the angle to something impossible so that we are forced to recompute.
             */
            _angle = -1000f;
            _unit = Vector2.Zero;
            _normal = Vector2.Zero;
            _length = 0f;
            _length2 = 0;
            _isLengthValid = false;
            _isUnitValid = false;
            _isAngleValid = false;
        }


        private Stroke()
        {
            lock (_classLock)
            {
                Sid = ++_nextId;
            }

            _a = null;
            _b = null;
            Store = null;

            TraversedAB = false;
            TraversedBA = false;
            Creator = "";

            Invalidate();
        }
    }
}
