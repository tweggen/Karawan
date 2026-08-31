using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.world;
using JoyceCode.Tests.engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.world;


/**
 * Where a new game starts, and where the coins it is seeded with go.
 *
 * Three defects, of which only the first was reported.
 *
 * 1. nogame.world.DropCoinModule dropped 19 coins in a vertical column at hard coded
 *    world (164, 45..99, 137) - no cluster, no player, no terrain, no fragment. In the
 *    shipped world that is 102.05 m in plan from where the player appears and 35.76 m
 *    below the bottom of the fall.
 * 2. It could not have asked, either. Saver.CallOnCreateNewGame runs against a brand new
 *    GameState whose PlayerPosition is Vector3.Zero, and that zero is precisely what makes
 *    PlayerPosition.GetPlayerPosition resolve a start lazily, later. So the resolution
 *    moved into engine.world.PlayerStart, which both ask and which remembers its answer -
 *    they are placed at different times and the answer depends on which estates have been
 *    built on by then.
 * 3. ClusterDesc.FindStartPosition answered in cluster relative coordinates on the estate
 *    branch and in ABSOLUTE ones on the "no free estate" branch, and both call sites added
 *    the cluster's origin to whatever came back. So that fallback spawned the player at
 *    2 x cluster.Pos - measured over the shipped world's 70 cities, a median 36.6 km from
 *    the city it was meant to start in. It is a branch no generated baseline reaches,
 *    which is why nothing noticed.
 */
public class PlayerStartTests
{
    private static (ClusterDesc, QuarterStore) _city(string idString, float size, float aver)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = aver;
        var store = StreetHarness.Generate(idString, size);
        return (cd, StreetHarness.GenerateQuarters(cd, store, idString));
    }


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


    /**
     * The estate branch is unchanged, term for term.
     *
     * The shipped expression is written out here rather than referred to, because this is
     * the assertion that says the flat city's start does not move: the position is
     * (estate centre + offset) in the cluster's frame, lifted to the city's average plus
     * the drop, plus the cluster's origin.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheEstateBranchIsTheShippedArithmetic(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size, 37.76129f);
        cd.Pos = new Vector3(-5.7712326f, 0f, 10f);

        var estate = quarters.GetQuarters()
            .Where(q => !q.IsInvalid())
            .SelectMany(q => q.GetEstates())
            .FirstOrDefault(e => e.GetBuildings().Count == 0);

        if (null == estate)
        {
            /*
             * Not hypothetical: QuarterGenerator puts a building on an estate as it
             * traces it, and on seed000/500 it succeeds on all three - so the smallest
             * baseline city takes the FALLBACK branch on a freshly generated world, with
             * no fragment operator having run at all. Which estate is free is a property
             * of the generator, not of play; the other three cities each have one whose
             * footprint collapsed.
             */
            Assert.Equal(PlayerStart.AtClusterCentre(cd).V3World,
                PlayerStart.PoseIn(cd, quarters).V3World);
            return;
        }

        Vector3 centre = estate.GetCenter();

        var vOffset = new Vector3(0f, 0f, -3f);
        Vector3 v3ClusterStart = (centre + vOffset) with { Y = cd.AverageHeight + 100f };
        Vector3 v3Shipped = v3ClusterStart + cd.Pos;

        var pose = PlayerStart.PoseIn(cd, quarters);

        Assert.Equal(v3Shipped.X, pose.V3World.X);
        Assert.Equal(v3Shipped.Z, pose.V3World.Z);
        Assert.Equal(cd.AverageHeight + 100f, pose.V3World.Y);

        Vector3 vuZ = Vector3.Normalize(v3ClusterStart with { Y = 0f });
        Assert.Equal(
            Quaternion.CreateFromRotationMatrix(
                Matrix4x4.CreateWorld(Vector3.Zero, -vuZ, Vector3.UnitY)),
            pose.QOrientation);
    }


    /**
     * The city's own average decides the height, not the nominal Pos.Y it was laid out
     * with.
     *
     * GenerateClustersOperator draws every city but the start one a random Pos.Y between
     * 10 and 40 m and then ClusterBaseElevationOperator measures what the ground under it
     * actually is. Both call sites used to add the whole of Pos, Y included - invisible
     * for the player, whose start cluster has Pos.Y exactly 0, and 10 to 40 m out for
     * joyce.ui.Clusters, which beams to any city in the world.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheStartIgnoresTheCitysNominalHeight(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size, 12.5f);

        cd.Pos = new Vector3(-14000f, 0f, 21000f);
        var atZero = PlayerStart.PoseIn(cd, quarters);

        cd.Pos = new Vector3(-14000f, 38.69f, 21000f);
        var atHeight = PlayerStart.PoseIn(cd, quarters);

        Assert.Equal(atZero.V3World, atHeight.V3World);
        Assert.Equal(cd.AverageHeight + PlayerStart.DropHeight, atHeight.V3World.Y);
    }


    /**
     * A block the block tracer threw away is not somewhere to start.
     *
     * All 3 / 10 / 82 / 445 blocks of all four baselines come out valid, so no amount of
     * real data distinguishes a PoseIn that skips invalid ones from a PoseIn that does
     * not - this is the mutation that survived until the invalid block was made by hand.
     * It is the same shape as §7j's Fragment.PartitionContains survivor.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AnInvalidBlockIsNotAPlaceToStart(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size, 12.5f);

        /*
         * Put a free estate on the FIRST block and then throw that block away.
         */
        var first = quarters.GetQuarters().First();
        first.GetEstates().Clear();
        var free = new Estate() { ClusterDesc = cd, Quarter = first };
        free.AddPoints(new List<Vector3>
        {
            new(-9999f, 0f, -9999f), new(-9989f, 0f, -9999f), new(-9989f, 0f, -9989f)
        });
        first.AddEstate(free);

        Assert.Equal(free.GetCenter(),
            quarters.GetQuarters()
                .SelectMany(q => q.GetEstates())
                .First(e => e.GetBuildings().Count == 0)
                .GetCenter());

        first.SetInvalid(true);

        var pose = PlayerStart.PoseIn(cd, quarters);
        Assert.NotEqual(PlayerStart.OnEstate(cd, free.GetCenter()).V3World, pose.V3World);
    }


    /**
     * The fallback branch, driven rather than read.
     *
     * A real generated city with a building on every one of its estates - the state the
     * fallback exists for and that no baseline reaches on its own. The start then has to
     * be inside the city it belongs to, which is exactly what the double add broke.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ACityWithNoFreeEstateStartsInsideItself(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size, 12.5f);
        cd.Pos = new Vector3(-14000f, 0f, 21000f);

        int nEstates = 0;
        foreach (var q in quarters.GetQuarters())
        {
            foreach (var e in q.GetEstates())
            {
                e.AddBuilding(new Building() { ClusterDesc = cd });
                ++nEstates;
            }
        }

        Assert.True(nEstates > 0, "the city has no estates at all");

        var pose = PlayerStart.PoseIn(cd, quarters);

        Assert.Equal(PlayerStart.AtClusterCentre(cd).V3World, pose.V3World);
        Assert.True(cd.IsInside(pose.V3World with { Y = 0f }),
            $"the fallback start {pose.V3World} is outside {cd.Name} at {cd.Pos}");

        /*
         * And what it used to be: the same expression with the cluster's origin added a
         * second time by the caller.
         */
        Vector3 v3Doubled = ((cd.Pos + PlayerStart.Offset) with
        {
            Y = cd.AverageHeight + PlayerStart.DropHeight
        }) + cd.Pos;
        Assert.False(cd.IsInside(v3Doubled with { Y = 0f }),
            "the double added fallback was inside its own city, so this test proves nothing");
    }


    /**
     * Over the shipped world's own 70 cities.
     *
     * Measured 2026-08-31: the double added fallback landed outside its own city for 69
     * of the 70, a median 36.6 km away. The one exception is the START cluster, whose Pos
     * is 11.5 m from the origin - so a fixture built on a city near zero, which is what
     * the harness makes, would have shown nothing at all.
     */
    [Fact]
    public void TheFallbackUsedToLandOutsideNearlyEveryCity()
    {
        var clusters = IntercityWorldHarness.Clusters();
        Assert.True(clusters.Count > 20, $"only {clusters.Count} cities");

        int nOutsideNow = 0, nOutsideBefore = 0;
        var distances = new List<double>();
        foreach (var cd in clusters)
        {
            var now = PlayerStart.AtClusterCentre(cd).V3World;
            if (!cd.IsInside(now with { Y = 0f })) ++nOutsideNow;

            Vector3 before = ((cd.Pos + PlayerStart.Offset) with
            {
                Y = cd.AverageHeight + PlayerStart.DropHeight
            }) + cd.Pos;
            if (!cd.IsInside(before with { Y = 0f })) ++nOutsideBefore;

            distances.Add(new Vector2(before.X - cd.Pos.X, before.Z - cd.Pos.Z).Length());
        }

        Assert.Equal(0, nOutsideNow);
        Assert.True(nOutsideBefore >= clusters.Count - 1,
            $"only {nOutsideBefore} of {clusters.Count} double added fallbacks were outside their city");

        distances.Sort();
        Assert.True(distances[distances.Count / 2] > 1000.0,
            $"median displacement was only {distances[distances.Count / 2]:F0} m");
    }


    /**
     * The coins hang under the start, in the fall, and above the ground.
     *
     * The count and the spacing are the shipped ones - 19 coins, 3 m apart - so the only
     * thing that changed about the column is where it is. The whole 57 m of it has to fit
     * between the ground and the point the player appears at, or the player either lands
     * on the bottom of it or starts inside the top of it.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheCoinsHangUnderThePlayerInsideTheFall(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size, 37.76129f);
        var pose = PlayerStart.PoseIn(cd, quarters);

        var column = PlayerStart.StartingItemColumn(pose.V3World).ToList();

        Assert.Equal(19, column.Count);
        Assert.Equal(PlayerStart.NStartingItems, column.Count);

        foreach (var v3 in column)
        {
            Assert.Equal(pose.V3World.X, v3.X);
            Assert.Equal(pose.V3World.Z, v3.Z);
            Assert.True(v3.Y < pose.V3World.Y, $"{v3} is not below the start {pose.V3World}");
            Assert.True(v3.Y > cd.AverageHeight, $"{v3} is at or under the ground {cd.AverageHeight}");
        }

        for (int i = 1; i < column.Count; ++i)
        {
            Assert.Equal(PlayerStart.StartingItemSpacing, column[i - 1].Y - column[i].Y);
        }

        Assert.Equal(pose.V3World.Y - PlayerStart.StartingItemTopGap, column[0].Y);
    }


    /**
     * Both askers ask the same function, and get the same answer.
     *
     * PlayerStart.Find needs the container, so what is driven here is the property that
     * makes it worth having: the answer is resolved once. Two calls at two different
     * times - which is what the coin module and the hover module are - cannot diverge
     * because an estate was built on in between.
     */
    [Fact]
    public void TheStartIsResolvedOnce()
    {
        PlayerStart.Reset();

        var (cd, quarters) = _city("seed000", 1500f, 12.5f);
        var first = PlayerStart.PoseIn(cd, quarters);

        foreach (var q in quarters.GetQuarters())
        {
            foreach (var e in q.GetEstates())
            {
                e.AddBuilding(new Building() { ClusterDesc = cd });
            }
        }

        var second = PlayerStart.PoseIn(cd, quarters);
        Assert.NotEqual(first.V3World, second.V3World);

        /*
         * ... which is why Find remembers. Driven through the seam Find is built on,
         * because Find itself needs four container services.
         */
        PlayerStart.Reset();
        int nResolved = 0;
        var remembered = PlayerStart.Once(() => { ++nResolved; return first; });
        var again = PlayerStart.Once(() => { ++nResolved; return second; });

        Assert.Equal(1, nResolved);
        Assert.Equal(first.V3World, remembered.V3World);
        Assert.Equal(first.V3World, again.V3World);

        /*
         * A resolver with no world to answer from is not remembered.
         */
        PlayerStart.Reset();
        int nRefused = 0;
        Assert.Equal(PlayerStart.NoCluster.V3World,
            PlayerStart.Once(() => { ++nRefused; return null; }).V3World);
        Assert.Equal(first.V3World, PlayerStart.Once(() => { ++nRefused; return first; }).V3World);
        Assert.Equal(2, nRefused);

        PlayerStart.Reset();

        /*
         * And nothing else may resolve a start of its own.
         */
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        string nogame = Path.GetFullPath(Path.Combine(root, "..", "nogameCode", "nogame"));
        Assert.True(Directory.Exists(nogame), $"could not find nogameCode at {nogame}");

        int nAskers = 0;
        foreach (var f in Directory.GetFiles(nogame, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(f);

            /*
             * Absence as well as presence: resolving a start of one's own beside a call
             * to PlayerStart.Find compiles, runs, and passes everything above.
             * ClusterDesc.FindStartPose answers freshly every time and is only for the
             * one caller that remembers - and for joyce.ui.Clusters, which is a debug
             * beam to an arbitrary city and deliberately not the player's start.
             */
            Assert.DoesNotContain("FindStartPose", source);

            if (source.Contains("PlayerStart.Find()")) ++nAskers;
        }

        Assert.Equal(2, nAskers);
    }


    /**
     * The coin module places the column and nothing else.
     *
     * DropCoinModule is in nogameCode, so this is a scan - but of the kind §7m's
     * survivors argue for: the constants it used to carry must be GONE, not merely
     * accompanied by a call.
     */
    [Fact]
    public void TheCoinModuleHasNoPlaceOfItsOwn()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        string coins = Path.GetFullPath(Path.Combine(
            root, "..", "nogameCode", "nogame", "world", "DropCoinModule.cs"));
        Assert.True(File.Exists(coins), $"could not find DropCoinModule at {coins}");

        string source = File.ReadAllText(coins);

        Assert.Contains("PlayerStart.StartingItemColumn(pose.V3World)", source);
        Assert.Contains("PlayerStart.Find()", source);

        /*
         * The literal column. Checked on the code rather than the whole file, because the
         * comment above it quotes the old coordinates on purpose.
         */
        int idxBody = source.IndexOf("public Func<Task> WorldOperatorApply()", StringComparison.Ordinal);
        Assert.True(idxBody > 0, "could not find the operator body");
        string body = source.Substring(idxBody);

        Assert.DoesNotContain("164", body);
        Assert.DoesNotContain("137f", body);
        Assert.DoesNotContain("new Vector3(", body);
    }


    /**
     * Nobody adds the cluster's origin to a pose that already carries it.
     *
     * The two functions above are driven; these two lines are the ones that reach for
     * them, and neither is reachable from a test - ClusterDesc.FindStartPose triggers
     * street generation, and joyce.ui.Clusters needs ImGui and a booted engine.
     * joyce.ui.Clusters is the OTHER caller that used to add Pos, and being a debug beam
     * rather than the player's start is exactly why nothing would have noticed.
     */
    [Fact]
    public void TheTwoUnreachableCallSitesTakeThePoseAsItComes()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");

        string clusterDesc = File.ReadAllText(Path.Combine(root, "engine", "world", "ClusterDesc.cs"));
        Assert.Contains("return PlayerStart.PoseIn(this, _quarterStore);", clusterDesc);

        string beam = File.ReadAllText(Path.Combine(root, "ui", "Clusters.cs"));
        Assert.Contains("clusterDesc.FindStartPose()", beam);
        Assert.Contains("BeamTo(pose.V3World, pose.QOrientation)", beam);
        Assert.DoesNotContain("+ clusterDesc.Pos", beam);
    }
}
