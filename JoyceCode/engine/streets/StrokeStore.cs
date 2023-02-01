using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.streets
{
    public class StrokeStore
    {
        static private void trace(string message)
        {
            Console.WriteLine(message);
        }
        private List<Stroke> _listStrokes;
        private List<StreetPoint> _listPoints;

        private bool _traceStrokes;

        public StrokeIntersection IntersectsMayTouchClosest(in Stroke cand, in StreetPoint refSP)
        {
            StrokeIntersection closestIntersection = null;
            float closestDist = 100000000.0f;

            foreach(var stroke in _listStrokes) {
                var si = stroke.intersects(cand);

                // Default to collide.
                bool xx = true;
                if( null==si ) {
                    /*
                     * No collision record? Then this is not a collision.
                     */
                    xx = false;
                } else {
                    /*
                     * We have a collision. But is this probably just the end of the
                     * stroke ending at this point.
                     */
                    // TXWTODO: We are just checking the candidate's endpoint, not the ones from the store. Shouldn't we also do them?
                    if(Vector2.Distance( si.Pos, cand.A.Pos ) < 0.005f
                        || Vector2.Distance( si.Pos, cand.B.Pos ) < 0.005f
                        || Vector2.Distance( si.Pos, stroke.A.Pos ) < 0.005f
                        || Vector2.Distance( si.Pos, stroke.B.Pos ) < 0.005f
                    ) {
                        xx = false;
                    } else
                    { 
                        /*
                            * This is an intersection with this stroke.
                            */
                        var dxi = si.Pos.X - refSP.Pos.X;
                        var dyi = si.Pos.Y - refSP.Pos.Y;
                        var dist = dxi * dxi + dyi * dyi;
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestIntersection = si;
                        }
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
            return FindClosestToCoordBelowButNot(
                sp0.Pos.X, sp0.Pos.Y, sp0, minDist, spNot
            );
        }


        public StreetPoint? FindClosestToCoordBelowButNot(
            float x, float y,
            in StreetPoint sp0,
            float minDist,
            in StreetPoint spNot)
        {
            float minDist2 = minDist * minDist;
            float minDiff2 = 100000000f;
            StreetPoint? minSp = null;
            foreach(var sp in _listPoints )
            {
                if (sp0 != sp && spNot != sp)
                {
                    var dx = (x - sp.Pos.X);
                    var dy = (y - sp.Pos.Y);
                    var dist2 = dx * dx + dy * dy;
                    if (sp0 != null && sp != null && 539 == sp0.Id && 505 == sp.Id)
                    {
                        trace( $"dist2 is {dist2}");
                    }
                    if (dist2 < minDist2)
                    {
                        if (dist2 < minDiff2)
                        {
                            // trace('from sp0: ${sp0.pos} : sp: ${sp.pos} with $dist2');
                            minDiff2 = dist2;
                            minSp = sp;
                        }
                    }
                }
            }
            if (null != minSp)
            {
                // trace('Returning from sp0: ${sp0.pos} : sp: ${minSp.pos} with $minDiff2');
            }
            return minSp;
        }


        public StrokeIntersection? GetClosestStroke(
            in StreetPoint sp)
        {
            if (_traceStrokes) trace( $"getClosestStroke(): Testing point {sp.Pos.ToString()}");
            float closestDist = 100000f; // 100km
            Stroke closestStroke = null;

            foreach(var stroke in _listStrokes)
            {

                /*
                 * Skip stroke's end points.
                 */
                if (sp == stroke.A || sp == stroke.B)
                {
                    if (_traceStrokes) trace( $"Skipping stroke {stroke.ToString()}, because point is part of stroke.");
                    continue;
                }

                var dist = stroke.distance(sp.Pos.X, sp.Pos.Y);

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
                if (_traceStrokes) trace( $"Stroke in range for {si.Pos.X}, {si.Pos.Y}, length {closestStroke.Length} distance {closestDist}");
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
        public StrokeIntersection GetClosestPoint(in Stroke stroke)
        {
            if (_traceStrokes) trace($"GetClosestPoint(): Testing stroke {stroke.ToString()}");
            float closestDist = 100000f;  // 100km
            StreetPoint? closestPoint = null;

            foreach(var sp0 in _listPoints)
            {

                /*
                 * Skip stroke's end points.
                 */
                if (sp0 == stroke.A || sp0 == stroke.B)
                {
                    if (_traceStrokes) trace($"Skipping point {sp0.Pos.X}, {sp0.Pos.Y}, because its part of this stroke.");
                    continue;
                }

                float dist = stroke.distance(sp0.Pos.X, sp0.Pos.Y);

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
                if (_traceStrokes) trace($"Stroke in range for {si.Pos.X}, ${si.Pos.Y}, length {stroke.Length} distance $closestDist");
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
        public void Remove(in Stroke p) {
            if (null == p.Store)
            {
                throw new InvalidOperationException( "StrokeStore: Stroke not in any store.");
            }
            if (this != p.Store)
            {
                throw new InvalidOperationException( "StrokeStore: Stroke not in this store." );
            }

            _listStrokes.Remove(p);
            p.Store = null;
            p.A.RemoveStartingStroke(p);
            p.B.RemoveEndingStroke(p);
        }


        private void AddPoint(in StreetPoint sp)
        {
            if (sp.InStore)
            {
                throw new InvalidOperationException( $"StrokeStore.addPoint(): Unable to add point {sp.ToString()}: Already in store." );
            }
            if (_traceStrokes) trace( $"Adding point {sp}.");

            // TXWTODO: Probably do not include this in the final
            foreach(var spExist in _listPoints )
            {
                float dx = spExist.Pos.X - sp.Pos.X;
                float dy = spExist.Pos.Y - sp.Pos.Y;
                float dist2 = dx * dx + dy * dy;
                if (dist2 < 0.00000001f)
                {
                    throw new InvalidOperationException( $"StrokeStore.addPoint(): Refusing to add point {sp.ToString()}, found considerably close {spExist.ToString()}." );
                }
            }

            /*
             * For debugging purposes, find a considerably close point.
             */

            sp.InStore = true;
            _listPoints.Add(sp);
        }


        public void AddStroke(in Stroke stroke)
        {
            if (_traceStrokes) trace($"Adding stroke {stroke}");

            if (stroke.Store != null)
            {
                if (stroke.Store == this)
                {
                    throw new InvalidOperationException( $"StrokeStore.add(): Stroke already in this store." );
                }
                else
                {
                    throw new InvalidOperationException( $"StrokeStore.add(): Stroke already in other store." );
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
        }


        public bool AreConnected(in StreetPoint sp0, in StreetPoint sp1)
        {
            foreach(var stroke in _listStrokes)
            {
                if (stroke.A == sp0 && stroke.B == sp1
                    || stroke.B == sp0 && stroke.A == sp1)
                {
                    if (_traceStrokes) trace( $"StrokeStore(): Already connected in {stroke.ToString()}.");
                    return true;
                }
            }
            return false;
        }


#if false
        /**
         * Starting from the given streetpoint, navigate to the next stroke.
         */
        public function getNextStroke(
            sp0: StreetPoint,
            str0: Stroke
        ) {
            var spNew: StreetPoint = null;
            if( str0.a == sp0 ) {
                spNew = str0.b;
            } else if( str0.b == sp0 ) {
                spNew = str0.a;
            } else {
                throw 'StrokeStore.getNextStroke(): Called with streetpoint/stroke pair that does not match.';
            }
            var angleOld = str0.angle;

            /*
             * Now, for a clockwise loop, we need to find the stroke
             * starting/ending in spNew with the smallest angle greater than
             * my angle. In that case, it does not matter whether this is a
             * starting or an ending stroke.
             */
            var strNew = spNew.getNextAngle( str0, angleOld, true /* clockwise */ );
        }
#endif


        public List<Stroke> GetStrokes()
        {
            return _listStrokes;
        }


        public List<StreetPoint> GetStreetPoints()
        {
            return _listPoints;
        }


        public void ClearTraversed() {
            foreach( var stroke in _listStrokes ) {
                stroke.TraversedAB = false;
                stroke.TraversedBA = false;
            }
        }


        public StrokeStore()
        {
            _listStrokes = new List<Stroke>();
            _listPoints = new List<StreetPoint>();
        }
    }
}
