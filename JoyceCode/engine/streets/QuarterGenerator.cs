using System;
using System.Numerics;
using System.Collections.Generic;
using Clipper2Lib;

namespace engine.streets
{
    public class QuarterGenerator
    {
#if false
        private StrokeStore _strokeStore;
        private QuarterStore _quarterStore;
        private bool _traceGenerate = false;
        private float _storyHeight = 3f;

        private engine.RandomSource _rnd;

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

            var house = new Array<Array<hxClipper.Clipper.IntPoint>>();
            house[0] = new Array<hxClipper.Clipper.IntPoint>();
            var clipperOffset = new hxClipper.Clipper.ClipperOffset();
            for(point in estate.getPoints() ) {
                house[0].push( new hxClipper.Clipper.IntPoint(Std.int (point.x*10.0), Std.int (point.z*10.0) ) );
            }
            if(house[0].length>0 ) {
                clipperOffset.addPaths(house, hxClipper.Clipper.JoinType.JT_MITER, hxClipper.Clipper.EndType.ET_CLOSED_POLYGON);
                var solution2 = new Array<Array<hxClipper.Clipper.IntPoint>>();

        /*
         * Generate a random sidewalk width.
         * TXWTODO: High buildings might have a larger entrace area, don't they?
         * Do it random for now.
         */
        var sidewalkWidth = 30.;
            {
                //sidewalkWidth = _rnd.getFloat()*75. + 25.;
            }
clipperOffset.executePaths(solution2, -sidewalkWidth);

var strPoints = "";

for (polygon in solution2 )
{
    for (point in polygon )
    {
        var x = point.x / 10.0;
        var y = point.y / 10.0;
        // trace( 'x: $x, y: $y' );
        p.push(new geom.Vector3D(x, 0., y));
        strPoints += '( $x, $y ), ';
        ++mn;
    }
    if (mn > 0)
    {
        /*
         * TXWTODO: What if we would have multiple polygons?
         */
        for (i in 0...mn )
        {
            var v0 = p[i];
            var v1 = p[(i + 1) % mn].clone();
            v1.subtract(v0);
            var sideLength = v1.length();
            if (sideLength < minHouseSide) minHouseSide = sideLength;
        }
    }

    /*
     * Now, compute the length of each of the sides and store them.
     * We derive design decitions from the lengths.
     */
}
if (strPoints.length > 0)
{
    quarter.addDebugTag("quarterPoints", strPoints);
}
if (0 == mn)
{
    quarter.addDebugTag("estateTooSmall", "true");
}
        } else
{
    // trace( 'no house[0]' );
    quarter.addDebugTag("estateWithoutPoints", "true");
}

if (mn == 0)
{
    return;
}

/*
 * But do not build everywhere. Trivial: Remove 30% of the buildings.
 */
if (_rnd.getFloat() > 0.7)
{
    quarter.addDebugTag("leftWithoutBuilding", "true");
    return;
}
p.reverse();

/*
 * If there are any points in the solution, then add the single polygon as the estate.
 * This polygon possibly is concave, but it is describing the entire building.
 */

/*
 * We have the concave polygon, create a collection of convex polygons
 */

// var convexPolys = geom.Tools.concaveToConvex( p );

var building = new streets.Building();
building.addPoints(p);
var maxHeight = 10.0;
if (minHouseSide <= 2.0)
{
    maxHeight = 1. * _storyHeight;
}
else if (minHouseSide <= 5.0)
{
    maxHeight = 2. * _storyHeight;
}
else if (minHouseSide <= 10.0)
{
    maxHeight = 8. * _storyHeight;
}
else
{
    maxHeight = 160. * _storyHeight;
}

var height = _storyHeight * Std.int((15.0 + _rnd.getFloat() *250.)/ _storyHeight);
if (height > maxHeight) height = maxHeight;
building.setHeight(height);

quarter.addDebugTag("haveBuilding", "true");
estate.addBuilding(building);

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
    public function generate(): Void
{

    _rnd.clear();
    _strokeStore.clearTraversed();
    var points = _strokeStore.getStreetPoints();
    for (spStart in points )
    {
        if (_traceGenerate) trace('QuarterGenerator(): Tracing Point ${spStart.pos.x}, ${spStart.pos.y}');
        var angleStrokes = spStart.getAngleArray();
        for (stroke in angleStrokes )
        {
            var spDest = null;
            // Which direction?
            var isAlreadyTraversed = false;
            if (stroke.a == spStart)
            {
                if (stroke.traversedAB)
                {
                    isAlreadyTraversed = true;
                }
                spDest = stroke.b;
            }
            else if (stroke.b == spStart)
            {
                if (stroke.traversedBA)
                {
                    isAlreadyTraversed = true;
                }
                spDest = stroke.a;
            }
            else
            {
                throw 'QuarterGenerator: Invalid stroke encountered.';
            }
            if (spStart == spDest)
            {
                throw 'QuaterGenerator: Invalid stroke: Start and end is the same.';
            }

            if (isAlreadyTraversed)
            {
                continue;
            }
            if (_traceGenerate) trace('QuarterGenerator():');
            if (_traceGenerate) trace('QuarterGenerator(): Starting with stroke from ${spStart.pos.x}, ${spStart.pos.y} to ${spDest.pos.x}, ${spDest.pos.y}');

            /*
             * We know that we need to start from spStart using "stroke". Follow the loop.
             */
            var spCurr = spStart;
            var strokeCurrent = stroke;
            var solestroke = false;

            var quarter = new Quarter();
            var hasNullSection = false;
            var hasDeadEnd = false;
            var nPoints = 0;
            var sumOfAngles = 0.0;
            var previousStroke;

            while (true)
            {

                if (null == strokeCurrent)
                {
                    throw 'QuarterGenerator(): strokeCurrent is null';
                }
                if (null == spCurr)
                {
                    throw 'QuarterGenerator(): spCurr is null';
                }

                /*
                 * For readability: First figure out the direction.
                 */
                var isAB = false;
                var spNext = null;
                if (strokeCurrent.a == spCurr)
                {
                    isAB = true;
                    spNext = strokeCurrent.b;
                }
                else if (strokeCurrent.b == spCurr)
                {
                    isAB = false;
                    spNext = strokeCurrent.a;
                }
                else
                {
                    throw 'QuarterGenerator: Invalid stroke following quarter.';
                }

                /*
                 * That's a bit difficult: stroke.angle is valid from a to b. So if we 
                 * try a stroke while being point a, everything is fine.
                 * However, if we are b, we need to invert the angle for the purpose of
                 * following.
                 */
                var followAngle = geom.Angles.snorm(strokeCurrent.angle + ((!isAB) ? Math.PI : 0.));
                sumOfAngles += followAngle;

                /*
                 * Hint what we are doing.
                 */
                if (_traceGenerate) trace('QuarterGenerator(): Following angle ${followAngle} (${geom.Angles.snorm(followAngle+Math.PI)}) from ${isAB?"A":"B"} to ${isAB?"B":"A"}: ${spNext.pos.x}, ${spNext.pos.y}');

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
                    if (strokeCurrent.traversedAB)
                    {
                        throw 'QuarterGenerator(): Attempt to traverseAB twice';
                    }
                    strokeCurrent.traversedAB = true;
                }
                else /* B to A */
                {
                    if (strokeCurrent.traversedBA)
                    {
                        throw 'QuarterGenerator(): Attempt to traverseBA twice';
                    }
                    strokeCurrent.traversedBA = true;
                }

                var quarterDelim = new QuarterDelim();
                quarterDelim.streetPoint = spCurr;
                quarterDelim.stroke = strokeCurrent;

                /*
                 * Before we add the delimiter, we need the next stroke, because
                 * we need the intersection of this and the next stroke.
                 */
                var strokeNext = spNext.getNextAngle(strokeCurrent, followAngle, true);
                if (null == strokeNext || strokeNext == strokeCurrent)
                {
                    if (_traceGenerate) trace('QuarterGenerator(): Followed same stroke back because there is no other angle.');
                    /*
                     * So follow myself back in the other direction.
                     * That means, spCurr (regularily) will become spNext.
                     * The next stroke, however, will be the same one as the current one.
                     */
                    strokeNext = strokeCurrent;
                    hasDeadEnd = true;
                    quarter.addDebugTag("hasDeadEnd", "true");
                }

                {
                    var section = spNext.getSectionPointByStroke(strokeNext, strokeCurrent);
                    if (null == section)
                    {
                        hasNullSection = true;
                        quarter.addDebugTag("hasNullSection", "true");
                    }
                    else
                    {
                        quarterDelim.startPoint = section.clone();
                    }
                }
                quarter.addQuarterDelim(quarterDelim);
                ++nPoints;

                if (spNext == spStart)
                {
                    if (_traceGenerate) trace('QuarterGenerator(): Reached start again.');
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
                if (_traceGenerate) trace('QuarterGenerator(): Leaving loop because this is a single stroke.');
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
                    if (_traceGenerate) trace('QuarterGenerator.generate(): Has null section.');
                }
                else
                {
                    /*
                     * Now create the root estate.
                     */
                    var estate = new Estate();
                    for (delim in quarter.getDelims())
                    {
                        estate.addPoint(new geom.Vector3D(delim.startPoint.x, 0, delim.startPoint.y));
                    }

                    /*
                     * Create the building(s) on that estate
                     */
                    if (true || _rnd.getFloat() > 0.05)
                    {
                        quarter.addDebugTag("shallHaveBuildings", "true");
                        createBuildings(quarter, estate);
                    }

                    quarter.addEstate(estate);
                    if (_traceGenerate) trace('QuarterGenerator.generate(): Adding quarter.');
                    quarter.polish();
                    var cp = quarter.getCenterPoint();
                    quarter.addDebugTag("centerPoint", 'x: ${cp.x}, y: ${cp.y}');
                    _quarterStore.add(quarter);
                }

            }
        }
    }

}

public function reset(
    seed0: String,
        quarterStore: QuarterStore,
        strokeStore: StrokeStore
    ) {
    _rnd = new engine.RandomSource(seed0);
    _quarterStore = quarterStore;
    _strokeStore = strokeStore;
}

public function new()
{
}
    }
#endif
}
