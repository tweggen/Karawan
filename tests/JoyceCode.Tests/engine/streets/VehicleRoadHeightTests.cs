using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using engine.world;
using JoyceCode.Tests.engine.world;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.streets;


/**
 * KAR-470: an NPC car rides the drawn road, like the player, rather than the terrain.
 *
 * StreetNavigationController placed every car at the conformed terrain + 3 m. The player
 * rides the road's colliders 1 m above the drawn road, i.e. road ground + 3 m - the same
 * number only where terrain and road agree: everywhere in a flat city, nowhere exactly in
 * a terrain-following one, and not at all on a ramp or a deck, which stand LevelElevation
 * above the ground. The controller now asks RoadSurface.HeightUnderVehicle; this pins that
 * helper against the drawn road and writes down what the old height got wrong.
 */
public class VehicleRoadHeightTests
{
    private readonly ITestOutputHelper _out;
    public VehicleRoadHeightTests(ITestOutputHelper output) => _out = output;

    private static float Clearance => MetaGen.ClusterNavigationHeight - MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;


    private static float P(List<float> v, double q) => v[(int)Math.Min(v.Count - 1, q * v.Count)];


    [Fact]
    public void TheClearanceIsThePlayersSoBothRideTheSameSurface()
    {
        Assert.Equal(global::engine.physics.HoverSurfaceProbe.SurfaceClearance, Clearance);
    }


    [Fact]
    public void ACarRidesTheDrawnRoad_NotTheTerrain_OverTerrainCities()
    {
        var off = new Dictionary<string, List<float>> { ["street"] = new(), ["ramp"] = new(), ["deck"] = new() };
        int onRoad = 0, overCaps = 0, ontoNext = 0;

        foreach (var cd in IntercityWorldHarness.Clusters().Take(8))
        {
            var (store, _) = StreetHarness.GenerateHeavyFirstReporting(cd.IdString, cd.Size,
                sp => ShippedTerrain.HeightAt(cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y));
            var heights = ShippedTerrain.StreetHeightsOf(cd, store);
            cd.StreetHeightSource = heights;
            var ground = ShippedTerrain.ConformedOf(cd, store);
            var strokes = store.GetStrokes().ToList();
            var surfaces = strokes.ToDictionary(s => s, s => RoadSurface.OfStroke(s, heights, Vector2.Zero));

            foreach (var s in strokes)
            {
                if (surfaces[s] is not { } road) continue;
                string kind = s.Kind == StrokeKind.Ramp ? "ramp"
                    : StrokeKinds.IsStructure(s.Kind) ? "deck" : "street";

                // Points in the right-hand lane of the carriageway.
                float lane = s.StreetWidth() / 2f - 2f;
                Vector2 normal = new(-s.Unit.Y, s.Unit.X);
                Assert.True(RoadSurface.TryCornersOf(s, out var al, out var ar, out var bl, out var br, out _));
                float d0 = MathF.Max(road.AxialAt(al), road.AxialAt(ar)), d1 = MathF.Min(road.AxialAt(bl), road.AxialAt(br));
                if (d1 - d0 < 2f) continue;
                for (int i = 1; i < 8; ++i)
                {
                    Vector2 p = s.A.Pos + s.Unit * Single.Lerp(d0, d1, i / 8f) + normal * lane;
                    if (!road.IsOnCarriageway(p)) continue;

                    float drawn = road.SurfaceHeightAt(p);
                    float car = RoadSurface.HeightUnderVehicle(road, null, p);
                    Assert.Equal(drawn, car);
                    ++onRoad;

                    float oldCar = ground.HeightAt(cd.Pos.X + p.X, cd.Pos.Z + p.Y) + MetaGen.ClusterNavigationHeight;
                    off[kind].Add(oldCar - (drawn + Clearance));
                }

                // Across the B junction onto every other stroke leaving it.
                var j = s.B;
                float hJ = RoadSurface.HeightAtJunction(heights, j);
                var section = j.GetSectionArray();
                if (section.Count >= 3)
                {
                    Vector2 centre = section.Aggregate(Vector2.Zero, (a, b) => a + b) / section.Count;
                    foreach (var next in strokes.Where(t => t != s && (t.A == j || t.B == j)))
                    {
                        if (surfaces[next] is not { } nextRoad) continue;
                        Assert.Equal(hJ, RoadSurface.HeightUnderVehicle(road, nextRoad, centre), 3);
                        ++overCaps;

                        var far = next.A == j ? next.B : next.A;
                        Vector2 onNext = Vector2.Lerp(j.Pos, far.Pos, 0.5f);
                        if (!nextRoad.IsOnCarriageway(onNext) || road.IsOnCarriageway(onNext)) continue;
                        Assert.Equal(nextRoad.SurfaceHeightAt(onNext), RoadSurface.HeightUnderVehicle(road, nextRoad, onNext));
                        ++ontoNext;
                    }
                }
            }
        }

        foreach (var (kind, v) in off)
        {
            if (v.Count == 0) continue;
            var abs = v.Select(MathF.Abs).OrderBy(x => x).ToList();
            var signed = v.OrderBy(x => x).ToList();
            _out.WriteLine($"old terrain height vs drawn road, {kind}: n={v.Count} |off| p50 {P(abs, 0.5):F2} p90 {P(abs, 0.9):F2} " +
                           $"p99 {P(abs, 0.99):F2} max {abs[^1]:F2} m; signed min {signed[0]:F2} max {signed[^1]:F2}");
        }
        _out.WriteLine($"checked {onRoad} lane points, {overCaps} cap crossings, {ontoNext} points onto the next stroke");

        Assert.True(onRoad > 10000 && overCaps > 1000 && ontoNext > 1000, $"{onRoad}/{overCaps}/{ontoNext}");
        Assert.NotEmpty(off["ramp"]);
        // The old height had a ramp's car metres away from its road; a deck's even more.
        Assert.True(off["ramp"].Select(MathF.Abs).Max() > 3f);
    }
}
