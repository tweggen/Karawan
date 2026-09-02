using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using builtin.tools;
using engine.streets;
using engine.streets.generation;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Where a building stands on a block, and where its shop windows are.
 *
 * The report was a house hanging in the air over a terrain-following city, its underside
 * on show. The base is ONE scalar - the footprint goes to the L-system with Y forced to
 * zero and is extruded straight up - and it was a sample of the block's PAD, a least
 * squares plane through the corner heights, taken at the building's centre. A footprint is
 * the block outline inset by 1-6 m, so it spans essentially the whole block: measured on
 * the shipped terrain, the block floor rises 13.3 m at the median under a single footprint
 * and 52.4 m at the worst, and EVERY building in every baseline city had both a corner in
 * the air and a corner in the ground.
 *
 * The fix is a bound rather than a better sample, and these tests are what makes it a
 * guarantee rather than a heuristic: over whole generated cities on the real shipped
 * diamond-square terrain, at every vertex of every footprint, the base is at or below the
 * block floor's own triangles read barycentrically.
 */
public class BuildingFootingTests
{
    /**
     * The four baselines this work stream measures on.
     */
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
     * A city on a named height field. `null` means the shipped terrain, sampled through
     * ShippedTerrain and relaxed with the shipped grade policy; `flat` means the default
     * shipped city.
     */
    private static (ClusterDesc, QuarterStore) _city(
        string idString, float size, string ground)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;

        var store = StreetHarness.Generate(idString, size);

        cd.StreetHeightSource = ground switch
        {
            "flat" => new FlatStreetHeight(cd),
            "shipped terrain" => ShippedTerrain.StreetHeightsOf(cd, store),
            "a 5.8 % plane" => new FuncStreetHeight((x, z) => 20f + 0.058f * x),
            _ => new FuncStreetHeight((x, z)
                => 20f + 25f * Single.Sin(x / 220f) + 20f * Single.Cos(z / 190f)),
        };

        return (cd, StreetHarness.GenerateQuarters(cd, store, idString));
    }


    private static readonly string[] _grounds =
        { "shipped terrain", "a 5.8 % plane", "rolling ground" };


    /**
     * The block floor as the operator emits it, as a list of its cap's own triangles.
     */
    private static List<(Vector3 a, Vector3 b, Vector3 c)> _capOf(
        Quarter q, IList<Vector3> outline, IList<CapInsetEdge> inset)
    {
        var path = new List<Vector3> { new(0f, MetaGen.QuarterSidewalkOffset, 0f) };
        var mesh = new global::engine.joyce.Mesh("floor");
        new ExtrudePoly(outline, path, 27, 10000f, false, false, true)
        {
            CapInsetEdges = inset
        }.BuildGeom(mesh);

        float up = MetaGen.QuarterSidewalkOffset;
        var wanted = new List<Vector3>();
        foreach (var v in outline) wanted.Add(v + new Vector3(0f, up, 0f));
        if (null != inset)
        {
            foreach (var e in inset)
            {
                wanted.Add(e.Start + new Vector3(0f, up, 0f));
                wanted.Add(e.End + new Vector3(0f, up, 0f));
            }
        }

        bool IsCap(Vector3 v) => wanted.Any(w => (w - v).Length() < 1e-3f);

        var tris = new List<(Vector3, Vector3, Vector3)>();
        for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
        {
            Vector3 a = mesh.Vertices[(int)mesh.Indices[i]];
            Vector3 b = mesh.Vertices[(int)mesh.Indices[i + 1]];
            Vector3 c = mesh.Vertices[(int)mesh.Indices[i + 2]];
            if (IsCap(a) && IsCap(b) && IsCap(c)) tris.Add((a, b, c));
        }

        return tris;
    }


    /**
     * The floor surface's height at a plan position, read off its own triangles, or null
     * where the position is not covered by the cap.
     */
    private static float? _surfaceAt(
        List<(Vector3 a, Vector3 b, Vector3 c)> tris, Vector2 p)
    {
        foreach (var (a, b, c) in tris)
        {
            Vector2 pa = new(a.X, a.Z), pb = new(b.X, b.Z), pc = new(c.X, c.Z);

            float d = (pb.Y - pc.Y) * (pa.X - pc.X) + (pc.X - pb.X) * (pa.Y - pc.Y);
            if (Single.Abs(d) < 1e-9f) continue;

            float l1 = ((pb.Y - pc.Y) * (p.X - pc.X) + (pc.X - pb.X) * (p.Y - pc.Y)) / d;
            float l2 = ((pc.Y - pa.Y) * (p.X - pc.X) + (pa.X - pc.X) * (p.Y - pc.Y)) / d;
            float l3 = 1f - l1 - l2;
            if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;

            return l1 * a.Y + l2 * b.Y + l3 * c.Y;
        }

        return null;
    }


    private static IEnumerable<(Quarter Q, List<Vector3> Outline,
        List<CapInsetEdge> Inset, Building B)> _buildingsOf(QuarterStore quarters)
    {
        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            if (outline.Count < 3) continue;

            var inset = GenerateClusterQuartersOperator.PavementInsetOf(q, outline);

            foreach (var est in q.GetEstates())
            foreach (var b in est.GetBuildings())
            {
                yield return (q, outline, inset, b);
            }
        }
    }


    /**
     * THE GUARANTEE. A building's base is never above the block floor under it.
     *
     * At every vertex of every footprint of every building of every baseline city, on the
     * shipped terrain and on two analytic slopes, read off the floor's OWN triangles.
     * Vertices rather than an average, because the report was about a CORNER in the air;
     * and the floor's own triangles rather than the ring it was built from, because what
     * the player sees is the surface, and §7j already found a case where the ring was
     * right and the surface was not.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ABuildingsBaseIsNeverAboveTheFloorUnderIt(string idString, float size)
    {
        foreach (var ground in _grounds)
        {
            var (_, quarters) = _city(idString, size, ground);
            int nChecked = 0;

            foreach (var (q, outline, inset, b) in _buildingsOf(quarters))
            {
                float baseY = BuildingFooting.BaseHeightOf(q);
                var tris = _capOf(q, outline, inset);

                foreach (var p in b.GetPoints())
                {
                    float? h = _surfaceAt(tris, new Vector2(p.X, p.Z));
                    if (!h.HasValue) continue;

                    ++nChecked;
                    Assert.True(baseY <= h.Value + 1e-3f,
                        $"{idString}/{size} on {ground}: the building on the block at "
                        + $"{q.GetCenterPoint()} is founded at {baseY:F3} where the block "
                        + $"floor under its own corner {p} is at {h.Value:F3} - "
                        + $"{baseY - h.Value:F3} m in the air");
                }
            }

            Assert.True(nChecked > 4,
                $"only {nChecked} footprint corners of {idString}/{size} on {ground} "
                + "landed on a block floor, which proves too little");
        }
    }


    /**
     * ...and not only at the corners: over the interior of the footprint too.
     *
     * A bound that held at the vertices and failed in between would be a bound on the
     * wrong thing. It cannot happen for a piecewise linear surface, which is exactly why
     * this is worth stating: it is the property the whole construction rests on.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheBaseIsUnderTheFloorAcrossTheWholeFootprint(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, "shipped terrain");
        int nChecked = 0;

        foreach (var (q, outline, inset, b) in _buildingsOf(quarters))
        {
            float baseY = BuildingFooting.BaseHeightOf(q);
            var tris = _capOf(q, outline, inset);

            var pts = b.GetPoints();
            var centre = b.GetCenter();

            for (int i = 0; i < pts.Count; ++i)
            {
                for (int k = 1; k < 8; ++k)
                {
                    Vector3 v = Vector3.Lerp(pts[i], centre, k / 8f);
                    float? h = _surfaceAt(tris, new Vector2(v.X, v.Z));
                    if (!h.HasValue) continue;

                    ++nChecked;
                    Assert.True(baseY <= h.Value + 1e-3f,
                        $"{idString}/{size}: the building on the block at "
                        + $"{q.GetCenterPoint()} is {baseY - h.Value:F3} m above its own "
                        + $"floor at {v}");
                }
            }
        }

        Assert.True(nChecked > 20, $"only {nChecked} interior samples");
    }


    /**
     * The premise the bound rests on: every vertex of the cap carries one of the block's
     * corner heights, or a blend of two of ONE edge's pair.
     *
     * Outer vertices are CornerGroundHeightAt exactly. Each inset point is meant to carry
     * the height its own outer edge has at its own projection onto that edge - so its
     * projection has to LAND on the edge. If one ever extrapolated past a corner the height
     * would leave the corner range and the bound would leak, silently, on that one block.
     * Measured: zero of the four cities has such a point, on any of the three grounds.
     *
     * This is the mutation guard for SidewalkRing's corner ramp: shortening the ramp
     * pushes inset points toward and then past the corners.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void EveryCapVertexCarriesACornerHeightOfItsOwnBlock(string idString, float size)
    {
        foreach (var ground in _grounds)
        {
            var (_, quarters) = _city(idString, size, ground);
            int nChecked = 0;

            foreach (var q in quarters.GetQuarters())
            {
                var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
                if (outline.Count < 3) continue;

                var inset = GenerateClusterQuartersOperator.PavementInsetOf(q, outline);
                if (null == inset) continue;

                int n = outline.Count;
                for (int i = 0; i < n; ++i)
                {
                    Vector3 a = outline[i], b = outline[(i + 1) % n];
                    Vector2 pa = new(a.X, a.Z), pb = new(b.X, b.Z);
                    float l = (pb - pa).Length();

                    float lo = Single.Min(a.Y, b.Y), hi = Single.Max(a.Y, b.Y);

                    foreach (var v in new[] { inset[i].Start, inset[i].End })
                    {
                        float t = Vector2.Dot(new Vector2(v.X, v.Z) - pa, (pb - pa) / l) / l;

                        Assert.True(t >= -1e-3f && t <= 1f + 1e-3f,
                            $"{idString}/{size} on {ground}: an inset point of the block at "
                            + $"{q.GetCenterPoint()} projects to {t:F3} of the way along "
                            + "its own edge, i.e. past a corner, so its height is an "
                            + "extrapolation and the block's corner heights no longer "
                            + "bound the floor");

                        Assert.True(v.Y >= lo - 1e-3f && v.Y <= hi + 1e-3f,
                            $"{idString}/{size} on {ground}: an inset point of the block at "
                            + $"{q.GetCenterPoint()} is at {v.Y:F3}, outside its edge's "
                            + $"[{lo:F3}, {hi:F3}]");

                        ++nChecked;
                    }
                }
            }

            Assert.True(nChecked > 8, $"only {nChecked} inset points on {ground}");
        }
    }


    /**
     * The base IS the lowest pavement on the block, and not merely somewhere below it.
     *
     * Stated on identity rather than on a distance: sinking every building to sea level
     * would satisfy the guarantee above and nothing else. Also measured against a real
     * spread, so it cannot be satisfied by a city whose corners happen to agree.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheBaseIsTheLowestPavementOnTheBlock(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, "shipped terrain");
        int nSpread = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            if (delims.Count < 3) continue;

            float lo = Single.MaxValue, hi = Single.MinValue;
            foreach (var d in delims)
            {
                lo = Single.Min(lo, q.CornerGroundHeightAt(d));
                hi = Single.Max(hi, q.CornerGroundHeightAt(d));
            }

            Assert.Equal(
                lo + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset,
                BuildingFooting.BaseHeightOf(q), 3);

            if (hi - lo > 3f) ++nSpread;
        }

        Assert.True(nSpread > 0,
            $"no block of {idString}/{size} on the shipped terrain has 3 m between its "
            + "highest and lowest corner, so this proves nothing about a slope");
    }


    /**
     * A building still stands its design height above the ground it is on.
     *
     * Sinking the base to the block's lowest corner would otherwise eat the building from
     * the uphill side - measured before HeightOf existed, the roof of 64 of the 149
     * buildings of Yelukhdidru/3000 fell below the block floor somewhere over its own
     * footprint, and the median 24 m building showed 4.5 m above the ground at its highest
     * corner. The roof is now at the block's HIGHEST corner plus the design height, which
     * is an upper bound on the floor for the same reason the base is a lower one.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ABuildingKeepsItsDesignHeightAboveTheGround(string idString, float size)
    {
        foreach (var ground in _grounds)
        {
            var (_, quarters) = _city(idString, size, ground);
            int nChecked = 0;

            foreach (var (q, outline, inset, b) in _buildingsOf(quarters))
            {
                float baseY = BuildingFooting.BaseHeightOf(q);
                float design = b.GetHeight();
                float roof = baseY + BuildingFooting.HeightOf(q, design);

                var tris = _capOf(q, outline, inset);

                foreach (var p in b.GetPoints())
                {
                    float? h = _surfaceAt(tris, new Vector2(p.X, p.Z));
                    if (!h.HasValue) continue;

                    ++nChecked;
                    Assert.True(roof >= h.Value + design - 1e-3f,
                        $"{idString}/{size} on {ground}: the roof of the building on the "
                        + $"block at {q.GetCenterPoint()} is {roof - h.Value:F2} m above "
                        + $"its own ground at {p}, against a design height of {design:F2}");
                }
            }

            Assert.True(nChecked > 4, $"only {nChecked} corners on {ground}");
        }
    }


    /**
     * Every shop is reachable: at or above the pavement in front of IT, and within one
     * storey of it.
     *
     * "In front of it" is the point: a building spans nearly a whole block, and the kerb
     * falls 13 m across one on the shipped terrain, so the constraint cannot be checked -
     * or met - with one height per building. The upper half of the assertion is what stops
     * "reachable" from being satisfied by putting every shop on the roof.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AShopIsAtOrAboveThePavementInFrontOfItAndWithinOneStorey(
        string idString, float size)
    {
        foreach (var ground in _grounds)
        {
            var (_, quarters) = _city(idString, size, ground);
            int nShops = 0, nRaised = 0;

            foreach (var (q, outline, inset, b) in _buildingsOf(quarters))
            {
                foreach (var sf in b.GetShopFronts())
                {
                    Vector2 plan = BuildingFooting.PlanOf(sf);

                    float sill = BuildingFooting.StoreyGroundAt(q, plan)
                                 + MetaGen.ClusterStreetHeight
                                 + MetaGen.QuarterSidewalkOffset;
                    float pavement = BuildingFooting.PavementHeightAt(q, plan);

                    ++nShops;
                    if (sill > pavement + 1e-3f) ++nRaised;

                    Assert.True(sill >= pavement - 1e-3f,
                        $"{idString}/{size} on {ground}: a shop of the block at "
                        + $"{q.GetCenterPoint()} sits {pavement - sill:F2} m below the "
                        + "pavement in front of it");

                    Assert.True(sill - pavement < MetaGen.StoryHeight + 1e-3f,
                        $"{idString}/{size} on {ground}: a shop of the block at "
                        + $"{q.GetCenterPoint()} sits {sill - pavement:F2} m above the "
                        + $"pavement, more than the {MetaGen.StoryHeight} m storey it is "
                        + "supposed to be snapped to");
                }
            }

            Assert.True(nShops > 8, $"only {nShops} shopfronts on {ground}");
            Assert.True(nRaised > 0,
                $"no shopfront of {idString}/{size} on {ground} had to be raised at all, "
                + "so the snapping is untested here");
        }
    }


    /**
     * The window, the interaction point and the TALE door of one shop are on ONE storey.
     *
     * Each of the three adds its own constant to a GROUND height, and StoreyGroundAt is
     * what all three ask - which is why it answers in ground terms rather than in pavement
     * terms. A visible shop window whose interaction point is a storey away is worse than
     * either being wrong alone: ShopNearbyBehavior scores in 3-D with a 16 m radius, so
     * 3 m of vertical error costs a third of the horizontal reach.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheWindowThePoiAndTheDoorOfAShopAreOnOneStorey(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, "shipped terrain");
        int nShops = 0;

        foreach (var (q, outline, inset, b) in _buildingsOf(quarters))
        {
            foreach (var sf in b.GetShopFronts())
            {
                Vector2 plan = BuildingFooting.PlanOf(sf);
                float g = BuildingFooting.StoreyGroundAt(q, plan);

                /*
                 * The three expressions the three sites actually use, so that a change to
                 * any one of them shows up here as a disagreement rather than in play.
                 */
                float window = g + 2.05f;
                float poi = g + 2.5f + 1f;
                float door = g + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset;

                Assert.True(poi > window && poi < window + MetaGen.StoryHeight,
                    $"{idString}/{size}: a shop POI at {poi:F2} is not within the window "
                    + $"starting at {window:F2}");
                Assert.True(Single.Abs(door - window) < MetaGen.StoryHeight,
                    $"{idString}/{size}: a shop door at {door:F2} is a storey away from "
                    + $"its window at {window:F2}");

                ++nShops;
            }
        }

        Assert.True(nShops > 8);
    }


    /**
     * The default FLAT city, exactly.
     *
     * This is the one deliberate move: every house drops by 0.35 m, from the pad plus
     * 2.5 m it has always stood at to the pavement it now stands on. The flat city has
     * been floating every house by that much since the L-system houses were written, hidden
     * wherever a shopfront quad skirted the gap by sitting 0.10 m BELOW the pavement.
     *
     * Everything else on the block stays where it is, and that is asserted as equality
     * rather than as a tolerance: the storey index is a difference of two ground heights
     * that are the same number on a flat block, so it is exactly zero rather than the
     * ceiling of a rounding error, and every constant those three sites add is the constant
     * they added before.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AFlatCityMovesOnlyTheHouseAndOnlyByAThirdOfAMetre(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size, "flat");
        int nBlocks = 0, nShops = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            if (delims.Count < 3) continue;
            ++nBlocks;

            float baseY = BuildingFooting.BaseHeightOf(q);

            /*
             * The expression that shipped, at the building centre, against the one that
             * ships now.
             */
            foreach (var est in q.GetEstates())
            foreach (var b in est.GetBuildings())
            {
                var c = b.GetCenter();
                float wasY = 2.5f + q.GroundHeightAt(new Vector2(c.X, c.Z));

                Assert.Equal(0.35f, wasY - baseY, 4);

                Assert.Equal(b.GetHeight(), BuildingFooting.HeightOf(q, b.GetHeight()));

                foreach (var sf in b.GetShopFronts())
                {
                    Vector2 plan = BuildingFooting.PlanOf(sf);

                    Assert.Equal(0, BuildingFooting.StoreyAt(q, plan));

                    /*
                     * Bit for bit: the shopfront quad, the shop POI and the TALE door.
                     * Vector3 addition is commutative, so the shopfront's old
                     * `2.05f + pad` and the new `ground + 2.05f` are the same float.
                     */
                    float g = BuildingFooting.StoreyGroundAt(q, plan);

                    Assert.Equal(2.05f + cd.AverageHeight, g + 2.05f);
                    Assert.Equal(
                        cd.GroundHeightAt(Vector3.Zero) + 2.5f + 1f, g + 2.5f + 1f);
                    ++nShops;
                }

                Assert.Equal(
                    q.GroundHeightAt(q.GetCenterPoint())
                    + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset,
                    BuildingFooting.PavementHeightAt(q, new Vector2(c.X, c.Z)));
            }
        }

        Assert.True(nBlocks > 0);
        Assert.True(nShops > 0, $"no shopfront in the flat {idString}/{size}");
    }


    /**
     * The block floor's own surface is at one height everywhere in a flat city, so the
     * pavement lookup cannot be measuring something else.
     *
     * Without this the equality above holds for any function at all that happens to return
     * the pad's value on a flat block - including one that ignores its argument.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ThePavementLookupFollowsTheBlockOnASlope(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, "shipped terrain");

        var errors = new List<float>();

        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            if (outline.Count < 3) continue;

            var inset = GenerateClusterQuartersOperator.PavementInsetOf(q, outline);
            if (null == inset) continue;

            var tris = _capOf(q, outline, inset);
            int n = outline.Count;

            for (int i = 0; i < n; ++i)
            {
                /*
                 * Half a pavement width in from the middle of each edge, i.e. on the rim,
                 * where the surface really is the edge's own linear height.
                 */
                Vector2 o0 = new(outline[i].X, outline[i].Z);
                Vector2 o1 = new(outline[(i + 1) % n].X, outline[(i + 1) % n].Z);
                Vector2 mid = 0.5f * (o0 + o1);
                Vector2 toInset = 0.5f * (new Vector2(inset[i].Start.X, inset[i].Start.Z)
                                          + new Vector2(inset[i].End.X, inset[i].End.Z))
                                  - mid;

                Vector2 p = mid + 0.5f * toInset;

                float? h = _surfaceAt(tris, p);
                if (!h.HasValue) continue;

                errors.Add(Single.Abs(h.Value - BuildingFooting.PavementHeightAt(q, p)));
            }
        }

        Assert.True(errors.Count > 8, $"only {errors.Count} rim samples");

        errors.Sort();
        float p95 = errors[(int)(0.95f * errors.Count)];

        Assert.True(p95 < 0.05f,
            $"the pavement lookup is {p95:F3} m off the block floor's own surface at the "
            + "95th percentile of the rim of {idString}/{size}");
    }


    /**
     * A block carries one estate, and an estate at most one building.
     *
     * This is what makes the block-wide bound the right one rather than a lazy one: a
     * footprint IS the block, inset by 1-6 m. The day a block carries several buildings
     * this test fails, and the bound has to be taken over each footprint instead - which
     * is a real difference, since the exact minimum over a footprint sits up to 3.7 m
     * above the block's own minimum on the worst building measured.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ABlockCarriesOneEstateAndAtMostOneBuilding(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, "shipped terrain");
        int nBuildings = 0;

        foreach (var q in quarters.GetQuarters())
        {
            Assert.Single(q.GetEstates());

            foreach (var est in q.GetEstates())
            {
                Assert.True(est.GetBuildings().Count <= 1,
                    $"{idString}/{size}: an estate of the block at {q.GetCenterPoint()} "
                    + $"carries {est.GetBuildings().Count} buildings, so "
                    + "BuildingFooting's bound over the whole block over-sinks the "
                    + "smaller ones and has to be taken over each footprint");
                nBuildings += est.GetBuildings().Count;
            }
        }

        Assert.True(nBuildings > 0);
    }


    /**
     * Only one place decides where a building on a block is founded.
     *
     * The house operator used to compute its own base from the pad plus a constant, and
     * that is the mutation this exists for: putting it back compiles, leaves every test
     * above green on the flat city, and floats every house in a hillside one again. A
     * source scan, because what has to hold is that there is no SECOND expression - a
     * second, correct copy would pass any test of the value.
     */
    [Fact]
    public void OnlyOnePlaceDecidesWhereABuildingIsFounded()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        string path = Path.GetFullPath(Path.Combine(
            root, "..", "nogameCode", "nogame", "cities", "GenerateHousesOperator.cs"));
        Assert.True(File.Exists(path), $"could not find the house operator at {path}");

        string source = File.ReadAllText(path);

        Assert.Contains("BuildingFooting", source);
        Assert.DoesNotContain("quarter.GroundHeightAt", source);
        Assert.DoesNotContain("2.5f +", source);
    }


    /**
     * The shop POI asks the block, not the terrain.
     *
     * It was the only thing on a block that did not - ClusterDesc.GroundHeightAt is the
     * TERRAIN even in the middle of a road, so in a hillside city the interaction point of
     * a shop was neither on its window nor on its pavement.
     */
    [Fact]
    public void TheShopPoiAsksTheBlock()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        string path = Path.GetFullPath(Path.Combine(
            root, "..", "nogameCode", "nogame", "cities", "GenerateShopsOperator.cs"));
        Assert.True(File.Exists(path), $"could not find the shops operator at {path}");

        string source = File.ReadAllText(path);

        Assert.Contains("BuildingFooting.StoreyGroundAt", source);
        Assert.DoesNotContain("clusterDesc.GroundHeightAt", source);
    }
}
