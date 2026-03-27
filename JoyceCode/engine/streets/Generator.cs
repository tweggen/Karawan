using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.world;
using static engine.Logger;

namespace engine.streets
{
    public class Generator
    {
        private void trace(string message)
        {
            Trace($"{_annotation}: {message}");
        }
        
        private List<Stroke> _listStrokesToDo;
        private StrokeStore _strokeStore;
        private bool _traceGenerator = false;
        private string _annotation = "";
        private ClusterDesc _clusterDesc;

        private int _generationCounter;
        private builtin.tools.RandomSource _rnd;

        private Vector2 _bl;
        private Vector2 _tr;

        // Safeguard 1: Track created vs added StreetPoints
        private HashSet<int> _createdStreetPointIds = new();
        private HashSet<int> _orphanedStreetPointIds = new();
        private Dictionary<int, string> _orphanedPointOrigins = new();  // ID -> creator info

        // Safeguard 2: Track strokes with missing endpoints
        private List<(int strokeId, int missingEndpointId, string missingEndpointCreator)> _strokesWithMissingEndpoints = new();

        // Option B: Track new StreetPoints created for current stroke being processed
        // If stroke fails validation, we'll mark these for cleanup
        private List<int> _currentStrokeNewPoints = new();
        private int _cleanedUpOrphanedPoints = 0;

        public float minPointToCandPointDistance { get; set; } = 30f;
        public float minPointToCandStrokeDistance { get; set; } = 30f;
        public float minPointToCandIntersectionDistance { get; set; } = 30f;

        public float weightMin { get; set; } = 0.2f;
        public float weightMax { get; set; } = 1.3f;
        public float weightRange 
        {
            get => weightMax-weightMin;
        }

        public float normWeight(float weight)
        {
            return (weight - weightMin) / weightRange;
        }


        /*
         * All proabilities are given in the 0..256 range to avoid differrences
         * between platforms due to rounding errors on floats.
         */
        public int probabilityNextStrokeForward { get; set; } = 252;
        private int probabilityNextStrokeBranch(float weight) => (int)(150f / (1+4f * (1f-normWeight(weight))));
        private int probabilityNextStrokeRandom(float weight) => (int)(80 - normWeight(weight)*60f);
        public int probabilityNextStrokeStraightDecreaseWeight { get; set; } = 5;
        public int probabilityNextStrokeStraightIncreaseWeight { get; set; } = 10;
        public int probabilityNextStrokeBranchDecreaseWeight { get; set; } = 190;
        public int probabilityNextStrokeBranchIncreaseWeight { get; set; } = 3;

        public float  newStrokeMinimum { get; set; } = 60f;
        public float newStrokeSquaredWeight { get; set; } = 40f;
        public float newLengthMin { get; set;  } = 75f;

        public float weightIncreaseFactor { get; set; } = 1.1f;
        public float weightDecreaseFactor { get; set; } = 0.9f;
        public float probabilityAngleSlightTurn { get; set; } = 30f;
        public int AngleSlightTurnMax { get; set; } = 6;

        public float AngleMinStrokes { get; set; } = 40.0f;


        private bool _inBounds(in Stroke cand)
        {
            return (true
                && cand.A.Pos.X > _bl.X
                && cand.A.Pos.Y > _bl.Y
                && cand.A.Pos.X < _tr.X
                && cand.A.Pos.Y < _tr.Y
                && cand.B.Pos.X > _bl.X
                && cand.B.Pos.Y > _bl.Y
                && cand.B.Pos.X < _tr.X
                && cand.B.Pos.Y < _tr.Y);
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
            if (_inBounds(stroke))
            {
                _listStrokesToDo.Add(stroke);
            }
        }


        /// <summary>
        /// Validate that all stroke endpoints exist in the stroke store.
        /// Returns true if valid, false if endpoints are missing.
        /// </summary>
        private bool _validateStrokeEndpoints(in Stroke stroke)
        {
            // Check if both endpoints are in the store
            bool aInStore = stroke.A.InStore;
            bool bInStore = stroke.B.InStore;

            if (!aInStore)
            {
                // Track the missing endpoint
                int missingId = stroke.A.Id;
                if (!_orphanedPointOrigins.ContainsKey(missingId))
                {
                    _orphanedPointOrigins[missingId] = stroke.A.Creator;
                    _orphanedStreetPointIds.Add(missingId);
                }
                _strokesWithMissingEndpoints.Add((stroke.Sid, missingId, stroke.A.Creator));

                if (_traceGenerator)
                    trace($"Stroke {stroke.Sid} has missing endpoint A (ID {missingId}, creator: {stroke.A.Creator})");

                return false;
            }

            if (!bInStore)
            {
                // Track the missing endpoint
                int missingId = stroke.B.Id;
                if (!_orphanedPointOrigins.ContainsKey(missingId))
                {
                    _orphanedPointOrigins[missingId] = stroke.B.Creator;
                    _orphanedStreetPointIds.Add(missingId);
                }
                _strokesWithMissingEndpoints.Add((stroke.Sid, missingId, stroke.B.Creator));

                if (_traceGenerator)
                    trace($"Stroke {stroke.Sid} has missing endpoint B (ID {missingId}, creator: {stroke.B.Creator})");

                return false;
            }

            return true;
        }

        /// <summary>
        /// Option B: Mark current stroke's new points as candidates for cleanup if validation fails.
        /// Call this BEFORE processing a stroke from the queue.
        /// </summary>
        /// <summary>
        /// Option A: Quick validation of stroke endpoint before creating the StreetPoint.
        /// Checks if endpoint position passes basic validity constraints (bounds, edge distance).
        /// Returns true if endpoint appears valid, false if it would likely fail validation.
        /// </summary>
        private bool _willStrokeEndpointBeValid(in Stroke candidateStroke)
        {
            // Check if B endpoint (the new point) is in bounds
            var b = candidateStroke.B.Pos;
            if (b.X <= _bl.X || b.X >= _tr.X || b.Y <= _bl.Y || b.Y >= _tr.Y)
            {
                return false;  // Out of bounds
            }

            // Check if B is too close to cluster edges (would fail edge distance checks)
            const float edgeBuffer = 15f;
            float distToBoundary = MathF.Min(
                MathF.Min(b.X - _bl.X, _tr.X - b.X),
                MathF.Min(b.Y - _bl.Y, _tr.Y - b.Y)
            );
            if (distToBoundary < edgeBuffer)
            {
                return false;  // Too close to boundary
            }

            // Check if B is very close to A (minimum length constraint)
            float lengthToB = Vector2.Distance(candidateStroke.A.Pos, b);
            if (lengthToB < newStrokeMinimum * 0.8f)  // Conservative check
            {
                return false;  // Stroke too short
            }

            return true;  // Endpoint appears valid
        }

        private void _markNewPointsForStroke(in Stroke stroke)
        {
            _currentStrokeNewPoints.Clear();

            // Mark endpoints that aren't in store yet (newly created)
            if (!stroke.A.InStore)
                _currentStrokeNewPoints.Add(stroke.A.Id);
            if (!stroke.B.InStore)
                _currentStrokeNewPoints.Add(stroke.B.Id);
        }

        /// <summary>
        /// Option B: Clean up StreetPoints that were created for a stroke that failed validation.
        /// Removes them from the created tracking set so they won't appear as orphaned.
        /// </summary>
        private void _cleanupFailedStrokePoints()
        {
            foreach (var pointId in _currentStrokeNewPoints)
            {
                // Remove from created set so it's not counted as orphaned later
                _createdStreetPointIds.Remove(pointId);
                _cleanedUpOrphanedPoints++;
            }
            _currentStrokeNewPoints.Clear();
        }

        /// <summary>
        /// Report on any orphaned street points that were created but never added.
        /// </summary>
        private void _reportOrphanedPoints()
        {
            trace($"\n{'='} GENERATION SAFEGUARD REPORT ======================");

            // Check which created points actually made it into the final store
            var actualStorePoints = new HashSet<int>(_strokeStore.GetStreetPoints().Select(p => p.Id));
            var truelyOrphanedPoints = _createdStreetPointIds.Where(id => !actualStorePoints.Contains(id)).ToList();

            trace($"Total StreetPoints created during generation: {_createdStreetPointIds.Count}");
            trace($"Total StreetPoints cleaned up (Option B): {_cleanedUpOrphanedPoints}");
            trace($"Total StreetPoints in final store: {actualStorePoints.Count}");
            trace($"⚠️  ORPHANED POINTS (created but not in final store): {truelyOrphanedPoints.Count}");
            trace($"Total strokes with endpoint issues: {_strokesWithMissingEndpoints.Count}");

            if (truelyOrphanedPoints.Count == 0 && _strokesWithMissingEndpoints.Count == 0)
            {
                trace($"✅ No orphaned StreetPoints found - all created points successfully added or cleaned up");
                return;
            }

            if (truelyOrphanedPoints.Count > 0)
            {
                trace($"⚠️  DETAILS: {truelyOrphanedPoints.Count} orphaned points (sample):");
                foreach (var orphanId in truelyOrphanedPoints.OrderBy(id => id).Take(20))
                {
                    trace($"  StreetPoint {orphanId} was created but never added to final store");
                }
                if (truelyOrphanedPoints.Count > 20)
                    trace($"  ... and {truelyOrphanedPoints.Count - 20} more orphaned points");
            }

            if (_strokesWithMissingEndpoints.Count > 0)
            {
                trace($"⚠️  STROKES WITH MISSING ENDPOINTS: {_strokesWithMissingEndpoints.Count} strokes reference non-existent endpoints:");
                foreach (var (strokeId, missingId, creator) in _strokesWithMissingEndpoints.Take(20))
                {
                    trace($"  Stroke {strokeId} → missing endpoint {missingId} (creator: {creator})");
                }
                if (_strokesWithMissingEndpoints.Count > 20)
                    trace($"  ... and {_strokesWithMissingEndpoints.Count - 20} more");
            }
        }

        /**
         * Iterate until the queue of strokes is empty again.
         */
        public void Generate()
        {
            int maxGenerations = (int)(_clusterDesc.Size * _clusterDesc.Size / 1000f);
            
            while (true)
            {

                if (maxGenerations < _generationCounter)
                {
                    Trace("Returning: max generations reached.");
                    _reportOrphanedPoints();
                    return;
                }

                if (!_haveStrokesToDo())
                {
                    Trace("Returning: no more streets to do.");
                    _reportOrphanedPoints();
                    return;
                }

                Stroke curr = _popStrokeToDo();
                // trace( 'Generator: Starting new generation.' );

                // Option B: Mark new points created for this stroke, in case validation fails
                _markNewPointsForStroke(curr);

                /*
                 * Check, wether this segment is valid.
                 */

                /*
                 * In bounds of the desired area?
                 */
                if (!_inBounds(curr))
                {
                    if (_traceGenerator) Trace($"curr is out of bounds: {curr.ToString()}");
                    /*
                     * Out of range, so discard it.
                     */
                    _cleanupFailedStrokePoints();  // Option B: Clean up points for failed stroke
                    continue;
                }

                /*
                 * Is there a street point close enough to use it?
                 *
                 * (in this implementation we assume no other pair of street points
                 * is too close to each other).
                 */

                bool continueCheck = true;
                bool doAdd = true;

                while (continueCheck)
                {

                    /*
                     * This actually might happen due to intersections etc. 
                     */

                    /*
                     * Is the end of the stroke too close to the beginning of the stroke?
                     */
                    if (Vector2.Distance(curr.A.Pos, curr.B.Pos) < minPointToCandPointDistance)
                    {
                        if (_traceGenerator)
                        {
                            Trace("Discarding candidate: is too short. $curr");
                        }
                        /*
                         * Test: If both are in store, we have an invalid entry in the store.
                         */
                        if (curr.A.InStore && curr.B.InStore)
                        {
                            // TXWTODO: Is this really invalid? Couldn't that happen due to merging both sides of a stroke?
                            // throw new InvalidOperationException( "Generator: (test too short) Found too close points (curr.a and curr.b) both in store" );
                        }
                        doAdd = false;
                        continueCheck = false;
                        _cleanupFailedStrokePoints();  // Option B: Clean up points for failed stroke
                        continue;
                    }

                    /*
                     * Check: Is any of our endpoints too close to an existing endpoint?
                     */
                    if(false) {
                        // TXWTODO: I don't check point a any more. Is that ok?
                        /*
                         * I wonder why a should be too close to another point?
                         * Possibly due to moving intersections?
                         */
                        StreetPoint tooClose = _strokeStore.FindClosestBelowButNot(
                            curr.A, minPointToCandPointDistance, curr.B);

                        if (null != tooClose)
                        {
                            if (_traceGenerator)
                            {
                                Trace($"StreetPoint A ({curr.A}) too close to StreetPoint ({tooClose}).");
                            }

                            /*
                             * A probably already is in the store, as we are moving from a to be. 
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

                    /*
                     * if B is new, look if it is too close to an existing point.
                     */
                    if (!curr.B.InStore)
                    {
                        StreetPoint tooClose = _strokeStore.FindClosestBelowButNot(
                            curr.B, minPointToCandPointDistance, curr.A );

                        if( null != tooClose ) {
                            if( _traceGenerator ) {
                                Trace($"StreetPoint B (${curr.B}) too close to StreetPoint ({tooClose}).");
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
                     * Look if the stroke would be too close to an existing one origining from either endpoint.
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
                            if( _traceGenerator ) Trace( $"Discarding stroke {curr.ToStringSP(curr.A)}, angle too close {closestAngle}" );
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
                            if( _traceGenerator ) Trace( $"Discarding stroke {curr.ToStringSP(curr.B)}, angle too close {closestAngle}" );
                            doAdd = false;
                            continueCheck = false;
                            break;
                        }
                    }


                    /*
                     * Look, whether the stroke is too close to an existing point
                     */
                    {
                        var si = _strokeStore.GetClosestPoint(curr, minPointToCandStrokeDistance);
                        if( si != null && si.ScaleExists < minPointToCandStrokeDistance ) {
                            if( _traceGenerator ) Trace( $"Discarding stroke {curr.ToString()}, too close to point: {si.StreetPoint}" );

                            if (curr.B.InStore)
                            {
                                /*
                                 * If B already is in the store, we do not want to exchange it.
                                 */
                                doAdd = false;
                                continueCheck = false;
                                break;
                            }
                            
                            /*
                             * Be is not in the store, maybe we can replace it by the streetpoint we found?
                             */
                            // float distB = (si.StreetPoint.Pos - curr.B.Pos).Length();
                            curr.B = si.StreetPoint;
                            continue;
                        }
                    }

                    /*
                     * The following does not seem to have pleasing results.
                     */
#if true
                    /*
                     * Look, whether the new point to is too close to an existing stroke
                     */
                    {
                        var si = _strokeStore.GetClosestStroke( curr.B, minPointToCandStrokeDistance);
                        if( si != null && si.ScaleExists < minPointToCandStrokeDistance ) {
                            /*
                             * We might want to check here, if it is perpendicular to the stroke as opposed to parallel.
                             * If it is perpendicular, we might be able to keep it, it might be a meaningful route.
                             */
                            float angleVice = Single.Abs(geom.Angles.Snorm(curr.Angle - si.StrokeExists.Angle));
                            float angleVersa = Single.Abs(geom.Angles.Snorm(Single.Pi+angleVice));
                            if (true ||angleVice<(Single.Pi/4f) || angleVersa<(Single.Pi/4f)) {
                                if (_traceGenerator)
                                    Trace(
                                        $"Discarding stroke {curr.ToString()}, point b too close to stroke: {si.StrokeExists}");

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
                    }
#endif

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

                        var intersectionStreetPoint = new StreetPoint() { ClusterId = _clusterDesc.Id };
                        _createdStreetPointIds.Add(intersectionStreetPoint.Id);  // Track creation
                        var intersectingStroke = intersection.StrokeExists;
                        intersectionStreetPoint.SetPos( intersection.Pos );
                        intersectionStreetPoint.PushCreator("intersection");
                        if( _traceGenerator ) {
                            Trace( $"Trying intersection point {intersectionStreetPoint}" );
                        }

                        /*
                         * Check, if the intersection is too close to either endpoint. It it is, just route it through
                         * the existing end point.
                         */

                        bool doGenerateTail = true;
                        
                        if( Vector2.Distance( intersectionStreetPoint.Pos, intersectingStroke.A.Pos) < minPointToCandIntersectionDistance ) {
                            /*
                             * The current one intersects very close to the beginning of this stroke.
                             */
                            // TXWTOOD: Add the part until this endpoint, continuing with the tail.
                            //doAdd = false;
                            //continueCheck = false;
                            //break;
                            doGenerateTail = true;
                        }

                        if( Vector2.Distance( intersectionStreetPoint.Pos, intersectingStroke.B.Pos) < minPointToCandIntersectionDistance ) {
                            /*
                             * The current one intersects very close to the ending of this stroke.
                             */
                            // TXWTOOD: Add the part until this endpoint, continuing with the tail.
                            // TXWTODO: Why don't we want to add this? Just use the intersection as b and we are fine, just leave out the tail.
                            
                            //doAdd = false;
                            //continueCheck = false;
                            doGenerateTail = false;
                            // break;
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
                        /*
                         * the two endpoints of stroke still are in the stroke store.
                         */
                        /*
                         * Warning: [null file name]:0: WorkerQueue:RunPart: Warning: Error executing worker queue engine.Engine.MainThread action: System.ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')
   at System.Collections.Generic.List`1.get_Item(Int32 index)
   at engine.streets.StrokeStore.AddPoint(StreetPoint& sp) in C:\Users\timow\coding\github\Karawan\JoyceCode\engine\streets\StrokeStore.cs:line 368
   at engine.streets.StrokeStore.AddStroke(Stroke& stroke) in C:\Users\timow\coding\github\Karawan\JoyceCode\engine\streets\StrokeStore.cs:line 396
   at engine.streets.Generator.Generate() in C:\Users\timow\coding\github\Karawan\JoyceCode\engine\streets\Generator.cs:line 494
   at engine.world.ClusterDesc._triggerStreets() in C:\Users\timow\coding\github\Karawan\JoyceCode\engine\world\ClusterDesc.cs:line 310
   at engine.world.ClusterDesc.FindStartPosition() in C:\Users\timow\coding\github\Karawan\JoyceCode\engine\world\ClusterDesc.cs:line 347
   at joyce.ui.Main.<>c__DisplayClass7_0.<Render>b__1() in C:\Users\timow\coding\github\Karawan\JoyceCode\ui\Main.cs:line 181
   at engine.WorkerQueue.RunPart(Single dt) in C:\Users\timow\coding\github\Karawan\JoyceCode\engine\WorkerQueue.cs:line 78
Trace: [null file name]:0: WorkerQueue:RunPart: Trace: Left 1 actions in queue engine.Engine.MainThread

                         */
                        newStrokeExists.PushCreator( "newStrokeExists" );

                        /*
                         * Intersection street point is not in the stroke store.
                         */
                        oldStrokeExists.B = intersectionStreetPoint;
                        oldStrokeExists.PushCreator( "oldStrokeExists" );

                        newStrokeExists.A = intersectionStreetPoint;
                        
                        /*
                         * So at this point:
                         * - oldStrokeExists.A already is in the stroke store.
                         * - oldStrokeExists.B is not.
                         * - newStrokeExists.A is not in the stroke store, same as old.B
                         * - newStrokeExists.B already is in the stroke store.
                         */

                        // newStrokeExists.weight = 0.1;
                        // oldStrokeExists.weight = 6.0;

                        _strokeStore.AddStroke(newStrokeExists);
                        _validateStrokeEndpoints(newStrokeExists);  // Validate endpoints
                        _strokeStore.AddStroke(oldStrokeExists);
                        _validateStrokeEndpoints(oldStrokeExists);  // Validate endpoints
                        _generationCounter++;

                        /*
                         * Add the candidate stroke, truncated to this intersection
                         */
                        var oldCurrB = curr.B;
                        if( curr.Store != null ) {
                            throw new InvalidOperationException( $"Generator: (intersecting) curr already is in store ({curr})");
                        }
                        curr.B = intersectionStreetPoint;

                        if (doGenerateTail)
                        {
                            /*
                             * And add the continuation, after the intersection.
                             */
                            var currTail = curr.CreateUnattachedCopy();
                            currTail.PushCreator("newTail");
                            currTail.A = intersectionStreetPoint;
                            currTail.B = oldCurrB;

                            // As this is a stack, first the continuation, then the head.
                            _listStrokesToDo.Add(currTail);
                        }
                        

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
                    _cleanupFailedStrokePoints();  // Option B: Clean up points for any failed stroke
                    continue;
                }

                /*
                 * Add the stroke to the map, creating a continuation and
                 * pronably side streets.
                 */
                _strokeStore.AddStroke(curr);
                _validateStrokeEndpoints(curr);  // Validate endpoints
                ++_generationCounter;

                /*
                 * Compute some options.
                 */
                bool doForward = _rnd.Get8() < probabilityNextStrokeForward;
                bool doRight = _rnd.Get8() < (int)probabilityNextStrokeBranch(curr.Weight);
                bool doLeft = _rnd.Get8() < (int)probabilityNextStrokeBranch(curr.Weight);
                bool doRandomDirection = _rnd.Get8() < (int)probabilityNextStrokeRandom(curr.Weight);

                var computeWeight = (
                    float currentWeight, 
                    float probDescrease, 
                    float probIncrease, 
                    float facDecrease, 
                    float facIncrease
                ) => {
                    float resultWeight = currentWeight;
                    bool doDecreaseWeight = _rnd.Get8() < probDescrease;
                    bool doIncreaseWeight = _rnd.Get8() < probIncrease;

                    if( doDecreaseWeight ) {
                        resultWeight = resultWeight * facDecrease;
                    }
                    if( doIncreaseWeight ) {
                        resultWeight = resultWeight * facIncrease;
                    }

                    if( resultWeight < weightMin ) {
                        resultWeight = weightMin;
                    } else {
                        if( resultWeight > weightMax) {
                            resultWeight = weightMax;
                        }
                    }
                    resultWeight = (int)((resultWeight)*1000f)/1000f;
                    
                    return resultWeight;
                };

                float newAngle = curr.Angle;
                if (_rnd.Get8() < probabilityAngleSlightTurn ) {
                    newAngle = newAngle +_rnd.GetFloat()*2f*AngleSlightTurnMax-AngleSlightTurnMax;
                }

                if(!doForward && !doRight && !doLeft && !doRandomDirection) {
                    doRandomDirection = true;
                }

                if (doForward || doRandomDirection) {
                    var straightWeight = computeWeight(
                        curr.Weight,
                        probabilityNextStrokeStraightDecreaseWeight,
                        probabilityNextStrokeStraightIncreaseWeight,
                        weightDecreaseFactor,
                        weightIncreaseFactor
                    );
                
                    float newLength = (int)((newStrokeMinimum + newStrokeSquaredWeight * (straightWeight*straightWeight))*10f)/10f;
                    if( newLength < newLengthMin ) {
                        newLength = newLengthMin;
                    }

                    if (doForward)
                    {
                        StreetPoint newB = new StreetPoint() { ClusterId = _clusterDesc.Id };
                        var forward = Stroke.CreateByAngleFrom(
                            _clusterDesc,
                            curr.B,
                            newB,
                            newAngle,
                            newLength,
                            curr.IsPrimary,
                            straightWeight
                        );

                        // Option A: Pre-validate endpoint before creating the point
                        if (_willStrokeEndpointBeValid(forward))
                        {
                            _createdStreetPointIds.Add(newB.Id);  // Track creation only if likely valid
                            forward.PushCreator("forward");
                            newB.PushCreator("forward");
                            _addStrokeToDo(forward);
                        }
                        // else: skip adding this stroke entirely - avoids creating orphan point
                    }

                    if (doRandomDirection) {
                        var newB = new StreetPoint() { ClusterId = _clusterDesc.Id };
                        var randStroke = Stroke.CreateByAngleFrom(
                            _clusterDesc,
                            curr.B,
                            newB,
                            _rnd.GetFloat()*(float)Math.PI*2f,
                            newLength,
                            curr.IsPrimary,
                            straightWeight
                        );

                        // Option A: Pre-validate endpoint before creating the point
                        if (_willStrokeEndpointBeValid(randStroke))
                        {
                            _createdStreetPointIds.Add(newB.Id);  // Track creation only if likely valid
                            randStroke.PushCreator("randStroke");
                            newB.PushCreator("randStroke");
                            _addStrokeToDo(randStroke);
                        }
                        // else: skip adding this stroke entirely - avoids creating orphan point
                    }
                }

                if (doRight || doLeft)
                {
                    var branchWeight = computeWeight(
                        curr.Weight,
                        probabilityNextStrokeBranchDecreaseWeight,
                        probabilityNextStrokeBranchIncreaseWeight,
                        weightDecreaseFactor,
                        weightIncreaseFactor
                    );
                
                    float newLength = (int)((newStrokeMinimum + newStrokeSquaredWeight * (branchWeight*branchWeight))*10f)/10f;
                    if( newLength < newLengthMin ) {
                        newLength = newLengthMin;
                    }

                    if( doRight ) {
                        var newB = new StreetPoint() { ClusterId = _clusterDesc.Id };
                        var right = Stroke.CreateByAngleFrom(
                            _clusterDesc,
                            curr.B,
                            newB,
                            newAngle-(float)Math.PI/2f,
                            newLength,
                            !curr.IsPrimary,
                            branchWeight
                        );

                        // Option A: Pre-validate endpoint before creating the point
                        if (_willStrokeEndpointBeValid(right))
                        {
                            _createdStreetPointIds.Add(newB.Id);  // Track creation only if likely valid
                            right.PushCreator("right");
                            newB.PushCreator("right");
                            _addStrokeToDo(right);
                        }
                        // else: skip adding this stroke entirely - avoids creating orphan point
                    }
                    if( doLeft ) {
                        var newB = new StreetPoint() { ClusterId = _clusterDesc.Id };
                        var left = Stroke.CreateByAngleFrom(
                            _clusterDesc,
                            curr.B,
                            newB,
                            newAngle+(float)Math.PI/2f,
                            newLength,
                            !curr.IsPrimary,
                            branchWeight
                        );

                        // Option A: Pre-validate endpoint before creating the point
                        if (_willStrokeEndpointBeValid(left))
                        {
                            _createdStreetPointIds.Add(newB.Id);  // Track creation only if likely valid
                            left.PushCreator("left");
                            newB.PushCreator("left");
                            _addStrokeToDo(left);
                        }
                        // else: skip adding this stroke entirely - avoids creating orphan point
                    }
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


        public void SetAnnotation(string annotation)
        {
            _annotation = annotation;
        }
        

        public void AddStartingStroke(in Stroke stroke0){
            _listStrokesToDo.Add(stroke0);
        }

        
        public void Reset(
            in string seed0,
            in StrokeStore strokeStore,
            in ClusterDesc clusterDesc
        ) {
            _rnd = new builtin.tools.RandomSource(seed0);
            _listStrokesToDo = new List<Stroke>();
            _strokeStore = strokeStore;
            _clusterDesc = clusterDesc;
            _generationCounter = 0;

            // Reset tracking structures
            _createdStreetPointIds.Clear();
            _orphanedStreetPointIds.Clear();
            _orphanedPointOrigins.Clear();
            _strokesWithMissingEndpoints.Clear();
        }


        public Generator() 
        {
        }
    }
}
