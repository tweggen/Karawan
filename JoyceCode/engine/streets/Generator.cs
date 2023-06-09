﻿using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.streets
{
    public class Generator
    {
        private void trace(string message)
        {
            Console.WriteLine(message);
        }
        private List<Stroke> _listStrokesToDo;
        private StrokeStore _strokeStore;
        private bool _traceGenerator = false;

        private int _generationCounter;
        private engine.RandomSource _rnd;

        private Vector2 _bl;
        private Vector2 _tr;

        public float minPointToCandPointDistance { get; set; } = 20f;
        public float minPointToCandStrokeDistance { get; set; } = 20f;
        public float minPointToCandIntersectionDistance { get; set; } = 20f;

        /*
         * All proabilities are given in the 0..256 range to avoid differrences 
         * between platforms due to rounding errors on floats.
         */
        public int probabilityNextStrokeForward { get; set; } = 252;
        public int probabilityNextStrokeRightWeightFactor { get; set; } = 150;
        public int probabilityNextStrokeLeftWeightFactor { get; set; } = 150;
        public int probabilityNextStrokeRandomNegWeightFactor { get; set; } = 240;
        public int probabilityNextStrokeIncreaseWeight { get; set; } = 8;
        public int probabilityNextStrokeDecreaseWeight { get; set; } = 77;

        public float  newStrokeMinimum { get; set; } = 40f;
        public float newStrokeLinearWeight { get; set; } = 0f;
        public float newStrokeSquaredWeight { get; set; } = 40f;
        public float newLengthMin { get; set;  } = 50f;

        public float weightIncreaseFactor { get; set; } = 1.1f;
        public float weightDecreaseFactor { get; set; } = 0.9f;
        public float weightMin { get; set; } = 0.1f;
        public float weightMax { get; set; } = 1.9f;

        public float probabilityAngleSlightTurn { get; set; } = 20f;
        public int AngleSlightTurnMax { get; set; } = 6;

        public float AngleMinStrokes { get; set; } = 30.0f;

        /**
         * Return the primary direction at the given point.
         */
        public Vector2 PrimaryVector(Vector2 origin)
        {
            return new Vector2(0f, 1f);
        }

        /**
         * Return the secondary direction at the given point.
         */
        public Vector2 SecondaryVector(Vector2 origin)
        {
            return new Vector2(1f, 0f);
        }


        /**
         * Find the first stroke in the list the candidate intersects with
         * and return it.
         */
        private bool _inBounds(Stroke cand)
        {
            return !(false
                || cand.A.Pos.X < _bl.X
                || cand.A.Pos.Y < _bl.Y
                || cand.A.Pos.X > _tr.X
                || cand.A.Pos.Y > _tr.Y);
        }


        private bool _haveStrokesToDo()
        {
            return _listStrokesToDo.Count > 0;
        }

        private Stroke _popStrokeToDo()
        {
            var idx = _listStrokesToDo.Count - 1;
            Stroke stroke = _listStrokesToDo[idx];
            _listStrokesToDo.RemoveAt(idx);
            return stroke;
        }

        private void _addStrokeToDo(in Stroke stroke)
        {
            _listStrokesToDo.Add(stroke);
        }


        /**
         * Iterate until the queue of strokes is empty again.
         */
        public void Generate()
        {
            while (true)
            {

                if (1000 < _generationCounter)
                {
                    if (_traceGenerator) trace("Generator: Returning: max generations reached.");
                    return;
                }

                if (!_haveStrokesToDo())
                {
                    if (_traceGenerator) trace("Generator: Returning: no more streets to do.");
                    return;
                }

                Stroke curr = _popStrokeToDo();
                // trace( 'Generator: Starting new generation.' );

                /*
                 * Check, wether this segment is valid.
                 */

                /*
                 * In bounds of the desired area?
                 */
                if (!_inBounds(curr))
                {
                    if (_traceGenerator) trace($"Generator: Out of bounds: {curr.ToString()}");
                    /*
                     * Out of range, so discard it.
                     */
                    continue;
                }

                /*
                 * Is there a street point close enough to use it?
                 *
                 * (in this implementation we assume no other pair of street points
                 * is too close to each other).
                 */

                /*
                 * TXWTODO: This actually is the wrong name. The proper would be: Stop to
                 * loop and do not add the current thing.
                 */
                bool continueCheck = true;
                bool doAdd = true;

                while (continueCheck)
                {

                    /*
                     * Is the end of the stroke too close to the beginning of the stroke?
                     */
                    if (Vector2.Distance(curr.A.Pos, curr.B.Pos) < minPointToCandPointDistance)
                    {
                        if (_traceGenerator)
                        {
                            trace("Generator: Discarding candidate: is too short. $curr");
                        }
                        /*
                         * Test: If both are in store, we have an invalid entry in the store.
                         */
                        if (curr.A.InStore && curr.B.InStore)
                        {
                            throw new InvalidOperationException( "Generator: (test too short) Found too close points (curr.a and curr.b) both in store" );
                        }
                        doAdd = false;
                        continueCheck = false;
                        continue;
                    }

                    /*
                     * Check: Is any of our endpoints too close to an existing endpoint?
                     */
                    {
                        StreetPoint tooClose = _strokeStore.FindClosestBelowButNot(
                            curr.A, minPointToCandPointDistance, curr.B);

                        if (null != tooClose)
                        {
                            if (_traceGenerator)
                            {
                                trace($"Generator: StreetPoint ({curr.A}) too close to StreetPoint ({tooClose}) ");
                            }

                            /*
                            * If a is too close, it better not be in the store.
                            */
                            if (curr.A.InStore)
                            {
                                // TXWTODO: Just commented this out. Is that good?
                                // throw new InvalidOperationException($"Generator: (check new a too close to existing) curr.a is in store ({curr.ToString()})");
                            }

                            /*
                             * Anyway, we won't add the stroke. 
                             * b might be in the store or not, I don't thinbk about it.
                             */
#if false
                             /*
                             * if a is close to another existing point, use that one.
                             */
                            curr.a = tooClose;
#endif
                            doAdd = false;
                            continueCheck = false;
                            continue;
                        }
                    }

                    {
                        StreetPoint tooClose = _strokeStore.FindClosestBelowButNot(
                            curr.B, minPointToCandPointDistance, curr.A );

                        if( null != tooClose ) {
                            if( _traceGenerator ) {
                                trace($"Generator: StreetPoint (${curr.B}) too close to StreetPoint ({tooClose})");
                            }

                            /*
                             * Validate assumptions: tooClose needs to be in store, curr.b needs to be not in the store.
                             */
                            if( !curr.A.InStore ) {
                                // Why do I check this? I would't move A.
                                // throw new InvalidOperationException( $"Generator: (check new b too close to existing) curr.a is not in store ({curr.ToString()})");
                            }
                            if( curr.B.InStore ) {
                                throw new InvalidOperationException( $"Generator: (check new b too close to existing) curr.b is in store ({curr.ToString()})");
                            }

                            /*
                             * Validate assumption: We must not move a stroke that already is in the store.
                             * However, in the store it already needs to have valid endpoints.
                             */
                            if( curr.Store != null ) {
                                throw new InvalidOperationException( $"Generator: (check new b too close to existing) curr already is in store ({curr})");
                            }

                            /*
                             * if a is close to another existing point, use that one.
                             */
                            curr.B = tooClose;

                            // Leave this loop.
                            // doAdd = false;
                            // continueCheck = false;
                            // break;
                            continue;
                        }
                    }
                    /*
                     * Now test whether the given points already are connected?
                     */
                    if( curr.A.InStore && curr.B.InStore ) {
                        if( _strokeStore.AreConnected(curr.A, curr.B)) {
                            doAdd = false;
                            continueCheck = false;
                            break;
                        }
                    }

                    /*
                     * Look, whether the stroke is too close to an existing point
                     */
                    {
                        var si = _strokeStore.GetClosestPoint( curr );
                        if( si != null && si.ScaleExists < minPointToCandStrokeDistance ) {
                            if( _traceGenerator ) trace( $"Generator: Discarding stroke {curr.ToString()}, too close to point: {si.StreetPoint}" );
                            /*
                             * If there is any point closer the d meters to this stroke,
                             * then [look, which point is closer to the stroke and connect
                             * it instead] drop it.
                             */
                            doAdd = false;
                            continueCheck = false;
                            break;
                        }
                    }

                    /*
                     * Look, whether the new point to is too close to an existing stroke
                     */
                    {
                        var si = _strokeStore.GetClosestStroke( curr.B );
                        if( si != null && si.ScaleExists < minPointToCandStrokeDistance ) {
                            if( _traceGenerator ) trace( $"Generator: Discarding stroke {curr.ToString()}, point b too close to stroke: {si.StrokeExists}" );

                            /*
                             * If there is any point closer the d meters to this stroke,
                             * then [look, which point is closer to the stroke and connect
                             * it instead] drop it.
                             */
                            doAdd = false;
                            continueCheck = false;
                            break;
                        }
                    }

                    /*
                     * Look if the stroke would be too close to an existing one.
                     */
                     {
                        /*
                         * Anything too close in point a?
                         */
                        var angles = curr.A.GetAngleArray();
                        var closestAngle = 9.0;
                        // its an incoming angle wrt a.
                        var myAngle = curr.Angle;
                        foreach( var stroke in angles ) {
                            float candAngle = stroke.GetAngleSP(curr.A);
                            float thisAngle = (float) Math.Abs(geom.Angles.Snorm( candAngle - myAngle ));
                            if( thisAngle < closestAngle ) {
                                closestAngle = thisAngle;
                            }
                        }
                        if( closestAngle < (AngleMinStrokes*(float)Math.PI/180f) ) {
                            if( _traceGenerator ) trace( $"Generator: Discarding stroke {curr.ToStringSP(curr.A)}, angle too close {closestAngle}" );
                            doAdd = false;
                            continueCheck = false;
                            break;
                        }
                    }

                    {
                        /*
                         * Anything too close in point b?
                         */
                        var angles = curr.B.GetAngleArray();
                        float closestAngle = 9.0f;
                        // its an incoming angle wrt b.
                        float myAngle = curr.Angle + (float) Math.PI;
                        foreach( var stroke in angles ) {
                            var candAngle = stroke.GetAngleSP(curr.B);
                            var thisAngle = Math.Abs(geom.Angles.Snorm( candAngle - myAngle ));
                            if( thisAngle < closestAngle ) {
                                closestAngle = thisAngle;
                            }
                        }
                        if( closestAngle < (AngleMinStrokes*(float) Math.PI/180f) ) {
                            if( _traceGenerator ) trace( $"Generator: Discarding stroke {curr.ToStringSP(curr.B)}, angle too close {closestAngle}" );
                            doAdd = false;
                            continueCheck = false;
                            break;
                        }
                    }

                    /*
                     * Neither of the endpoints is too close to an existing one.
                     * However, this new stroke still could intersect with another 
                     * stroke. Test this.
                     */
                    StrokeIntersection? intersection  = _strokeStore.IntersectsMayTouchClosest(curr, curr.A);
                    if( null != intersection ) {
                        /*
                         * Logical error: We need to remove all of the intersections in some way.
                         * Therefore truncate this right now.
                         */
                        // doAdd = false;
                        // continueCheck = false;
                        // break;
                        /*
                         * We have an intersection of this stroke with another
                         * stroke.
                         *
                         * Given the way that we emit strokes we know, that curr.a
                         * is one StreetPoint of an existing stroke.
                         *
                         * In every case we will need to split the existing stroke into
                         * two, adding a new streetpoint at the intersection. If curr has a 
                         * higher weight than the existing one, curr also is added (in two parts).
                         * Otherwise, curr simply stops at the intersection point.
                         */

                        var intersectionStreetPoint = new StreetPoint();
                        var intersectingStroke = intersection.StrokeExists;
                        intersectionStreetPoint.SetPos( intersection.Pos );
                        intersectionStreetPoint.PushCreator("intersection");
                        if( _traceGenerator ) {
                            trace( $"Generator: Trying intersection point {intersectionStreetPoint}" );
                        }

                        /*
                         * Check, if the intersection is too close to either endpoint. It it is, just route it through
                         * the existing end point.
                         */
                        if( Vector2.Distance( intersectionStreetPoint.Pos, intersectingStroke.A.Pos) < minPointToCandIntersectionDistance ) {
                            /*
                             * The current one intersects very close to the beginning of this stroke.
                             */
                            // TXWTOOD: Add the part until this endpoint, continueing with the tail.
                            doAdd = false;
                            continueCheck = false;
                            break;
                        }
                        if( Vector2.Distance( intersectionStreetPoint.Pos, intersectingStroke.B.Pos) < minPointToCandIntersectionDistance ) {
                            /*
                             * The current one intersects very close to the ending of this stroke.
                             */
                            // TXWTOOD: Add the part until this endpoint, continueing with the tail.
                            doAdd = false;
                            continueCheck = false;
                            break;
                        }
#if false
                        /*
                         * Well, still this intersection point might be closer to another
                         * known street point. If it does, we use the established point instead of the
                         * intersection point. Which, you guess, might yield to a different intersection...
                         */
                        var tooClose: StreetPoint = _strokeStore.findClosestBelowButNot( 
                            intersectionStreetPoint, minPointToCandIntersectionDistance, null );

                        if( tooClose != null ) {
                            if( _traceGenerator ) {
                                trace( 'Generator: Found $intersectionStreetPoint to be too close to $tooClose. Ignoring intersecting stroke $curr');
                            }
                            /*
                             * The intersection is pretty close to another street point. Given,
                             * that the network was in a sane state before, it should remain sane enough if
                             * I replace this intersection with the existing street point for both
                             * the existing and the new stroke.
                             *
                             * TXWTODO: Add a new condition to check for street points near strokes
                             * while 1st path insertion.
                             */

                            /*
                             * Until we add further checks, do not add this stroke, but remove it.
                             */
                            doAdd = false;
                            continueCheck = false;
                            break;
                        }
#endif
#if false
                        /*
                         * Now, before dissecting the target stroke look, whether the intersection
                         * point would be too close to another point. If it would be, discard the current 
                         * stroke entirely.
                         * 
                         * TXWTODO: However, we do not test whether it is too close to another line.
                         */
                         {
                            var si = _strokeStore.getClosestPoint( curr );
                            if( si != null && si.scaleExists < minPointToCandStrokeDistance ) {
                                if( _traceGenerator ) trace( 'Generator: Discarding stroke, too close: ${si.scaleExists}' );
                                /*
                                * If there is any point closer the d meters to this stroke,
                                * then [look, which point is closer to the stroke and connect
                                * it instead] drop it.
                                */
                                doAdd = false;
                                continueCheck = false;
                                break;
                            }
                        }
#endif
                        Stroke oldStrokeExists = intersection.StrokeExists;

                        /*
                         * Important: We must not modify the topology of the graph directly.
                         * Therefore we first remove the edge from the graph. Modifying the nodes
                         * and then readding it.
                         */
                        _strokeStore.Remove( intersection.StrokeExists );
                        var newStrokeExists = intersection.StrokeExists.CreateUnattachedCopy();
                        newStrokeExists.PushCreator( "newStrokeExists" );

                        oldStrokeExists.B = intersectionStreetPoint;
                        oldStrokeExists.PushCreator( "oldStrokeExists" );

                        newStrokeExists.A = intersectionStreetPoint;

                        // newStrokeExists.weight = 0.1;
                        // oldStrokeExists.weight = 6.0;

                        _strokeStore.AddStroke(newStrokeExists);
                        _strokeStore.AddStroke(oldStrokeExists);
                        _generationCounter++;

                        /*
                         * Add the candidate stroke, truncated to this intersection
                         */
                        var oldCurrB = curr.B;
                        if( curr.Store != null ) {
                            throw new InvalidOperationException( $"Generator: (intersecting) curr already is in store ({curr})");
                        }
                        curr.B = intersectionStreetPoint;

                        /*
                         * And add the continuation, after the intersection.
                         */
                        var currTail = curr.CreateUnattachedCopy();
                        currTail.PushCreator( "newTail" );
                        currTail.A = intersectionStreetPoint;
                        currTail.B = oldCurrB;

                        // As this is a stack, first the continuation, then the head.
                        _listStrokesToDo.Add(currTail);
                        _listStrokesToDo.Add(curr);
                        _generationCounter++;

                        // Leave this loop.
                        doAdd = false;
                        continueCheck = false;
                    }

                    /*
                     * If we reached this point, we are clean. No streetpoint closer to another
                     * streetpoint, plus this stroke is not intersecting another one.
                     */
                    break;

                }

                if( !doAdd ) {
                    // trace( 'Generator: Avoiding to add stroke.' );
                    continue;
                }

                /*
                 * Add the stroke to the map, creating a continuation and
                 * pronably side streets.
                 */
                _strokeStore.AddStroke(curr);
                ++_generationCounter;

                /*
                 * Compute some options.
                 */
                bool doForward = _rnd.get8() < probabilityNextStrokeForward;
                bool doRight = _rnd.get8() < (int)(probabilityNextStrokeRightWeightFactor / ((int)(curr.Weight)+1) );
                bool doLeft = _rnd.get8() < (int)(probabilityNextStrokeLeftWeightFactor / ((int)(curr.Weight)+1) );
                bool doRandomDirection = _rnd.get8() > (/*Std.int(curr.weight) * */ probabilityNextStrokeRandomNegWeightFactor);
                bool doIncreaseWeight = _rnd.get8() < probabilityNextStrokeIncreaseWeight;
                bool doDecreaseWeight = _rnd.get8() < probabilityNextStrokeDecreaseWeight;

                float newWeight = curr.Weight;
                float newLength = (int)((newStrokeMinimum + newStrokeSquaredWeight * (newWeight*newWeight))*10f)/10f;
                if( newLength < newLengthMin ) {
                    newLength = newLengthMin;
                }

                {
                    if( doIncreaseWeight ) {
                        newWeight *= weightIncreaseFactor;
                    }
                    if( doDecreaseWeight ) {
                        newWeight *= weightDecreaseFactor;
                    }
                    if( newWeight < weightMin ) {
                        newWeight = weightMin;
                    } else {
                        if( newWeight > weightMax) {
                            newWeight = weightMax;
                        }
                    }
                    newWeight = (int)((newWeight)*1000f)/1000f;
                }

                float newAngle = curr.Angle;
                if (_rnd.get8() < probabilityAngleSlightTurn ) {
                    newAngle = newAngle +_rnd.getFloat()*2f*AngleSlightTurnMax-AngleSlightTurnMax;
                }

                if(!doForward && !doRight && !doLeft && !doRandomDirection) {
                    doRandomDirection = true;
                }

                if( doForward ) {
                    StreetPoint newB = new StreetPoint();
                    var forward = Stroke.CreateByAngleFrom(
                        curr.B,
                        newB,
                        newAngle,
                        newLength,
                        curr.IsPrimary,
                        newWeight
                    );
                    forward.PushCreator("forward");
                    newB.PushCreator("forward");
                    _addStrokeToDo(forward);
                }
                if( doRight ) {
                    var newB = new StreetPoint();
                    var right = Stroke.CreateByAngleFrom(
                        curr.B,
                        newB,
                        newAngle-(float)Math.PI/2f,
                        newLength,
                        !curr.IsPrimary,
                        newWeight
                    );
                    right.PushCreator("right");
                    newB.PushCreator("right");
                    _addStrokeToDo(right);
                }
                if( doLeft ) {
                    var newB = new StreetPoint();
                    var left = Stroke.CreateByAngleFrom(
                        curr.B,
                        newB,
                        newAngle+(float)Math.PI/2f,
                        newLength,
                        !curr.IsPrimary,
                        newWeight
                    );
                    left.PushCreator("left");
                    newB.PushCreator("left");
                    _addStrokeToDo(left);
                }
                if( doRandomDirection ) {
                    var newB = new StreetPoint();
                    var randStroke = Stroke.CreateByAngleFrom(
                        curr.B,
                        newB,
                        _rnd.getFloat()*(float)Math.PI*2f,
                        newLength,
                        curr.IsPrimary,
                        newWeight
                    );
                    randStroke.PushCreator("randStroke");
                    newB.PushCreator("randStroke");
                    _addStrokeToDo(randStroke);
                }
            }
        }


        public void SetBounds( 
            float blx0, float bly0,
            float trx0, float try0
        ) {
            _tr = new Vector2( trx0, try0 );
            _bl = new Vector2( blx0, bly0 );
        }


        public List<Stroke> GetStrokes() 
        {
            return _strokeStore.GetStrokes();
        }


        public void AddStartingStroke(in Stroke stroke0){
            _listStrokesToDo.Add(stroke0);
        }

        public void Reset(
            in string seed0,
            in StrokeStore strokeStore
        ) {
            _rnd = new engine.RandomSource(seed0);
            _listStrokesToDo = new List<Stroke>();
            _strokeStore = strokeStore;
            _generationCounter = 0;
        }


        public Generator() 
        {
        }
    }
}
