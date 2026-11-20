using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.physics;
using engine.streets.generation;
using engine.world;
using JoyceCode.Tests.engine.streets;
using JoyceCode.Tests.engine.world;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.physics;


/**
 * The walking player stands on what is built under it - pavement, road, deck - and on the
 * terrain only over open ground, like the ship (KAR-472) and like walking NPCs, which stand
 * on the pavement (GoToStrategyPart).
 *
 * Reported from a playtest: the player on foot visibly floating above the pavement, "not
 * restricted to this single spot". WalkController clamped the walker up to the terrain-
 * derived walking height and nothing else, and - standing still - never lowered it at all.
 */
public class WalkingFloorTests
{
    private readonly ITestOutputHelper _out;
    public WalkingFloorTests(ITestOutputHelper output) => _out = output;


    private static string _repoRoot() =>
        Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));


    [Fact]
    public void TheFloorIsWhatIsBuilt_AboveOrBelowTheTerrain()
    {
        Assert.Equal(12.15f, HoverSurfaceProbe.WalkingFloorHeight(20f, 12.15f));   // in a cutting
        Assert.Equal(25f, HoverSurfaceProbe.WalkingFloorHeight(20f, 25f));         // on fill or a deck
        Assert.Equal(20f, HoverSurfaceProbe.WalkingFloorHeight(20f, null));        // open ground
    }


    /**
     * The flat city: the block floor's top - the pavement - is exactly the old walking
     * height, and the fragment floor plane under its roads is the road as drawn, so the
     * walker keeps its height on a pavement and stands on, not a kerb above, a road.
     */
    [Theory]
    [InlineData(0f)]
    [InlineData(37.5f)]
    public void AFlatCityPavementIsTheOldWalkingHeight(float averageHeight)
    {
        float oldWalking = averageHeight + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset;
        float pavementTop = averageHeight + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE + MetaGen.QuarterSidewalkOffset;
        float floorPlaneTop = averageHeight + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;

        Assert.Equal(oldWalking, HoverSurfaceProbe.WalkingFloorHeight(oldWalking, pavementTop));
        Assert.Equal(oldWalking - MetaGen.QuarterSidewalkOffset,
            HoverSurfaceProbe.WalkingFloorHeight(oldWalking, floorPlaneTop));
    }


    /**
     * Why: over eight shipped terrain cities, at the middle of every pavement, the terrain-
     * derived walking height against the pavement as drawn (BuildingFooting.PavementHeightAt,
     * the top of the block floor's collider). Measured 2026-11-20: 25 416 points, median
     * 0.00, p10 -2.51, p90 +2.53, worst +10.56 m; above by more than 0.3 m at 42.7 %, by
     * more than 1 m at 28.1 %.
     */
    [Fact]
    public void TheTerrainIsNotThePavement_OverTerrainCities()
    {
        var off = new List<float>();
        foreach (var cd in IntercityWorldHarness.Clusters().Take(8))
        {
            var (store, _) = StreetHarness.GenerateHeavyFirstReporting(cd.IdString, cd.Size,
                sp => ShippedTerrain.HeightAt(cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y));
            cd.StreetHeightSource = ShippedTerrain.StreetHeightsOf(cd, store);
            var ground = ShippedTerrain.ConformedOf(cd, store);
            var quarters = StreetHarness.GenerateQuarters(cd, store, cd.IdString);

            foreach (var q in quarters.GetQuarters())
            {
                var delims = q.GetDelims();
                if (delims.Count < 3) continue;
                var centre = q.GetCenterPoint();
                for (int i = 0; i < delims.Count; i++)
                {
                    Vector2 mid = (delims[i].StartPoint + delims[(i + 1) % delims.Count].StartPoint) / 2f;
                    Vector2 p = mid + Vector2.Normalize(centre - mid) * (q.SidewalkWidth / 2f);
                    float drawn = BuildingFooting.PavementHeightAt(q, p);
                    float terrainWalking = ground.HeightAt(cd.Pos.X + p.X, cd.Pos.Z + p.Y)
                                           + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset;
                    off.Add(terrainWalking - drawn);

                    // With the pavement's collider under it, the floor is the pavement.
                    Assert.Equal(drawn, HoverSurfaceProbe.WalkingFloorHeight(terrainWalking, drawn));
                }
            }
        }

        off.Sort();
        float P(double q) => off[(int)Math.Min(off.Count - 1, q * off.Count)];
        float above = off.Count(x => x > 0.3f) / (float)off.Count;
        _out.WriteLine($"terrain walking height minus drawn pavement: n={off.Count} p10 {P(.1):F2} p50 {P(.5):F2} " +
                       $"p90 {P(.9):F2} max {off[^1]:F2} m; above by > 0.3 m at {above:P1}");

        Assert.True(off.Count > 10000, $"only {off.Count} pavement points");
        Assert.True(above > 0.2f, "the terrain no longer stands above the pavement - this test has stopped testing");
    }


    /**
     * The walker itself needs a booted engine; what has to hold is scanned. It asks the
     * probe, takes the floor from WalkingFloorHeight, and a grounded walker standing above
     * the floor steps down to it - which nothing else does while it stands still.
     */
    [Fact]
    public void TheWalkerStandsOnTheProbedFloor()
    {
        string walk = File.ReadAllText(Path.Combine(_repoRoot(), "nogameCode/nogame/modules/playerhover/WalkController.cs"));

        Assert.Contains("HoverSurfaceProbe.SurfaceBelow(", walk);
        Assert.Contains("HoverSurfaceProbe.WalkingFloorHeight(terrainWalkingHeight, surfaceBelow)", walk);
        Assert.DoesNotContain("float heightAtTarget = I.Get<engine.world.MetaGen>().Loader.GetWalkingHeightAt(", walk);
        Assert.Contains("else if (surfaceBelow != null && JumpState.Grounded == _jumpState", walk);
        Assert.Contains("vNewTargetPos.Y = Single.Max(heightAtTarget, vNewTargetPos.Y - 20f * 1f / 60f);", walk);
    }
}
