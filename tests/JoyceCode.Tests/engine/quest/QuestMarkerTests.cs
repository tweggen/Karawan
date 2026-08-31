using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using engine.quest;
using engine.streets;
using engine.streets.generation;
using engine.world;
using JoyceCode.Tests.engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.quest;


/**
 * The quest marker's visible bottom is at or above the surface it stands on.
 *
 * Reported from play of a terrain-following city: the goal cube's lower part is under the
 * road. Two independent causes, and neither is sufficient on its own:
 *
 *   1. the cube was drawn CENTRED on the goal's position, so its bottom was always 1.5 m
 *      below whatever height the quest had chosen; and
 *   2. that height was the TERRAIN plus ClusterNavigationHeight - the vehicle hover
 *      reference - and in a city that keeps its terrain the terrain is not the road.
 *
 * The flat city hid both. ClusterBaseElevationOperator writes the ground at the city
 * average plus 1.5, a constant unrelated to CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE = 2.0, so
 * the cube's bottom landed exactly one metre over the road and looked deliberate.
 *
 * Measured here rather than asserted: over every junction of the four baseline cities on
 * the shipped terrain, with the conforming pass reproduced on its own 20 m grid.
 */
public class QuestMarkerTests
{
    public static IEnumerable<object[]> Cities()
    {
        foreach (var (idString, size) in new[]
                 {
                     ("seed000", 500f), ("Yelukhdidru", 800f),
                     ("seed000", 1500f), ("Yelukhdidru", 3000f)
                 })
        {
            yield return new object[] { idString, size };
        }
    }


    private static (ClusterDesc, StrokeStore, QuarterStore) _city(
        string idString, float size, bool isFlat)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;

        var store = StreetHarness.Generate(idString, size);

        cd.StreetHeightSource = isFlat
            ? new FlatStreetHeight(cd)
            : ShippedTerrain.StreetHeightsOf(cd, store);

        return (cd, store, StreetHarness.GenerateQuarters(cd, store, idString));
    }


    /**
     * Where engine.Placer puts a quest destination: exactly on a junction.
     *
     * Reference.StreetPoint adds sp.Pos3 with Y = sp.LevelElevation to the cluster origin
     * and nothing else, so this is not an approximation of the placement rule, it is the
     * rule. It matters because the surface query is exact AT a junction and an
     * extrapolation away from one.
     */
    private static Vector3 _destinationAt(ClusterDesc cd, StreetPoint sp)
        => cd.Pos + (sp.Pos3 with { Y = sp.LevelElevation });


    /**
     * THE GUARANTEE. At every junction of every baseline city, on the shipped terrain, the
     * marker's visible bottom is at or above the road, the junction cap and the pavement
     * of every block that corners there.
     *
     * Stated against three surfaces rather than one because a junction carries three: the
     * carriageway the deck is sheared onto, the flat cap that fills the area between the
     * branches, and the pavements of the blocks whose corners stand on it - which are the
     * HIGHEST of the three, one kerb above the other two, and therefore the one that
     * actually decides this.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheMarkersBottomIsNeverBelowTheSurfaceAtItsOwnPosition(
        string idString, float size)
    {
        var (cd, store, quarters) = _city(idString, size, false);

        var junctions = store.GetStreetPoints();
        int nChecked = 0, nCorners = 0;

        foreach (var sp in junctions)
        {
            Vector3 v3Target = _destinationAt(cd, sp);

            Assert.True(CitySurface.TryHeightAt(
                cd.StreetHeightSource, junctions,
                new Vector2(v3Target.X - cd.Pos.X, v3Target.Z - cd.Pos.Z),
                out float anchor, out float distance));

            float bottom = QuestMarker.BottomOf(anchor);

            float road = cd.StreetHeightSource.GroundHeightAt(sp)
                         + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE + sp.LevelElevation;

            Assert.True(bottom >= road - 1e-3f,
                $"{idString}/{size}: the marker at junction {sp.Id} has its bottom "
                + $"{road - bottom:F3} m under the carriageway");

            ++nChecked;

            /*
             * ...and under every pavement that corners on this junction. Identity, not
             * proximity: a delimiter's own StreetPoint IS the junction its corner stands on
             * since §7i, and a neighbouring junction can be nearer than a corner's own.
             */
            foreach (var q in quarters.GetQuarters())
            {
                foreach (var delim in q.GetDelims())
                {
                    if (!ReferenceEquals(delim.StreetPoint, sp)) continue;

                    float pavement = q.CornerGroundHeightAt(delim)
                                     + MetaGen.ClusterStreetHeight
                                     + MetaGen.QuarterSidewalkOffset;

                    Assert.True(bottom >= pavement - 1e-3f,
                        $"{idString}/{size}: the marker at junction {sp.Id} has its bottom "
                        + $"{pavement - bottom:F3} m under the pavement of the block at "
                        + $"{q.GetCenterPoint()} that corners on it");

                    ++nCorners;
                }
            }

            Assert.Equal(0f, distance, 3);
        }

        Assert.True(nChecked > 20, $"only {nChecked} junctions of {idString}/{size}");
        Assert.True(nCorners > 4,
            $"only {nCorners} block corners stood on a junction of {idString}/{size}, so "
            + "the pavement half of this proves nothing");
    }


    /**
     * The surface query answers from the junction it is standing on, by identity.
     *
     * Distance is no use here for the reason this whole work stream keeps rediscovering:
     * two junctions of a city can be 25 m apart while a block corner sits 25.7 m from its
     * own. The claim is that a destination placed at a junction gets THAT junction.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheSurfaceAtAJunctionIsThatJunctionsOwn(string idString, float size)
    {
        var (cd, store, _) = _city(idString, size, false);
        var junctions = store.GetStreetPoints();

        foreach (var sp in junctions)
        {
            Assert.Same(sp, CitySurface.NearestJunctionTo(junctions, sp.Pos));

            Assert.Equal(
                CitySurface.HeightAtJunction(cd.StreetHeightSource, sp),
                CitySurface.NearestJunctionTo(junctions, sp.Pos) is var found
                    ? CitySurface.HeightAtJunction(cd.StreetHeightSource, found)
                    : Single.NaN);
        }

        Assert.True(junctions.Count > 20);
    }


    /**
     * Neither half of the fix would have done on its own, and this is the measurement that
     * says so.
     *
     * (C) in the ledger - resting the cube on the position it already had - was called the
     * cheapest option. It leaves the bottom at terrain + ClusterNavigationHeight, which is
     * still below the pavement at a fifth of the junctions of a hillside city and 8.3 m
     * below it at the worst. (A) - routing through GetNavigationHeightAt - is the same
     * quantity again, minus the flat city's 1.5 m bias, so it changes nothing on a slope at
     * all.
     *
     * Recorded as a test rather than as a note because it is also what proves this file can
     * tell the two apart: if the terrain and the built surface agreed, everything above
     * would pass for the wrong reason.
     */
    [Fact]
    public void TheTerrainWouldNotHaveDone()
    {
        int nCitiesWhereRestingAloneFails = 0;

        foreach (var row in Cities())
        {
            string idString = (string)row[0];
            float size = (float)row[1];

            var (cd, store, _) = _city(idString, size, false);
            var conformed = ShippedTerrain.ConformedOf(cd, store);
            var junctions = store.GetStreetPoints();

            var wasBelow = new List<float>();
            float worstRested = Single.MaxValue;

            foreach (var sp in junctions)
            {
                float pavement = CitySurface.HeightAtJunction(cd.StreetHeightSource, sp);
                float terrain = conformed.HeightAt(cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y);

                /*
                 * The expression that shipped - the cube CENTRED on
                 * terrain + ClusterNavigationHeight - and option (C), the same cube RESTING
                 * on that same anchor.
                 */
                wasBelow.Add(terrain + MetaGen.ClusterNavigationHeight
                             - QuestMarker.Height / 2f - pavement);

                worstRested = Single.Min(
                    worstRested, terrain + MetaGen.ClusterNavigationHeight - pavement);
            }

            Assert.True(BlockFloor.Percentile(wasBelow, 0.5f) < -0.1f,
                $"{idString}/{size}: the marker that shipped was not under the pavement at "
                + "the median junction, so this file cannot distinguish the terrain from "
                + "the road and everything else it asserts is vacuous");

            if (worstRested < 0f) ++nCitiesWhereRestingAloneFails;
        }

        Assert.True(nCitiesWhereRestingAloneFails >= 3,
            $"resting the shipped cube on its own anchor was enough in all but "
            + $"{nCitiesWhereRestingAloneFails} of the baseline cities, which would "
            + "contradict the reason the anchor was changed as well as the offset. It is "
            + "enough in seed000/500, which has 27 junctions and barely any relief; it is "
            + "not in the other three, where the worst junction leaves the bottom several "
            + "metres under the pavement.");
    }


    /**
     * The default FLAT city, exactly - and it MOVES.
     *
     * The marker's bottom was at the average plus 3.0: the flattening operator writes the
     * ground at average + 1.5, the quest added ClusterNavigationHeight = 3, and the cube
     * hung 1.5 m below that. It is now at average + 2.15, resting on the pavement rather
     * than hovering a metre over the road. **Every quest marker in the shipped flat game
     * drops by 0.85 m**, and this is where that number is stated rather than discovered.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AFlatCityDropsEveryMarkerByFourFifthsOfAMetre(string idString, float size)
    {
        var (cd, store, _) = _city(idString, size, true);
        var junctions = store.GetStreetPoints();

        /*
         * ClusterBaseElevationOperator writes aver + 1.5 into the elevation grid inside a
         * cluster, which is what Loader.GetHeightAt answered for the old expression. Named
         * here because it is the whole reason the flat city looked right.
         */
        const float flatteningBias = 1.5f;

        foreach (var sp in junctions)
        {
            Assert.True(CitySurface.TryHeightAt(
                cd.StreetHeightSource, junctions, sp.Pos, out float anchor, out _));

            Assert.Equal(
                cd.AverageHeight
                + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset,
                anchor);

            float wasBottom = cd.AverageHeight + flatteningBias
                              + MetaGen.ClusterNavigationHeight - QuestMarker.Height / 2f;

            Assert.Equal(0.85f, wasBottom - QuestMarker.BottomOf(anchor), 4);
        }

        Assert.True(junctions.Count > 20);
    }


    /**
     * The cube rests on the goal rather than straddling it, and ToSomewhere is what does it.
     *
     * A source scan, because _createTargetInstance is private, needs a booted engine, a
     * container and a physics world, and runs inside a queued main thread action - the same
     * reason RouteRibbon's own call site is scanned rather than called. Absence as well as
     * presence: a second, correct offset elsewhere would satisfy any test of the value,
     * and what has to hold is that the marker's height and its offset are ONE pair.
     */
    [Fact]
    public void OnlyOnePlaceDecidesWhereTheMarkerCubeSits()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        string path = Path.Combine(root, "engine", "quest", "ToSomewhere.cs");
        Assert.True(File.Exists(path), $"could not find ToSomewhere at {path}");

        string source = File.ReadAllText(path);

        Assert.Contains("QuestMarker.RestOffset", source);
        Assert.Contains("QuestMarker.ScaleFor(SensitiveRadius)", source);
        Assert.DoesNotContain("new Vector3(SensitiveRadius, 3f, SensitiveRadius)", source);

        Assert.Equal(QuestMarker.Height / 2f, QuestMarker.RestOffset.Y);
        Assert.Equal(0f, QuestMarker.BottomOf(0f));
    }


    /**
     * The three quest strategies ask for the city's surface, not for the terrain.
     *
     * They live in nogameCode, which this assembly does not reference, so a scan is the
     * only instrument. Absence as well as presence, for the same reason as above: a fourth
     * quest that kept GetHeightAt would put its marker back in the road while every test of
     * the other three stayed green.
     */
    [Fact]
    public void EveryQuestMarkerAsksForTheCitySurface()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        string quests = Path.GetFullPath(Path.Combine(
            root, "..", "nogameCode", "nogame", "quests"));
        Assert.True(Directory.Exists(quests), $"could not find the quests at {quests}");

        int nSites = 0;

        foreach (var f in Directory.GetFiles(quests, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(f);
            if (!source.Contains("RelativePosition")) continue;

            Assert.DoesNotContain("Loader.GetHeightAt", source);

            if (source.Contains("GetCitySurfaceHeightAt")) ++nSites;
        }

        Assert.True(nSites >= 3,
            $"only {nSites} quest markers ask for the city surface; there were three");
    }
}
