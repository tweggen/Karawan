using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.navigation;
using engine.streets;
using engine.streets.generation;
using engine.world;
using JoyceCode.Tests.engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B6 — what turning grade separation on actually gives the game.
 *
 * The seven pinned seeds are a test fixture, not the world: they run 400 to 3000 m while
 * GenerateClustersOperator draws its city sizes from 800 to 3800 and lays SEVENTY of them.
 * So the headline of this work package is measured over the world the game builds, from
 * that operator's own generator seeded exactly as MetaGen seeds it - the same harness the
 * intercity work used for exactly this reason.
 *
 * The two properties that go with the count are here as well:
 *
 *   - what a lift COSTS a driver. A grade separated crossing has no slip roads, so the
 *     through road and the road beneath stop being joined at that point; the component
 *     count cannot see that (the lift adds a path between the two far ends while removing
 *     two arms, so nothing is ever disconnected), and the falsifiable statement is the
 *     DETOUR between the crossing's own neighbours, before and after;
 *   - that every junction is still reachable over car lanes, which is what would catch a
 *     deck whose ramps got no lanes.
 */
public class ShippedWorldStructureTests
{
    private static readonly object _lo = new();
    private static List<(ClusterDesc Cluster, StrokeStore Store, StructurePlacementReport Report)>
        _world;


    /**
     * Every city of the shipped world, generated with the flag on, on the shipped terrain.
     *
     * Cached: seventy cities of up to 2600 strokes is about eight seconds, and every test
     * in this file wants the same seventy.
     */
    private static List<(ClusterDesc Cluster, StrokeStore Store, StructurePlacementReport Report)>
        World()
    {
        lock (_lo)
        {
            if (null != _world) return _world;

            _world = new();
            foreach (var cd in IntercityWorldHarness.Clusters())
            {
                var (store, generator) = StreetHarness.GenerateHeavyFirstReporting(
                    cd.IdString, cd.Size,
                    sp => ShippedTerrain.HeightAt(
                        cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y));

                _world.Add((cd, store, generator.StructurePlacement));
            }

            return _world;
        }
    }


    /*
     * ================================================== B6.1 the headline ============
     */

    /**
     * ⚠️ THE HEADLINE: 737 structures across the seventy cities of the shipped world.
     *
     * 643 bridges and 94 tunnels, in 60 of the 70 cities. The ten with none are the small
     * ones - a city needs 99.7 m of arm at a crossing before two ramps and a deck fit at
     * all (§13.3), and a 1000 m town has no such crossing.
     *
     * Recorded as an exact equality rather than a bound, because this is the number the
     * whole phase exists to produce and a ruleset change that halves it should be read and
     * argued with rather than silently tolerated.
     */
    [Fact]
    public void TheShippedWorldGetsThisManyStructures()
    {
        var world = World();

        Assert.Equal(70, world.Count);

        int placed = world.Sum(c => c.Report.Placed);
        int bridges = world.Sum(c => c.Report.Bridges);
        int tunnels = world.Sum(c => c.Report.Tunnels);
        int withAny = world.Count(c => c.Report.Placed > 0);

        Assert.Equal(737, placed);
        Assert.Equal(643, bridges);
        Assert.Equal(94, tunnels);
        Assert.Equal(60, withAny);
        Assert.Equal(placed, bridges + tunnels);
    }


    /**
     * A tunnel appears exactly where the road beneath weighs more than the corridor
     * (§13.6), so both kinds have to occur - a world of nothing but bridges would satisfy
     * every other assertion here and would mean B4.2 had stopped working.
     */
    [Fact]
    public void BothKindsOfStructureOccurInTheShippedWorld()
    {
        var world = World();

        Assert.Contains(world, c => c.Report.Bridges > 0);
        Assert.Contains(world, c => c.Report.Tunnels > 0);
        Assert.Contains(world, c => c.Report.Placed == 0);
    }


    /**
     * Every city of the shipped world comes out in one piece.
     *
     * §11.3's WouldDisconnect refusal is what guarantees it, and it is measured on all
     * seventy rather than on the seven that happen to be pinned - the rule fired on level
     * ground long before it fired on any terrain seed, so "no terrain city needs it" was
     * true right up until it was not.
     */
    [Fact]
    public void EveryCityOfTheShippedWorldIsOneComponent()
    {
        foreach (var (cd, store, _) in World())
        {
            if (0 == store.GetStreetPoints().Count) continue;

            Assert.Equal(1, StreetHarness.CountComponents(store));
        }
    }


    /*
     * ================================================== B6.2 the detour ==============
     */

    /**
     * The seeds whose flag-on city carries a structure, with the worst detour a lift
     * imposes on the crossing's own neighbours, as a ratio of the distance before it.
     *
     * ⚠️ THE COMPONENT COUNT CANNOT SEE A LIFT AT ALL. A separation removes the two arms
     * of the crossing and adds a ramp-deck-ramp path between their far ends, so the graph
     * stays connected by construction and "still one component" is true of a policy that
     * did nothing. What changes is distance: from the road that now passes UNDERNEATH, the
     * far end of the road above is no longer one junction away, and the shortest way there
     * is round the block.
     */
    public static IEnumerable<object[]> DetourSeeds => new List<object[]>
    {
        //                                   lifts   worst detour ratio
        new object[] { "Yelukhdidru", 800f,      2,   2.97f },
        new object[] { "seed000",     1500f,     3,   2.58f },
        new object[] { "seed017",     2400f,     5,   3.67f },
        new object[] { "Yelukhdidru", 3000f,    10,   2.80f },
    };


    /**
     * For each lifted crossing, the shortest path between every pair of its own
     * neighbours, before the lift and after it.
     *
     * "Before" is not a second generation run - it is this network with every structure
     * taken out and the removed arms put back, which is exactly what the placer did to
     * it: a stroke is a straight segment, so the arm it removed had precisely the length
     * of the chord between its two junctions.
     */
    [Theory]
    [MemberData(nameof(DetourSeeds))]
    public void ALiftedCrossingCostsItsNeighboursThisMuch(
        string idString, float size, int expectedLifts, float worstRatio)
    {
        var (store, report) = _terrain(idString, size);

        Assert.Equal(expectedLifts, report.Lifts.Count);

        var after = _graphOf(store, includeStructures: true);
        var before = _graphOf(store, includeStructures: false);

        var byId = store.GetStreetPoints().ToDictionary(sp => sp.Id);

        /*
         * Put the lifted arms back. Both ends still exist - the crossing keeps its place
         * and the feet are ordinary junctions - so this is the ground network the city had
         * before the placement pass ran.
         */
        foreach (var (crossing, footA, footB) in report.Lifts)
        {
            _join(before, crossing, footA,
                (byId[crossing].Pos - byId[footA].Pos).Length());
            _join(before, crossing, footB,
                (byId[crossing].Pos - byId[footB].Pos).Length());
        }

        float worst = 1f;
        int nPairs = 0;

        foreach (var (crossing, footA, footB) in report.Lifts)
        {
            var neighbours = before[crossing].Select(e => e.To).Distinct().ToList();

            Assert.Contains(footA, neighbours);
            Assert.Contains(footB, neighbours);

            foreach (int from in neighbours)
            {
                var dBefore = _dijkstra(before, from, neighbours);
                var dAfter = _dijkstra(after, from, neighbours);

                foreach (int to in neighbours)
                {
                    if (to == from) continue;

                    Assert.True(dBefore.ContainsKey(to),
                        $"{idString}@{size}: {from} could not reach {to} before the lift");
                    Assert.True(dAfter.ContainsKey(to),
                        $"{idString}@{size}: the lift at {crossing} cut {from} off from "
                        + $"{to}");

                    ++nPairs;
                    worst = Single.Max(worst, dAfter[to] / Single.Max(dBefore[to], 1e-3f));
                }
            }
        }

        Assert.True(nPairs > 0);
        Assert.True(worst <= worstRatio + 0.01f,
            $"{idString}@{size}: worst detour is {worst:F2}x, not {worstRatio:F2}x");

        /*
         * ...and it DOES cost something. Without this the whole theory is satisfied by a
         * policy that placed nothing at all, or by one whose lifts happened to leave every
         * neighbour exactly as far away as before.
         */
        Assert.True(worst > 1.5f,
            $"{idString}@{size}: the worst detour is {worst:F2}x, so this measured nothing");
    }


    /**
     * ...and the shape of it, over all four seeds at once: no neighbour is ever cut off,
     * and the MEDIAN pair is barely touched.
     *
     * The median is what says the detour is local: most of a crossing's neighbours were
     * never joined through the crossing in the first place, and the pairs that were - the
     * two feet, and each foot against the road underneath - are the ones that pay.
     *
     * Measured over the 264 ordered pairs of the four seeds: p50 1.00, p95 2.63, p99 3.21,
     * worst 3.67. Nothing is ever cut off, and nobody has to go more than three and a half
     * times as far as before.
     */
    [Fact]
    public void MostPairsAtALiftedCrossingDoNotMoveAtAll()
    {
        var ratios = new List<float>();

        foreach (var row in DetourSeeds)
        {
            var (store, report) = _terrain((string)row[0], (float)row[1]);

            var after = _graphOf(store, includeStructures: true);
            var before = _graphOf(store, includeStructures: false);
            var byId = store.GetStreetPoints().ToDictionary(sp => sp.Id);

            foreach (var (crossing, footA, footB) in report.Lifts)
            {
                _join(before, crossing, footA,
                    (byId[crossing].Pos - byId[footA].Pos).Length());
                _join(before, crossing, footB,
                    (byId[crossing].Pos - byId[footB].Pos).Length());
            }

            foreach (var (crossing, _, _) in report.Lifts)
            {
                var neighbours = before[crossing].Select(e => e.To).Distinct().ToList();

                foreach (int from in neighbours)
                {
                    var dBefore = _dijkstra(before, from, neighbours);
                    var dAfter = _dijkstra(after, from, neighbours);

                    foreach (int to in neighbours)
                    {
                        if (to == from) continue;
                        ratios.Add(dAfter[to] / Single.Max(dBefore[to], 1e-3f));
                    }
                }
            }
        }

        ratios.Sort();

        Assert.True(ratios.Count >= 100, $"only {ratios.Count} pairs measured");
        Assert.Equal(1.00f, ratios[ratios.Count / 2], 2);
        Assert.True(ratios[(int)(ratios.Count * 0.95)] < 2.65f,
            $"p95 detour is {ratios[(int)(ratios.Count * 0.95)]:F2}x");
    }


    /*
     * ================================================== B6.2 reachability ============
     */

    /**
     * Every junction of a flag-on city is reachable over car lanes.
     *
     * ⚠️ THIS IS THE ONE THAT WOULD CATCH A DECK WITH NO RAMP LANES. The stroke graph
     * being one component says nothing about the nav map: GenerateNavMapOperator emits a
     * lane per stroke, and a rule that skipped a structure by Kind - which several rules
     * in this phase legitimately do - would leave a deck's two junctions joined to each
     * other and to nothing else, with every other gate in the suite still green.
     *
     * Reachability over a graph with positive edge costs is what A* finds and nothing
     * more, so it is measured as reachability; a real A* is driven separately, below,
     * onto the deck itself.
     */
    [Theory]
    [MemberData(nameof(DetourSeeds))]
    public void EveryCarJunctionIsReachable(
        string idString, float size, int expectedLifts, float worstRatio)
    {
        var city = _navCityOf(idString, size);

        var carJunctions = new HashSet<NavJunction>();
        foreach (var nl in city.Content.Lanes)
        {
            if (!nl.AllowedTypes.HasFlag(TransportationType.Car)) continue;
            carJunctions.Add(nl.Start);
            carJunctions.Add(nl.End);
        }

        Assert.True(carJunctions.Count > 0);

        /*
         * ⚠️ EVERY JUNCTION OF THE STORE HAS TO BE IN THERE, and asking the LANES which
         * junctions exist cannot say that. A rule that skipped a structure by Kind - which
         * several rules in this phase legitimately do - would emit no lane for a ramp or a
         * deck, the deck ends would simply not appear in this set, and the reachability
         * assertion below would pass over the smaller set it was handed. Measured: that
         * mutation failed one seed of four before this was written, for an unrelated
         * reason, and now fails all four.
         */
        var atPosition = new HashSet<(int, int)>(
            carJunctions.Select(nj => ((int)(nj.Position.X * 10), (int)(nj.Position.Z * 10))));

        foreach (var sp in city.Store.GetStreetPoints())
        {
            var key = ((int)((sp.Pos3.X + city.Cluster.Pos.X) * 10),
                       (int)((sp.Pos3.Z + city.Cluster.Pos.Z) * 10));

            Assert.True(atPosition.Contains(key),
                $"{idString}@{size}: junction {sp.Id} (level {sp.Level}) carries no car lane "
                + "at all, so nothing can drive to it");
        }

        var reached = new HashSet<NavJunction>();
        var queue = new Queue<NavJunction>();
        var seed = carJunctions.First();
        reached.Add(seed);
        queue.Enqueue(seed);

        while (queue.Count > 0)
        {
            var nj = queue.Dequeue();
            foreach (var nl in nj.StartingLanes)
            {
                if (!nl.AllowedTypes.HasFlag(TransportationType.Car)) continue;
                if (reached.Add(nl.End)) queue.Enqueue(nl.End);
            }
        }

        Assert.Equal(carJunctions.Count, reached.Count);
    }


    /**
     * A real A* reaches every deck, and the route CLIMBS to get there.
     *
     * From one junction of the city to each deck end, because a deck is where the car
     * network stops being the ground plan and a route onto it has to use a ramp.
     *
     * ⚠️ THREE WEAKER FORMS OF THIS WERE DRIVEN AND ALL THREE PASSED WITH EVERY STRUCTURE
     * STROKE DENIED A LANE. "A route exists" passes, because the cursor snaps the target
     * onto the nearest lane and the road under a deck is right there in plan. "The route's
     * highest junction is within a metre of the deck" passes, because a route across a
     * hillside city climbs higher than eight metres on its own. "Some lane of the route
     * ends at the deck's own plan position" fails on the unmutated code, because
     * TruncateAtTarget replaces the last lane of a route with a new one. What holds is
     * that some lane of the route LIES ON a ramp, deck or bore.
     *
     * ⚠️ AND IT IS NOT ALL OF THEM: 38 of the 40 deck ends over these four seeds. On
     * Yelukhdidru@3000 two decks are targeted onto the road underneath instead - the
     * cursor takes the nearest lane to a position and does not know that a bridge is not
     * the road below it. Found here, recorded rather than fixed: it is a property of
     * NavCluster.TryCreateCursor and belongs with whoever gives the satnav a notion of
     * level.
     */
    [Theory]
    [InlineData("Yelukhdidru", 800f, 4, 4)]
    [InlineData("seed000", 1500f, 6, 6)]
    [InlineData("seed017", 2400f, 10, 10)]
    [InlineData("Yelukhdidru", 3000f, 20, 18)]
    public async Task ADeckCanBeDrivenTo(
        string idString, float size, int expectedDecks, int expectedClimbing)
    {
        var city = _navCityOf(idString, size);

        var nc = new NavCluster { AABB = city.Cluster.AABB, Content = city.Content };
        city.Content.Cluster = nc;
        city.Content.Recompile();

        /*
         * The junctions that are genuinely in the air. !StandsOnTheGround also catches the
         * one ramp foot whose ordinary streets were all lifted away (see
         * GroundJunctionTests.ARampCanComeDownAtADeadEnd) - that one is AT ground level, so
         * a route to it does not have to climb anything and asserting that it does would be
         * asserting something untrue.
         */
        var decks = city.Store.GetStreetPoints()
            .Where(sp => !BlockGraph.StandsOnTheGround(sp) && 0 != sp.Level).ToList();

        var structures = city.Store.GetStrokes()
            .Where(st => StrokeKinds.IsStructure(st.Kind)).ToList();

        Assert.True(decks.Count > 0);

        var ground = city.Store.GetStreetPoints()
            .First(BlockGraph.StandsOnTheGround);

        /*
         * In world space, at the height the nav map put the junction: the ground under it
         * plus its deck elevation. Reaching for the plan position alone would compare the
         * route's height against zero and assert nothing at all.
         */
        float HeightOf(StreetPoint sp)
            => city.Cluster.StreetHeightSource.GroundHeightAt(sp) + sp.LevelElevation;

        Vector3 v3From = ground.Pos3 + city.Cluster.Pos
                         + new Vector3(0f, HeightOf(ground), 0f);

        int nRouted = 0, nClimbed = 0;
        foreach (var deck in decks)
        {
            float deckY = HeightOf(deck);
            Vector3 v3To = deck.Pos3 + city.Cluster.Pos + new Vector3(0f, deckY, 0f);

            /*
             * The A* itself rather than RoutePlan.PlanAsync, and deliberately: PlanAsync
             * ends with TruncateAtTarget, which cuts the route short where it passes
             * closest to the target and REPLACES the last lane with a new one - measured,
             * that removes the ramp from the route on one deck end of Yelukhdidru@3000, so
             * a gate over PlanAsync would be asserting something about the truncation. The
             * cursors and the pathfinder are what PlanAsync itself uses.
             */
            var cursorFrom = await nc.TryCreateCursor(v3From, TransportationType.Car);
            var cursorTo = await nc.TryCreateCursor(v3To, TransportationType.Car);

            var lanes = new LocalPathfinder(
                cursorFrom, cursorTo, TransportationType.Car).Pathfind();

            Assert.True(null != lanes && lanes.Count > 0,
                $"{idString}@{size}: no car route from {ground.Id} to deck end {deck.Id}");

            /*
             * ⚠️ AND IT HAS TO ARRIVE AT THE DECK ITSELF. TryCreateCursor snaps a position
             * onto the NEAREST lane, and a deck end stands in plan right over the road it
             * flies across - so a route that never touched a ramp still "reaches" it, on
             * the ground eight metres below, and PlanAsync still returns lanes.
             *
             * Two weaker forms of this assertion were driven and both passed with every
             * structure stroke denied a lane: "the route is non-empty", and "the route's
             * highest junction is within a metre of the deck" - the second because a route
             * across a hillside city climbs higher than eight metres on its own. So the
             * test is on IDENTITY: some lane of the route ends at the deck end's own plan
             * position, which no ground lane does.
             */
            if (lanes.Any(nl => structures.Any(st => _lies(nl, st, city)))) ++nClimbed;
            ++nRouted;
        }

        Assert.Equal(decks.Count, nRouted);
        Assert.Equal(expectedDecks, decks.Count);
        Assert.Equal(expectedClimbing, nClimbed);
    }


    /**
     * Whether a nav lane runs along one particular stroke, in plan.
     *
     * A lane subdivided at MaxLaneLength lies inside its stroke's segment like any other
     * piece of it, so both endpoints being within half a metre of the segment is the test.
     */
    private static bool _lies(NavLane nl, Stroke st, NavCity city)
        => _lies(nl.Start.Position, st, city) && _lies(nl.End.Position, st, city);


    private static bool _lies(in Vector3 p3, Stroke st, NavCity city)
    {
        Vector2 d = new Vector2(p3.X - city.Cluster.Pos.X, p3.Z - city.Cluster.Pos.Z)
                    - st.A.Pos;

        return Single.Abs(Vector2.Dot(d, st.Normal)) < 0.5f
               && Vector2.Dot(d, st.Unit) > -0.5f
               && Vector2.Dot(d, st.Unit) < st.Length + 0.5f;
    }


    /*
     * ================================================== an existing save =============
     */

    /**
     * What an existing save game finds when the flag goes on, per seed:
     * junctions before, junctions after, ids that no longer exist at all, and ids that
     * still land on the same place.
     */
    public static IEnumerable<object[]> SaveGameSeeds => new List<object[]>
    {
        //                                 before  after  gone  unmoved
        new object[] { "seed000",     500f,    27,    29,    0,       5 },
        new object[] { "seed011",     500f,    23,    28,    0,       4 },
        new object[] { "Yelukhdidru", 400f,    12,    13,    0,       2 },
        new object[] { "Yelukhdidru", 800f,    64,    67,    0,      15 },
        new object[] { "seed000",     1500f,  274,   253,   21,       3 },
        new object[] { "seed017",     2400f,  785,   623,  162,       2 },
        new object[] { "Yelukhdidru", 3000f, 1379,   950,  429,       2 },
    };


    /**
     * ⚠️ AN EXISTING SAVE GAME RESOLVES THE SAME JUNCTION ID TO A DIFFERENT JUNCTION,
     * SILENTLY.
     *
     * A save stores a street point as (ClusterId, Id) and a stroke as (ClusterId, Sid);
     * StreetPointConverter and StrokeConverter look them up in the REGENERATED store with
     * StrokeStore.GetStreetPoint / GetStroke, which are FirstOrDefault. So an id that no
     * longer exists comes back as null with no error at all, and an id that does exist
     * comes back as whichever junction now happens to carry that number.
     *
     * It is not a near miss. The flag does not add bridges to the city that was there - the
     * heavy-first queue builds a different city (§13.2) - so on Yelukhdidru@3000 429 of
     * 1379 junction ids are simply gone, and of the 950 that resolve, TWO are in the same
     * place; the median resolves 1430.8 m away and the worst 3614.6 m.
     *
     * Nothing here fixes that. The version that would invalidate a save is
     * DBStorage.DbVersion, which also governs gamestate.db, so bumping it deletes every
     * player's save outright - a decision with a cost either way and one for the owner.
     * What this test does is make the size of it a number rather than an adjective.
     */
    [Theory]
    [MemberData(nameof(SaveGameSeeds))]
    public void AnExistingSaveResolvesTheSameIdToADifferentJunction(
        string idString, float size, int before, int after, int gone, int unmoved)
    {
        var off = StreetHarness.Generate(idString, size);
        var (on, _) = _terrain(idString, size);

        var offPoints = off.GetStreetPoints().ToDictionary(sp => sp.Id);
        var onPoints = on.GetStreetPoints().ToDictionary(sp => sp.Id);

        Assert.Equal(before, offPoints.Count);
        Assert.Equal(after, onPoints.Count);
        Assert.Equal(gone, offPoints.Keys.Count(k => !onPoints.ContainsKey(k)));

        var moved = offPoints.Keys.Where(onPoints.ContainsKey)
            .Select(k => (offPoints[k].Pos - onPoints[k].Pos).Length())
            .OrderBy(d => d).ToList();

        Assert.Equal(unmoved, moved.Count(d => d < 0.001f));

        /*
         * ...and the ones that do resolve are not near where they were. Without this the
         * counts above are satisfied by a city that barely moved.
         */
        Assert.True(moved[moved.Count / 2] > 100f,
            $"{idString}@{size}: the median surviving id moved {moved[moved.Count / 2]:F1} m");
    }


    /*
     * ================================================== plumbing =====================
     */

    private sealed class NavCity
    {
        internal ClusterDesc Cluster;
        internal StrokeStore Store;
        internal NavClusterContent Content;
    }


    private static readonly Dictionary<string, NavCity> _navCities = new();


    private static NavCity _navCityOf(string idString, float size)
    {
        lock (_lo)
        {
            string key = $"{idString}@{size}";
            if (_navCities.TryGetValue(key, out var cached)) return cached;

            var cd = StreetHarness.MakeCluster(idString, size);
            cd.AverageHeight = 20f;
            var (store, _) = StreetHarness.GenerateHeavyFirstReporting(
                idString, size,
                sp => ShippedTerrain.HeightAt(
                    cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y));
            cd.StreetHeightSource = ShippedTerrain.StreetHeightsOf(cd, store);

            var quarters = StreetHarness.GenerateQuarters(cd, store, idString);

            var city = new NavCity
            {
                Cluster = cd,
                Store = store,
                Content = GenerateNavMapOperator.ContentOf(
                    cd, store, quarters, new NavCluster())
            };

            _navCities[key] = city;
            return city;
        }
    }


    private static (StrokeStore, StructurePlacementReport) _terrain(
        string idString, float size)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        var (store, generator) = StreetHarness.GenerateHeavyFirstReporting(
            idString, size,
            sp => ShippedTerrain.HeightAt(cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y));

        return (store, generator.StructurePlacement);
    }


    private static Dictionary<int, List<(int To, float Cost)>> _graphOf(
        StrokeStore store, bool includeStructures)
    {
        var g = new Dictionary<int, List<(int, float)>>();
        foreach (var sp in store.GetStreetPoints()) g[sp.Id] = new List<(int, float)>();

        foreach (var s in store.GetStrokes())
        {
            if (!includeStructures && StrokeKinds.IsStructure(s.Kind)) continue;
            _join(g, s.A.Id, s.B.Id, s.Length);
        }

        return g;
    }


    private static void _join(
        Dictionary<int, List<(int To, float Cost)>> g, int a, int b, float cost)
    {
        g[a].Add((b, cost));
        g[b].Add((a, cost));
    }


    /**
     * Shortest path from one junction to a handful of others.
     */
    private static Dictionary<int, float> _dijkstra(
        Dictionary<int, List<(int To, float Cost)>> g, int from, List<int> wanted)
    {
        var dist = new Dictionary<int, float> { [from] = 0f };
        var todo = new PriorityQueue<int, float>();
        todo.Enqueue(from, 0f);
        var want = new HashSet<int>(wanted);
        want.Remove(from);

        while (todo.Count > 0 && want.Count > 0)
        {
            todo.TryDequeue(out int at, out float d);
            if (d > dist[at] + 1e-4f) continue;
            want.Remove(at);

            foreach (var (to, cost) in g[at])
            {
                float nd = d + cost;
                if (dist.TryGetValue(to, out float old) && old <= nd) continue;

                dist[to] = nd;
                todo.Enqueue(to, nd);
            }
        }

        return dist;
    }
}
