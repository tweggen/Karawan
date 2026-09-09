using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using builtin.tools;
using ClipperLib;
using engine.world;
using static engine.Logger;

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
        /*
         * One copy, shared with the shop window that has to line up with a storey of the
         * building this sizes. See engine.world.MetaGen.StoryHeight.
         */
        private float _storyHeight = engine.world.MetaGen.StoryHeight;
        private string _seed0;
        

        /**
         * Add the possible shops in this building.
         *
         * Only when they are tagged later by the quarter generator they are
         * populated with actual visual shops.
         *
         * TXWTODO: We should partition large shopfronts into several
         * smaller ones.
         */
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
                var vNAB = new Vector3(-vAB.Z, 0, vAB.X);
                var vuNAB = Vector3.Normalize(vNAB);

                float lenBefore = (len - (float)nFronts * minShopWidth) / 2;
                
                Vector3 v3Start = pA + vuAB * lenBefore + vuNAB * 0.1f;
                for (int j = 0; j < nFronts; ++j)
                {
                    Vector3 v3Next = v3Start + vuAB * minShopWidth;

                    /*
                     * Be boring, evenly split into minShopWidth windows.
                     * of individual shops.
                     */
                    {
                        List<Vector3> frontPoints = new();
                        frontPoints.Add(v3Start);
                        frontPoints.Add(v3Next);
                        ShopFront shopFront = new();
                        shopFront.AddPoints(frontPoints);
                        building.AddShopFront(shopFront);
                    }
                    v3Start = v3Next;
                }
                // frontPoints.Add(pA + vuAB * (lenBefore+len) + vuNAB*0.1f);
            }
        }

        private List<string> _determineBuildingRoles(Building building)
        {
            // Seed from building position for determinism
            var buildingCenter = building.GetCenter();
            var rnd = new RandomSource(_clusterDesc.IdString + "-building-" + buildingCenter.GetHashCode());

            // Compute location attribute intensity at building center
            float livingIntensity = _clusterDesc.GetAttributeIntensity(
                buildingCenter + _clusterDesc.Pos,
                ClusterDesc.LocationAttributes.Living);
            float downstairsIntensity = _clusterDesc.GetAttributeIntensity(
                buildingCenter + _clusterDesc.Pos,
                ClusterDesc.LocationAttributes.Downtown);
            float industrialIntensity = _clusterDesc.GetAttributeIntensity(
                buildingCenter + _clusterDesc.Pos,
                ClusterDesc.LocationAttributes.Industrial);

            // Decision tree: which role(s) does this building fill?
            var roles = new List<string>();

            if (livingIntensity > 0.6f)
                roles.Add("residential");

            if (downstairsIntensity > 0.5f && livingIntensity < 0.7f)
                roles.Add("office");

            if (industrialIntensity > 0.4f)
                roles.Add("warehouse");

            // If no roles assigned, default to residential
            if (roles.Count == 0)
                roles.Add("residential");

            return roles;
        }


        /**
         * The carriageways whose footprint can reach into this block.
         *
         * Bounded by the block's own AABB grown by the widest thing a footprint adds to
         * a carriageway, so that a block nowhere near one is not merely unaffected but
         * does not go through the difference at all.
         *
         * ⚠️ THE BOX RATHER THAN THE OUTLINE, deliberately, and it is not the same
         * question. "Is this road inside this block" would be a point-in-polygon test on
         * the ring; what is asked here is "does this road's own footprint reach into this
         * block's land", and the answer to that is the difference itself. A spur hangs off
         * a junction that is ON some block's ring, so its carriageway widened by a pavement
         * width pokes a little way past that junction into the block on the other side -
         * and that ground is road there too, so it is right for it to come out of that
         * estate as well. Measured over the seventy shipped cities: a block that holds no
         * spur of its own loses land to one on 121 blocks flag off and 10 flag on, a median
         * 1.0 m² off a corner, and where a road's box reaches a block it does not touch the
         * difference is a no-op.
         *
         * @param strokes
         *     Every ramp, bridge and tunnel in this network - empty in every city built
         *     with joyce.EnableGradeSeparation off - or every spur corridor the peel took
         *     out of the block graph. Handed down from Generate() rather than cached in a
         *     field, because a field is something a second Generate() would have to
         *     remember to refresh and nothing would notice if it did not.
         */
        internal static IEnumerable<Stroke> Reaching(
            Quarter quarter, List<Stroke> strokes)
        {
            if (0 == strokes.Count)
            {
                yield break;
            }

            var aabb = quarter.AABB;

            foreach (var s in strokes)
            {
                float reach = s.StreetWidth() / 2f + quarter.SidewalkWidth;

                float minX = Single.Min(s.A.Pos.X, s.B.Pos.X) - reach;
                float maxX = Single.Max(s.A.Pos.X, s.B.Pos.X) + reach;
                float minY = Single.Min(s.A.Pos.Y, s.B.Pos.Y) - reach;
                float maxY = Single.Max(s.A.Pos.Y, s.B.Pos.Y) + reach;

                if (maxX < aabb.AA.X || minX > aabb.BB.X
                                     || maxY < aabb.AA.Z || minY > aabb.BB.Z)
                {
                    continue;
                }

                yield return s;
            }
        }


        /**
         * ⚠️ THE BUILDABLE LAND OF ONE CITY BLOCK: inset, then subtract, then count the
         * polygons.
         *
         * That is the whole of the notch-or-split rule §7t.10 measured and it carries no
         * threshold and no new constant. The estate is the block's outline; leaving room
         * for the pavement insets it by the block's own Quarter.SidewalkWidth (which the
         * block floor also insets its cap by, so that the pavement and the building wall
         * meet); and any carriageway standing inside the block - a lifted structure, or a
         * dead-end spur the peel took out of the block graph - is taken out of the result,
         * widened by that same pavement width.
         *
         * A shallow spur then leaves ONE polygon with a slot cut out of it and a deep one
         * leaves TWO, because the neck between them was narrower than two pavement widths
         * and the inset the estate was always going to get closed it. Nothing here decides
         * which; the geometry does, and the caller designs one building per polygon.
         *
         * ⚠️ THE ORDER OF THE TWO STEPS IS WORTH EIGHT TIMES THE GEOMETRY. Subtracting
         * before the inset insets the notch as well, widening it by a second pavement
         * width: measured over the seventy shipped cities, 163 / 646 blocks come back in
         * two pieces that way against 21 / 150 this way, from identical geometry
         * (§7t.10.2). Nothing in the tree stated which order was intended before WP-O2;
         * this is the one that does.
         *
         * Returns null when the block's outline has no points at all, which is what the
         * caller records as "estateWithoutPoints".
         *
         * Internal so that a test can ask a real generated block what land it has without
         * a second copy of these three steps - the reconstruction in SpurBlocks was that
         * copy, and its whole value was being written independently of this.
         */
        internal static List<List<IntPoint>> BuildableLandOf(
            in Quarter quarter, in Estate estate,
            List<Stroke> structures, List<Stroke> spurs)
        {
            List<IntPoint> polyPoints = new();
            List<List<IntPoint>> polyList = new();
            polyList.Add(polyPoints);
            var clipperOffset = new ClipperOffset();
            foreach(var point in estate.GetPoints()) {
                polyPoints.Add( new IntPoint((int)(point.X*10f), (int)(point.Z*10f) ) );
            }

            if (0 == polyPoints.Count)
            {
                return null;
            }

            clipperOffset.AddPaths(polyList, JoinType.jtMiter, EndType.etClosedPolygon);
            List<List<IntPoint>> solution2 = new();

            /*
             * How much room to leave around the building for the pavement.
             * TXWTODO: High buildings might have a larger entrace area, don't they?
             *
             * Tenth metres, because that is the unit the polygon above is in - Clipper works
             * in integers. The number itself belongs to the block: the block floor insets
             * its cap by the same width so that the strip along the kerb is level across,
             * and if the two ever disagreed the pavement and the building wall would stop
             * meeting. See Quarter.SidewalkWidth.
             */
            clipperOffset.Execute(ref solution2, -quarter.SidewalkWidth * 10f);

            /*
             * ⚠️ A lifted corridor leaves its ramps and its deck INSIDE this block,
             * because the blocks either side of it merged when the structure left the
             * block graph. Nothing else here knows that: the estate is the outline,
             * the inset above is the building footprint, and a building would be put
             * under the deck or across the ramp.
             *
             * Returns the same list when this block has no structure in it, which is
             * every block of every city with joyce.EnableGradeSeparation off.
             */
            solution2 = generation.BlockGraph.ExcludeStructures(
                solution2, Reaching(quarter, structures), quarter.SidewalkWidth);

            /*
             * ⚠️ AND THE DEAD-END SPUR STANDING IN THIS BLOCK, which is the same rule
             * and the same margin (§7u, WP-O2). The block graph is peeled to its
             * 2-core before a face is traced, so a spur is not a block edge and the
             * ring closes round it rather than being cut short by a chord - and the
             * spur is then INSIDE the block, exactly as a lifted corridor is. Without
             * this the estate is laid over it and a building is designed across a
             * street: measured over the seventy shipped cities, on 3247 blocks flag
             * off and 2986 flag on, which is the reported symptom.
             *
             * Returns the same list when no spur reaches this block.
             */
            solution2 = generation.BlockGraph.ExcludeCarriageways(
                solution2, Reaching(quarter, spurs), quarter.SidewalkWidth);

            return solution2;
        }


        /**
         * ⚠️ CAN THIS PIECE OF BUILDABLE LAND CARRY A BUILDING AT ALL? One predicate,
         * because there has only ever been one question here.
         *
         * The loop below used to ask "does this polygon have any points", which is the
         * same rule with its threshold at zero: it refused a piece of 0 m² and accepted
         * one of 0.005 m², i.e. fifty square centimetres, and built a three metre tower on
         * it. §7x counted 4 such buildings under a square metre flag off and 53 flag on,
         * standing on the tiny triangles between three roads, and nothing anywhere held
         * them to existing - _designBuilding's minHouseSide <= 2.0f holds them to one
         * STOREY, which is a different question.
         *
         * The floor is engine.world.MetaGen.MinBuildingArea, an owner-given real-world
         * number (see it for the decision in the owner's own words). A piece below it is
         * left as what it already is - pavement - exactly as a piece of no area is.
         *
         * Clipper works in tenth metres, so its own area comes back in hundredths of a
         * square metre; the comparison is scaled rather than the area, so that a polygon
         * with no points, two points or a zero area is refused by the same expression and
         * not by a special case beside it.
         *
         * ⚠️ AND HOW WIDE IT IS, WHICH AREA DOES NOT SAY (§7z). The thinnest building of
         * the shipped world was 0.32 m thick with a 95.7 m perimeter round 17.2 m² of
         * floor - seventy per cent clear of the area floor, and no area threshold a city
         * would survive can reach that shape. So the second half of the question is
         * MetaGen.MinBuildingWidth, asked as "does this piece survive being inset by half
         * that width all round" - §7x's own half-width expression at one radius, and the
         * estate's own mitred inset rather than an exact disc erosion (see it).
         *
         * ⚠️ THE TWO ARE INDEPENDENT AND NEITHER SUBSUMES THE OTHER, which is why they are
         * two terms of one predicate rather than one of them dressed as the other: the
         * disc the width rule asks for has an area of π·1² ≈ 3.14 m², well under ten, so a
         * 20 m² ribbon passes the area floor and fails the width one, and a 5 m² square is
         * 2.24 m on a side, so it passes the width floor and fails the area one.
         *
         * The area term is asked first because it is arithmetic on points already in hand
         * and the width term is a ClipperOffset.
         */
        internal static bool CanCarryABuilding(List<IntPoint> land)
            => Math.Abs(Clipper.Area(land)) >= 100.0 * world.MetaGen.MinBuildingArea
               && generation.BlockGraph.SurvivesInsetBy(
                   land, 0.5f * world.MetaGen.MinBuildingWidth);


        /**
         * Design what stands on one block, on every piece of buildable land it has.
         *
         * ⚠️ ONE BUILDING PER POLYGON, and until WP-O2 every polygon of the inset was
         * concatenated into a single ring - the TXWTODO that used to sit on the loop below
         * said so, and it has been there since the file was written. That is harmless
         * while the answer is one polygon and nonsense the moment it is two: the ring
         * self-crosses, minHouseSide is measured across the gap between two pieces rather
         * than along either of them, and one building is designed over both.
         *
         * The pieces come from the geometry rather than from a policy, which is the whole
         * of §7t.10's answer: the estate is the block inset by its own pavement width, a
         * shallow notch leaves one polygon and a neck narrower than two pavement widths
         * insets to nothing and Clipper returns two. No threshold and no new constant -
         * "inset, then count polygons".
         *
         * A block with exactly one piece runs the sequence it always ran, draw for draw:
         * the 30 % refusal, the height and the shopping test are consumed inside the loop
         * in the order they were consumed outside it. A second piece takes three more
         * draws of the block's own RandomSource, so the estates on one block do not all
         * make the same decisions.
         */
        private void _createBuildings(
            in Quarter quarter, in Estate estate, List<Stroke> structures,
            List<Stroke> spurs)
        {
            var v2QuarterCenter = quarter.GetCenterPoint();

            /*
             * To behave predictably no matter what platform, parallelization
             * or caching we are on, we use a local random source using the
             * position of the quarter only.
             */
            RandomSource rndQuarter;
            rndQuarter = new RandomSource($"{((int)(v2QuarterCenter.X * 10))^((int)(v2QuarterCenter.Y*10))}{_seed0}"); 

            /*
             * The current implementation takes one estate, shrinks the real estate
             * (like a sidewalk). If any suitable area remains, this is gonna be
             * the house.
             *
             * We also derive attributes of the house from the size of the estate.
             */

            float downtownness =
                _clusterDesc.GetAttributeIntensity(
                     _clusterDesc.Pos + new Vector3(v2QuarterCenter.X, 0f, v2QuarterCenter.Y),
                    ClusterDesc.LocationAttributes.Downtown);

            var solution2 = BuildableLandOf(quarter, estate, structures, spurs);

            if (null != solution2)
            {
                var strPoints = "";
                int nPoints = 0;

                /*
                 * ⚠️ ONE BUILDING PER POLYGON. This loop used to concatenate every polygon
                 * into one ring - "TXWTODO: What if we would have multiple polygons?" -
                 * and design a single self-crossing building across the lot. There are
                 * three ways to get more than one: the pavement inset can pinch a block in
                 * two on its own, a structure can cut one in two, and a spur can. §7t.10
                 * measured the last of those over the world and found the estate comes back
                 * in one piece on 99.6 % / 96.6 % of the blocks that hold a spur, so the
                 * second estate is the exception the geometry announces rather than a
                 * policy with a threshold in it.
                 */
                foreach (var polygon in solution2)
                {
                    /*
                     * ⚠️ One predicate, and it used to be "0 == polygon.Count" - the same
                     * rule with its threshold at zero. See CanCarryABuilding.
                     */
                    if (!CanCarryABuilding(polygon))
                    {
                        continue;
                    }

                    List<Vector3> p = new();
                    foreach (var point in polygon)
                    {
                        float x = point.X / 10f;
                        float y = point.Y / 10f;
                        // trace( 'x: $x, y: $y' );
                        p.Add(new Vector3(x, 0f, y));
                        strPoints += $"( $x, $y ), ";
                    }

                    nPoints += p.Count;

                    /*
                     * Now, compute the length of each of the sides and store them.
                     * We derive design decitions from the lengths.
                     *
                     * ⚠️ Round THIS polygon rather than round the concatenation of all of
                     * them: the step from the last corner of one piece to the first corner
                     * of the next is not a side of anything, and it was being measured as
                     * the shortest one.
                     */
                    int mn = p.Count;
                    float minHouseSide = Single.MaxValue;
                    for (int i = 0; i < mn; ++i)
                    {
                        Vector3 v0 = p[i];
                        Vector3 v1 = p[(i + 1) % mn];
                        v1 -= v0;
                        float sideLength = v1.Length();
                        if (sideLength < minHouseSide) minHouseSide = sideLength;
                    }

                    _designBuilding(quarter, estate, p, minHouseSide, downtownness, rndQuarter);
                }

                if (strPoints.Length > 0)
                {
                    quarter.AddDebugTag("quarterPoints", strPoints);
                }
                if (0 == nPoints)
                {
                    /*
                     * ⚠️ Not one piece of this block's land can carry a building - which
                     * since §7y means "none of them reaches MetaGen.MinBuildingArea",
                     * since §7z "or MetaGen.MinBuildingWidth across", and used to mean
                     * "there are no pieces at all". The tag moves with the
                     * predicate deliberately: it is the generator's own record of the
                     * refusal, and two records of one rule is how the two would drift.
                     * §7x measured the old set - 9 blocks flag off and 522 flag on, every
                     * one of them a traffic island entirely inside its own pavement.
                     */
                    quarter.AddDebugTag("estateTooSmall", "true");
                }
            } else
            {
                // trace( 'no house[0]' );
                quarter.AddDebugTag("estateWithoutPoints", "true");
            }
        }


        /**
         * Design one building on one piece of buildable land.
         *
         * The draws are taken in the order they were taken when this was straight-line
         * code inside _createBuildings, so a block with a single piece of land - which is
         * every block of the shipped flat city that has no spur and no structure in it -
         * consumes exactly the sequence it always consumed.
         */
        private void _designBuilding(
            in Quarter quarter, in Estate estate, List<Vector3> p,
            float minHouseSide, float downtownness, RandomSource rndQuarter)
        {
            /*
             * But do not build everywhere. Trivial: Remove 30% of the buildings.
             */
            if (rndQuarter.GetFloat() > 0.7f)
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

            float maxHeight;
            var building = new streets.Building() { ClusterDesc = _clusterDesc };
            building.AddPoints(p);
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

            var height = _storyHeight * (int)((15f + rndQuarter.GetFloat() * 250f)/ _storyHeight);
            if (height > maxHeight) height = maxHeight;

            building.SetHeight(height);

            quarter.AddDebugTag("haveBuilding", "true");
            quarter.Attributes |= Quarter.QuarterAttributes.Building;
            estate.AddBuilding(building);

            // Assign building roles based on location attributes
            var buildingRoles = _determineBuildingRoles(building);
            foreach (var role in buildingRoles)
            {
                building.Tags.Add(role);
            }

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
            float shoppingness = _clusterDesc.GetAttributeIntensity(
                p[0] + _clusterDesc.Pos,
                ClusterDesc.LocationAttributes.Shopping);
            // Trace($"shoppingness in {_clusterDesc.Name} is {shoppingness}");
            if (rndQuarter.GetFloat() <= shoppingness)
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
         *
         * ⚠️ THE FACE IS TRACED OVER THE BLOCK GRAPH'S 2-CORE (§7t/§7u). Every junction
         * with fewer than two block arms is peeled away first, so a dead-end spur is not
         * a block edge at all and cannot cut a slit into the face beside it. See
         * generation.BlockGraph.TwoCoreOf for why, and for what it costs until WP-O2
         * subtracts the spur from the estate.
         */
        public void Generate()
        {

            _strokeStore.ClearTraversed();
            var structures = _strokeStore.GetStrokes()
                .FindAll(s => StrokeKinds.IsStructure(s.Kind));

            var core = generation.BlockGraph.TwoCoreOf(_strokeStore);
            var accept = generation.BlockGraph.AcceptWithin(core);

            /*
             * The other half of the peel: whatever accept refuses as a block arm stands
             * INSIDE a block instead, so it comes out of that block's estate the way a
             * ramp does. Gathered once per city, from the same core, and handed down -
             * a field would be something a second Generate() had to remember to refresh.
             */
            var spurs = generation.BlockGraph.SpurCorridorsOf(_strokeStore, core);

            var points = _strokeStore.GetStreetPoints();
            foreach (var spStart in points)
            {
                /*
                 * Quarters, estates and buildings live on the ground. A face traced
                 * through a raised deck is not a city block, it is the hole under a
                 * bridge, and the ground face it sits on has already been traced
                 * separately. Currently a no-op: every junction is on level 0 until a
                 * multilayer ruleset is enabled.
                 */
                if (0 != spStart.Level)
                {
                    continue;
                }

                if (_traceGenerate) trace($"QuarterGenerator(): Tracing Point {spStart.Pos}");
                var angleStrokes = spStart.GetAngleArray();
                foreach (var stroke in angleStrokes)
                {
                    /*
                     * A ramp, bridge or tunnel is not an edge of a city block, and
                     * neither is a street leading off the 2-core - a dead-end spur, or
                     * the tree of streets hanging off one. Refusing to START on such an
                     * arm is only half of it - see the GetNextAngle call below, which
                     * takes the same predicate and is what stops a face being followed
                     * OUT along one and turned round at its far end.
                     */
                    if (!accept(stroke))
                    {
                        continue;
                    }

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

                    var quarter = new Quarter() { ClusterDesc = _clusterDesc };
                    var hasNullSection = false;
                    var hasDeadEnd = false;
                    var nPoints = 0;

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

                        /*
                         * Hint what we are doing.
                         */
                        if (_traceGenerate) trace($"QuarterGenerator(): Following angle {followAngle} ({geom.Angles.Snorm(followAngle + (float)Math.PI)}) from {isAB} ${spNext.Pos}");

                        /*
                         * In every iteration: Follow strokeCurrent from spCurr, look for
                         * the next stroke clockwise to it, add the corner between the two
                         * to the Quarter, and repeat until we are back on the DIRECTED
                         * EDGE we set out along.
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

                        /*
                         * Before we can build the delimiter, we need the next stroke,
                         * because we need the intersection of this and the next stroke.
                         */
                        var strokeNext = spNext.GetNextAngle(
                            strokeCurrent, followAngle, true, accept);
                        if (null == strokeNext || strokeNext == strokeCurrent)
                        {
                            /*
                             * Unreachable once the graph is peeled to its 2-core: every
                             * junction left in it has at least two arms to other junctions
                             * left in it, so there is always another one to turn onto.
                             * Kept as the backstop it always was, and asserted to fire on
                             * no face of any city.
                             */
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

                        var quarterDelim = new QuarterDelim();
                        {
                            /*
                             * The corner where this block turns off strokeCurrent onto
                             * strokeNext.
                             *
                             * ⚠️ NOT GetSectionPointByStroke, and the difference only
                             * shows once a structure exists. That map is keyed on pairs
                             * of arms ADJACENT in the junction's section array, and the
                             * section array is the junction CAP - a ramp leaving a foot
                             * has a carriageway and is part of it. So at a foot the two
                             * ordinary arms this block turns between are not adjacent
                             * there, the lookup misses, and the block would be silently
                             * discarded as hasNullSection. The corner is the mitre of the
                             * two arms the block actually turns between, from the one
                             * expression that answers that; where the two arms ARE
                             * adjacent - every junction of every city with the flag off -
                             * it is the same float the map holds, because the map is
                             * filled from it.
                             *
                             * A junction with fewer than two BLOCK arms has no corner, in
                             * exactly the way a one-armed junction has never had one: the
                             * section array of such a junction is empty, which is measured
                             * rather than assumed - over the seven pinned cities the count
                             * of junctions with an empty section array equals the count
                             * with fewer than two arms, exactly.
                             */
                            Vector2? section =
                                generation.BlockGraph.ArmCountOf(spNext) < 2
                                    ? null
                                    : spNext.SectionPointBetween(strokeCurrent, strokeNext);
                            if (null == section)
                            {
                                hasNullSection = true;
                                quarter.AddDebugTag("hasNullSection", "true");
                            }

                            /*
                             * All three parts of this delimiter belong to spNext, and
                             * they are written in one call so that they cannot come from
                             * different steps of the trace again: the corner is a section
                             * point OF spNext, and strokeNext is the street leaving spNext
                             * towards the next corner - which is the edge the corner
                             * starts. spCurr and strokeCurrent describe the edge that
                             * ARRIVES here; that is the PREVIOUS delimiter.
                             *
                             * A missing section leaves the corner at the origin, exactly
                             * as it did before, and the quarter is discarded below.
                             */
                            quarterDelim.SetEdge(
                                section ?? Vector2.Zero, spNext, strokeNext);
                        }
                        quarter.AddQuarterDelim(quarterDelim);
                        ++nPoints;

                        /*
                         * ⚠️ THE (JUNCTION, OUTGOING STROKE) PAIR, NOT THE JUNCTION.
                         *
                         * A face walk closes when it is about to repeat the directed edge
                         * it set out along; arriving back at the starting JUNCTION is not
                         * the same statement, because a face may pass through one junction
                         * twice. Stopping there closed the ring with a straight chord
                         * across the block instead of a street - 219 rings of the flat
                         * city and 274 of the shipped one, every one of them broken at
                         * this very edge and nowhere else (§7t.2).
                         *
                         * The 2-core removes the case that made this fire - a face is
                         * pinched at a junction only because a dead-end spur cuts a slit
                         * into it - so this is correctness rather than repair, and it is
                         * kept for that: it is what a face walk terminates on.
                         */
                        if (spNext == spStart && strokeNext == stroke)
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

                    /*
                     * ⚠️ THE OUTSIDE IS DETECTED EXPLICITLY NOW, and it has to be.
                     *
                     * The old comment here read "most likely, the outside does have dead
                     * ends, so do not add them as quarters" - which was true and was an
                     * accident: the outer face of a component ran through some dead-end
                     * spur on the city's edge and was refused as hasNullSection. Peel the
                     * spurs away and the outer face becomes a perfectly good closed ring
                     * of junctions that all have corners, so without a rule of its own it
                     * would be stored as one city block covering the whole city.
                     *
                     * The rule is the winding: this walk always turns to the next arm
                     * clockwise, so every interior face comes out one way round and the
                     * single outer face of each component the other.
                     */
                    var ring = quarter.GetDelims().Select(d => d.StartPoint).ToList();
                    if (!generation.BlockGraph.IsInteriorFace(ring))
                    {
                        if (_traceGenerate) trace($"QuarterGenerator.generate(): Outer face.");
                    }
                    else if (hasNullSection)
                    {
                        /*
                         * A junction on this ring has fewer than two block arms, so it has
                         * no corner. This used to discard a THIRD of all faces (§7t.4) and
                         * over the 2-core it discards none: every junction left in the core
                         * has at least two arms inside it, so the guard above never fires.
                         *
                         * ⚠️ Mutation testing says so out loud - deleting this branch passes
                         * every gate, and it is the one survivor of eleven. It is kept as
                         * the backstop it always was rather than deleted, because it is the
                         * only thing between a loosened peel and a corner left at the
                         * origin; what says it is unreachable rather than untested is
                         * TwoCoreBlockTests' Euler gate, which counts the blocks a
                         * component may have and would see one go missing.
                         */
                        if (_traceGenerate) trace($"QuarterGenerator.generate(): Has null section.");
                    }
                    else
                    {
                        /*
                         * Now create the root estate.
                         */
                        var estate = new Estate() { ClusterDesc = _clusterDesc };
                        List<Vector3> estatePoints = new();
                        foreach (var delim in quarter.GetDelims())
                        {
                            estatePoints.Add(new Vector3(delim.StartPoint.X, 0, delim.StartPoint.Y));
                        }

                        estate.AddPoints(estatePoints);

                        /*
                         * Create the building(s) on that estate
                         */
                        {
                            quarter.AddDebugTag("shallHaveBuildings", "true");
                            _createBuildings(quarter, estate, structures, spurs);
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


        public void Reset(
            string seed0,
            engine.world.ClusterDesc clusterDesc,
            QuarterStore quarterStore,
            StrokeStore strokeStore) 
        {
            _seed0 = seed0;
            _clusterDesc = clusterDesc;
            _quarterStore = quarterStore;
            _strokeStore = strokeStore;
        }

        public QuarterGenerator()
        {
        }
    }
}
