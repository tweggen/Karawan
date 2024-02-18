using System;
using System.Numerics;
using System.Collections.Generic;
using ClipperLib;
using engine.world;

namespace engine.streets
{
    public class QuarterGenerator
    {
        private static void trace(in string message)
        {
            Console.WriteLine(message);
        }

        private engine.world.ClusterDesc _clusterDesc;
        private StrokeStore _strokeStore;
        private QuarterStore _quarterStore;
        private bool _traceGenerate = false;
        private float _storyHeight = 3f;

        private builtin.tools.RandomSource _rnd;


        private void _addShops(Building building)
        {
            float minShopWidth = 5f;
            var p = building.GetPoints();
            int l = p.Count;
            for (int i = 0; i < l; ++i)
            {
                var pA = p[i];
                var pB = p[(i + 1) % l];
                var vAB = pB - pA;
                var len = (pB - pA).Length();

                int nFronts = (int)Single.Floor(len / minShopWidth);
                if (0 == nFronts)
                {
                    continue;
                }

                var vuAB = vAB / len;
                var vNAB = new Vector3(vAB.Z, 0, -vAB.X);
                var vuNAB = Vector3.Normalize(vNAB);

                float lenBefore = (len - (float)nFronts * minShopWidth) / 2;
                
                List<Vector3> frontPoints = new();
                frontPoints.Add(pA + vuAB * lenBefore + vuNAB*0.1f);
                frontPoints.Add(pA + vuAB * (lenBefore+len) + vuNAB*0.1f);

                ShopFront shopFront = new();
                shopFront.AddPoints(frontPoints);
            }
        }
        
        
        private void _createBuildings(in Quarter quarter, in Estate estate)
        {
            /*
             * The current implementation takes one estate, shrinks the real estate 
             * (like a sidewalk). If any suitable area remains, this is gonna be
             * the house.
             *
             * We also derive attributes of the house from the size of the estate.
             */

            List<Vector3> p = new();

            var mn = 0;

            float minHouseSide = 10000f;

            List<IntPoint> polyPoints = new();
            List<List<IntPoint>> polyList = new();
            polyList.Add(polyPoints);
            var clipperOffset = new ClipperOffset();
            foreach(var point in estate.GetPoints()) {
                polyPoints.Add( new IntPoint((int)(point.X*10f), (int)(point.Z*10f) ) );
            }

            if( polyPoints.Count>0 ) { 
                clipperOffset.AddPaths(polyList, JoinType.jtMiter, EndType.etClosedPolygon);
                List<List<IntPoint>> solution2 = new();

                /*
                 * Generate a random sidewalk width.
                 * TXWTODO: High buildings might have a larger entrace area, don't they?
                 * Do it random for now.
                 */
                float sidewalkWidth = 30f;
                {
                    //sidewalkWidth = _rnd.getFloat()*75. + 25.;
                }
                clipperOffset.Execute(ref solution2, -sidewalkWidth);

                var strPoints = "";

                foreach(var polygon in solution2)
                {
                    foreach(var point in polygon)
                    {
                        float x = point.X / 10f;
                        float y = point.Y / 10f;
                        // trace( 'x: $x, y: $y' );
                        p.Add(new Vector3(x, 0f, y));
                        strPoints += $"( $x, $y ), ";
                        ++mn;
                    }
                    if (mn > 0)
                    {
                        /*
                         * TXWTODO: What if we would have multiple polygons?
                         */
                        for(int i=0; i<mn; ++i)
                        {
                            Vector3 v0 = p[i];
                            Vector3 v1 = p[(i + 1) % mn];
                            v1 -= v0;
                            float sideLength = v1.Length();
                            if (sideLength < minHouseSide) minHouseSide = sideLength;
                        }
                    }

                    /*
                     * Now, compute the length of each of the sides and store them.
                     * We derive design decitions from the lengths.
                     */
                }
                if (strPoints.Length > 0)
                {
                    quarter.AddDebugTag("quarterPoints", strPoints);
                }
                if (0 == mn)
                {
                    quarter.AddDebugTag("estateTooSmall", "true");
                }
            } else
            {
                // trace( 'no house[0]' );
                quarter.AddDebugTag("estateWithoutPoints", "true");
            }

            if (mn == 0)
            {
                return;
            }

            /*
             * But do not build everywhere. Trivial: Remove 30% of the buildings.
             */
            if (_rnd.GetFloat() > 0.7f)
            {
                quarter.AddDebugTag("leftWithoutBuilding", "true");
                return;
            }
            p.Reverse();

            /*
             * If there are any points in the solution, then add the single polygon as the estate.
             * This polygon possibly is concave, but it is describing the entire building.
             */

            /*
             * We have the concave polygon, create a collection of convex polygons
             */

            // var convexPolys = geom.Tools.concaveToConvex( p );

            var building = new streets.Building() { ClusterDesc = _clusterDesc };
            building.AddPoints(p);
            float maxHeight;
            float downtownness =
                _clusterDesc.GetAttributeIntensity(
                    p[0] + _clusterDesc.Pos,
                    ClusterDesc.LocationAttributes.Downtown);
            if (minHouseSide <= 2.0f || downtownness < 0.3f)
            {
                maxHeight = 1f * _storyHeight;
            }
            else if (minHouseSide <= 5.0f || downtownness < 0.5)
            {
                maxHeight = 2f * _storyHeight;
            }
            else if (minHouseSide <= 10.0f || downtownness < 0.7)
            {
                maxHeight = 8f * _storyHeight;
            }
            else
            {
                maxHeight = 160f * _storyHeight;
            }

            var height = _storyHeight * (int)((15f + _rnd.GetFloat() * 250f)/ _storyHeight);
            if (height > maxHeight) height = maxHeight;
            building.SetHeight(height);

            quarter.AddDebugTag("haveBuilding", "true");
            estate.AddBuilding(building);
            
            /*
             * Generate buildings:
             * - (if non-industrial area or too corpo) shopfront 3m
             * - if larger than 2 storeys
             *
             * - split if large enough
             * - if heigher than 30m can be forced
             *   - round
             *   - rectangle
             *   - rect without diagonal
             * - [sphere on top]
             * - [antenna on top]
             */
            
            /*
             * Now generate shops if we are supposed to have storefronts.
             * We store shops as a path in front of a building.
             */
            if (_rnd.GetFloat() <=
                _clusterDesc.GetAttributeIntensity(
                    p[0] + _clusterDesc.Pos, 
                    ClusterDesc.LocationAttributes.Shopping))
            {
                _addShops(building);
            }
        }


        /**
         * Generate quarters by following the strokes.
         *
         * - Mark all strokes untraversed in either direction.
         * - Iterate through all street points.
         * - for every stroke (starting and ending) at this endpoint, follow it
         *   to create a quarter, unless it already has been traversed. Mark it
         *   traversed afterwards.
         */
        public void Generate()
        {

            _rnd.Clear();
            _strokeStore.ClearTraversed();
            var points = _strokeStore.GetStreetPoints();
            foreach (var spStart in points)
            {

                if (_traceGenerate) trace($"QuarterGenerator(): Tracing Point {spStart.Pos}");
                var angleStrokes = spStart.GetAngleArray();
                foreach (var stroke in angleStrokes)
                {
                    StreetPoint? spDest = null;
                    // Which direction?
                    var isAlreadyTraversed = false;
                    if (stroke.A == spStart)
                    {
                        if (stroke.TraversedAB)
                        {
                            isAlreadyTraversed = true;
                        }
                        spDest = stroke.B;
                    }
                    else if (stroke.B == spStart)
                    {
                        if (stroke.TraversedBA)
                        {
                            isAlreadyTraversed = true;
                        }
                        spDest = stroke.A;
                    }
                    else
                    {
                        throw new InvalidOperationException("QuarterGenerator: Invalid stroke encountered.");
                    }
                    if (spStart == spDest)
                    {
                        throw new InvalidOperationException("QuaterGenerator: Invalid stroke: Start and end is the same.");
                    }

                    if (isAlreadyTraversed)
                    {
                        continue;
                    }
                    if (_traceGenerate) trace("QuarterGenerator():");
                    if (_traceGenerate) trace($"QuarterGenerator(): Starting with stroke from {spStart.Pos} to ${spDest.Pos}");

                    /*
                     * We know that we need to start from spStart using "stroke". Follow the loop.
                     */
                    var spCurr = spStart;
                    var strokeCurrent = stroke;
                    var solestroke = false;

                    var quarter = new Quarter() { ClusterDesc = _clusterDesc };
                    var hasNullSection = false;
                    var hasDeadEnd = false;
                    var nPoints = 0;
                    var sumOfAngles = 0.0;
                    Stroke? previousStroke;

                    while (true)
                    {

                        if (null == strokeCurrent)
                        {
                            throw new InvalidOperationException("QuarterGenerator(): strokeCurrent is null");
                        }
                        if (null == spCurr)
                        {
                            throw new InvalidOperationException("QuarterGenerator(): spCurr is null");
                        }

                        /*
                         * For readability: First figure out the direction.
                         */
                        var isAB = false;
                        StreetPoint? spNext = null;
                        if (strokeCurrent.A == spCurr)
                        {
                            isAB = true;
                            spNext = strokeCurrent.B;
                        }
                        else if (strokeCurrent.B == spCurr)
                        {
                            isAB = false;
                            spNext = strokeCurrent.A;
                        }
                        else
                        {
                            throw new InvalidOperationException("QuarterGenerator: Invalid stroke following quarter.");
                        }

                        /*
                         * That's a bit difficult: stroke.angle is valid from a to b. So if we 
                         * try a stroke while being point a, everything is fine.
                         * However, if we are b, we need to invert the angle for the purpose of
                         * following.
                         */
                        var followAngle = geom.Angles.Snorm(strokeCurrent.Angle + ((!isAB) ? (float)Math.PI : 0f));
                        sumOfAngles += followAngle;

                        /*
                         * Hint what we are doing.
                         */
                        if (_traceGenerate) trace($"QuarterGenerator(): Following angle {followAngle} ({geom.Angles.Snorm(followAngle + (float)Math.PI)}) from {isAB} ${spNext.Pos}");

                        /*
                         * In every iteration: Follow strokeCurrent from spCurr.
                         * If spCurr is spStart, terminate (case 1).
                         * Look for the next stroke clockwise to strokeCurrent.
                         * If it is strokeCurrent, terminate (case 2).
                         * add the new spCurr to the Quarter.
                         * repeat.
                         */
                        if (isAB)
                        {
                            if (strokeCurrent.TraversedAB)
                            {
                                throw new InvalidOperationException($"QuarterGenerator(): Attempt to traverseAB twice");
                            }
                            strokeCurrent.TraversedAB = true;
                        }
                        else /* B to A */
                        {
                            if (strokeCurrent.TraversedBA)
                            {
                                throw new InvalidOperationException($"QuarterGenerator(): Attempt to traverseBA twice");
                            }
                            strokeCurrent.TraversedBA = true;
                        }

                        var quarterDelim = new QuarterDelim();
                        quarterDelim.StreetPoint = spCurr;
                        quarterDelim.Stroke = strokeCurrent;

                        /*
                         * Before we add the delimiter, we need the next stroke, because
                         * we need the intersection of this and the next stroke.
                         */
                        var strokeNext = spNext.GetNextAngle(strokeCurrent, followAngle, true);
                        if (null == strokeNext || strokeNext == strokeCurrent)
                        {
                            if (_traceGenerate) trace($"QuarterGenerator(): Followed same stroke back because there is no other angle.");
                            /*
                             * So follow myself back in the other direction.
                             * That means, spCurr (regularily) will become spNext.
                             * The next stroke, however, will be the same one as the current one.
                             */
                            strokeNext = strokeCurrent;
                            hasDeadEnd = true;
                            quarter.AddDebugTag("hasDeadEnd", "true");
                        }

                        {
                            var section = spNext.GetSectionPointByStroke(strokeNext, strokeCurrent);
                            if (null == section)
                            {
                                hasNullSection = true;
                                quarter.AddDebugTag("hasNullSection", "true");
                            }
                            else
                            {
                                quarterDelim.StartPoint = (Vector2) section;
                            }
                        }
                        quarter.AddQuarterDelim(quarterDelim);
                        ++nPoints;

                        if (spNext == spStart)
                        {
                            if (_traceGenerate) trace($"QuarterGenerator(): Reached start again.");
                            break;
                        }

                        /*
                         * Iterate to the next one.
                         */
                        strokeCurrent = strokeNext;
                        spCurr = spNext;
                    }

                    if (solestroke)
                    {
                        if (_traceGenerate) trace($"QuarterGenerator(): Leaving loop because this is a single stroke.");
                    }
                    else
                    {
                        // TXWTODO: We do not explicitely detect the "outside". 
                        // TXWTODO: We can't properly handle dead ends.
                        /*
                         * However, most likely, the "outside" does have dead ends, so do not add them as quarters.
                         */
                        if (hasNullSection)
                        {
                            if (_traceGenerate) trace($"QuarterGenerator.generate(): Has null section.");
                        }
                        else
                        {
                            /*
                             * Now create the root estate.
                             */
                            var estate = new Estate() { ClusterDesc = _clusterDesc };
                            foreach (var delim in quarter.GetDelims())
                            {
                                estate.AddPoint(new Vector3(delim.StartPoint.X, 0, delim.StartPoint.Y));
                            }

                            /*
                             * Create the building(s) on that estate
                             */
                            if (true || _rnd.GetFloat() > 0.05)
                            {
                                quarter.AddDebugTag("shallHaveBuildings", "true");
                                _createBuildings(quarter, estate);
                            }

                            quarter.AddEstate(estate);
                            if (_traceGenerate) trace($"QuarterGenerator.generate(): Adding quarter.");
                            quarter.Polish();
                            var cp = quarter.GetCenterPoint();
                            quarter.AddDebugTag("centerPoint", $"x: {cp.X}, y: {cp.Y}");
                            _quarterStore.Add(quarter);
                        }

                    }
                }
            }
        }


        public void Reset(
            string seed0,
            engine.world.ClusterDesc clusterDesc,
            QuarterStore quarterStore,
            StrokeStore strokeStore) 
        {
            _rnd = new builtin.tools.RandomSource(seed0);
            _clusterDesc = clusterDesc;
            _quarterStore = quarterStore;
            _strokeStore = strokeStore;
        }

        public QuarterGenerator()
        {
        }
    }
}
