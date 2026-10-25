using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using JoyceCode.Tests.engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * The collider under a stroke's carriageway (KAR-465): a prism over the four corners the
 * road mesh is built on, at the two junction heights the mesh gives them.
 *
 * It replaced a box from junction centre to junction centre. That box parted from the
 * drawn road inside every junction by the cap's reach times the grade, and on a branch
 * falling away from a junction it left the flat cap slab standing out of the road where
 * the carriageway leaves it - an edge a ship climbing into the junction drove into. The
 * Bepu call needs a live simulation and is not covered; the arithmetic, which is where
 * that edge came from, is.
 */
public class CarriagewayColliderTests
{
    private const float Thickness = 0.1f;


    private static IEnumerable<Vector3> Tops(CarriagewayCollider c) =>
        c.Slices.SelectMany(sl => sl.Where((_, k) => k % 2 == 0));


    [Fact]
    public void AFlatCarriagewayIsOnePrism_TopFaceOnTheRoad()
    {
        var c = CarriagewayCollider.For(Vector2.Zero, Vector2.UnitX,
            new Vector2(0, -6), new Vector2(0, 6), new Vector2(100, -6), new Vector2(100, 6),
            new Vector3(1000, 0, 2000), 20f, 20f, Thickness);

        Assert.Single(c.Slices);
        Assert.All(Tops(c), p => Assert.Equal(20f, p.Y, 4));
        for (int k = 0; k < c.Slices[0].Count; k += 2)
            Assert.Equal(c.Slices[0][k].Y - Thickness, c.Slices[0][k + 1].Y, 4);
        Assert.Contains(Tops(c), p => p == new Vector3(1000, 20, 1994));
    }


    [Fact]
    public void AnEvenRampIsOnePrismBetweenItsCornerCuts()
    {
        // Both kerbs 80 m long, rising 8 m alike: the drawn road is a plane.
        var c = CarriagewayCollider.For(Vector2.Zero, Vector2.UnitX,
            new Vector2(0, -6), new Vector2(5, 6), new Vector2(80, -6), new Vector2(85, 6),
            Vector3.Zero, 8f, 16f, Thickness);
        Assert.Equal(3, c.Slices.Count);     // the two end wedges and the run between
    }


    [Fact]
    public void ARampHoldsEachEndAtItsJunctionsHeight_AndRisesAlongEachKerb()
    {
        var c = CarriagewayCollider.For(Vector2.Zero, Vector2.UnitX,
            new Vector2(10, -6), new Vector2(12, 6), new Vector2(90, -6), new Vector2(95, 6),
            Vector3.Zero, 8f, 16f, Thickness);

        var tops = Tops(c).ToList();
        bool At(Vector3 p, float x, float z, float y) =>
            MathF.Abs(p.X - x) < 1e-3f && MathF.Abs(p.Z - z) < 1e-3f && MathF.Abs(p.Y - y) < 1e-4f;
        Assert.Contains(tops, p => At(p, 10, -6, 8f));
        Assert.Contains(tops, p => At(p, 12, 6, 8f));
        Assert.Contains(tops, p => At(p, 90, -6, 16f));
        Assert.Contains(tops, p => At(p, 95, 6, 16f));
        // Every point on a kerb has the height of that kerb's own linear rise.
        Assert.All(tops.Where(p => MathF.Abs(p.Z + 6) < 1e-3f),
            p => Assert.Equal(8f + 8f * (p.X - 10f) / 80f, p.Y, 3));
        Assert.All(tops.Where(p => MathF.Abs(p.Z - 6) < 1e-3f),
            p => Assert.Equal(8f + 8f * (p.X - 12f) / 83f, p.Y, 3));
        Assert.True(tops.Count(p => MathF.Abs(p.Z + 6) < 1e-3f) > 4,
            "kerbs of different length climb at different rates, so the twisted road is cut into slices");
        Assert.All(tops, p => Assert.InRange(p.Y, 8f - 1e-4f, 16f + 1e-4f));
    }


    [Fact]
    public void ADegenerateCarriagewayIsRefused()
    {
        var p = new Vector2(5, 5);
        Assert.False(CarriagewayCollider.For(Vector2.Zero, Vector2.UnitX, p, p, p, p, Vector3.Zero, 0f, 0f, Thickness).IsUsable);
        Assert.False(CarriagewayCollider.For(Vector2.Zero, Vector2.UnitX,
            new Vector2(0, 0), new Vector2(0, 0.001f), new Vector2(0, 0.002f), new Vector2(0, 0.003f),
            Vector3.Zero, 0f, 1f, Thickness).IsUsable);
    }


    /*
     * The top of a convex hull at a plan point: the highest plane through any three of
     * its points whose plan triangle contains the point (null when it covers none).
     */
    private static float? _hullTopAt(IReadOnlyList<Vector3> slice, Vector2 p)
    {
        var tops = slice.Where((_, k) => k % 2 == 0).ToList();
        float? best = null;
        for (int a = 0; a < tops.Count; ++a)
        for (int b = a + 1; b < tops.Count; ++b)
        for (int c = b + 1; c < tops.Count; ++c)
        {
            Vector3 A = tops[a], B = tops[b], C = tops[c];
            float d = (B.Z - C.Z) * (A.X - C.X) + (C.X - B.X) * (A.Z - C.Z);
            if (MathF.Abs(d) < 1e-6f) continue;
            float w1 = ((B.Z - C.Z) * (p.X - C.X) + (C.X - B.X) * (p.Y - C.Z)) / d;
            float w2 = ((C.Z - A.Z) * (p.X - C.X) + (A.X - C.X) * (p.Y - C.Z)) / d;
            float w3 = 1 - w1 - w2;
            if (w1 < -1e-4f || w2 < -1e-4f || w3 < -1e-4f) continue;
            float h = w1 * A.Y + w2 * B.Y + w3 * C.Y;
            best = best == null ? h : MathF.Max(best.Value, h);
        }
        return best;
    }


    /**
     * The reported defect, over real terrain cities with grade separation on: wherever a
     * junction has a cap slab, every branch's collider starts at the cap's own height,
     * on the cap's own section chord - so there is no edge between them to drive into -
     * and over the carriageway it never lies below the drawn road.
     *
     * Before the fix the same cities had 2 812 street ends and 48 ramp feet with an edge
     * of over a metre between the slab and the box, the worst 2.3 m.
     */
    [Fact]
    public void EveryBranchMeetsItsJunctionsSlabWithoutAnEdge_OverTerrainCities()
    {
        int seams = 0, structures = 0;
        float worstStep = 0f, worstBelowRoad = 0f;
        var aboveRoad = new List<float>();

        foreach (var cd in IntercityWorldHarness.Clusters().Take(8))
        {
            var (store, _) = StreetHarness.GenerateHeavyFirstReporting(cd.IdString, cd.Size,
                sp => ShippedTerrain.HeightAt(cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y));
            var heights = ShippedTerrain.StreetHeightsOf(cd, store);
            var strokes = store.GetStrokes().ToList();
            structures += strokes.Count(s => StrokeKinds.IsStructure(s.Kind));

            foreach (var s in strokes)
            {
                if (!DeckCollider.IsNeededFor(s, groundIsFlat: false)) continue;
                Assert.True(RoadSurface.TryCornersOf(s, out var al, out var ar, out var bl, out var br, out _));
                float hA = RoadSurface.HeightAtJunction(heights, s.A);
                float hB = RoadSurface.HeightAtJunction(heights, s.B);
                var c = CarriagewayCollider.For(s.A.Pos, s.Unit, al, ar, bl, br, Vector3.Zero, hA, hB, Thickness);
                if (!c.IsUsable) continue;

                // Against each end's cap slab, where the junction has one.
                var tops = Tops(c).ToList();
                foreach (var (j, corner) in new[] { (s.A, al), (s.A, ar), (s.B, bl), (s.B, br) })
                {
                    if (!JunctionCollider.IsNeededFor(j, groundIsFlat: false)) continue;
                    float capHeight = RoadSurface.HeightAtJunction(heights, j);
                    var cap = JunctionCollider.For(j.GetSectionArray(), Vector3.Zero, capHeight, Thickness);
                    Assert.Contains(cap.Points, q => Vector2.Distance(new Vector2(q.X, q.Z), corner) < 1e-3f);
                    var mine = tops.Where(q => Vector2.Distance(new Vector2(q.X, q.Z), corner) < 1e-3f).ToList();
                    Assert.NotEmpty(mine);
                    foreach (var q in mine) worstStep = MathF.Max(worstStep, MathF.Abs(capHeight - q.Y));
                    ++seams;
                }

                // Over the carriageway, against the surface the mesh draws: its vertices lie
                // on the two kerbs, each rising linearly from its A to its B corner, and its
                // rows join a left and a right kerb point at the same axial position.
                Vector2 u = s.Unit;
                float Ax(Vector2 v) => Vector2.Dot(v - s.A.Pos, u);
                (Vector2 pos, float h) OnKerb(Vector2 a, Vector2 b, float d)
                {
                    float f = Math.Clamp((d - Ax(a)) / MathF.Max(1e-4f, Ax(b) - Ax(a)), 0f, 1f);
                    return (Vector2.Lerp(a, b, f), Single.Lerp(hA, hB, f));
                }
                float d0 = MathF.Max(Ax(al), Ax(ar)), d1 = MathF.Min(Ax(bl), Ax(br));
                if (d0 >= d1)
                {
                    // Overlapping ends: the mesh draws one quad over the four section points,
                    // and the collider is one prism over the same four.
                    Assert.Single(c.Slices);
                    continue;
                }
                for (int i = 0; i <= 8; ++i)
                for (int k = 1; k < 4; ++k)
                {
                    float d = Single.Lerp(d0, d1, i / 8f);
                    var (pl, hl) = OnKerb(al, bl, d);
                    var (pr, hr) = OnKerb(ar, br, d);
                    Vector2 p = Vector2.Lerp(pl, pr, k / 4f);
                    float drawn = Single.Lerp(hl, hr, k / 4f);
                    float? top = c.Slices.Select(sl => _hullTopAt(sl, p)).Where(h => h != null).Max();
                    Assert.True(top != null, $"{p} on the carriageway is covered by no slice");
                    float diff = top.Value - drawn;
                    worstBelowRoad = MathF.Max(worstBelowRoad, -diff);
                    aboveRoad.Add(diff);
                }
            }
        }

        Assert.True(structures > 0, "these cities should carry bridges or tunnels");
        Assert.True(seams > 1000, $"only {seams} branch ends met a slab - the test has stopped testing");
        Assert.True(worstStep < 1e-4f, $"a branch meets its slab {worstStep * 1000:F3} mm off its height");
        Assert.True(worstBelowRoad < 0.001f, $"the collider lies {worstBelowRoad:F4} m below the drawn road");
        aboveRoad.Sort();
        float p99 = aboveRoad[(int)(aboveRoad.Count * 0.99)], max = aboveRoad[^1];
        Console.WriteLine($"KAR-465 seams {seams}, collider above the drawn road: p50 {aboveRoad[aboveRoad.Count / 2]:F3} p99 {p99:F3} max {max:F3} m");
        // Measured 2026-10-25 over these eight cities: p50 0.008, p99 0.047, max 0.170 m.
        Assert.True(p99 < 0.1f, $"the collider stands {p99:F3} m above the drawn road at the 99th percentile");
        Assert.True(max < 0.25f, $"the collider stands up to {max:F3} m above the drawn road");
    }
}
