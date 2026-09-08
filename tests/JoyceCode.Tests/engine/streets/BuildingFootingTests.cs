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
                float baseY = BuildingFooting.BaseHeightOf(q, b);
                var tris = DrawnBlockFloor.CapOf(outline, inset);

                foreach (var p in b.GetPoints())
                {
                    float? h = DrawnBlockFloor.SurfaceAt(tris, new Vector2(p.X, p.Z));
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
     *
     * ⚠️ SUPERSEDED FROM WP-O3 (§7w), NOT RE-BASELINED - and the change is in the sampling
     * rather than in the assertion. It used to walk each footprint corner toward the
     * footprint's CENTRE in eighths:
     *
     *     Vector3 v = Vector3.Lerp(pts[i], centre, k / 8f);
     *
     * A building footprint is a block outline inset by a pavement width and is very often
     * not convex, so that segment leaves the footprint - which did not matter while the
     * bound was the whole BLOCK's, and is a false failure the moment the bound belongs to
     * the piece. The samples are filtered to the footprint's own interior now, and a
     * regular grid is used as well as the spokes so the interior is covered rather than
     * merely crossed.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheBaseIsUnderTheFloorAcrossTheWholeFootprint(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, "shipped terrain");
        int nChecked = 0;

        foreach (var (q, outline, inset, b) in _buildingsOf(quarters))
        {
            float baseY = BuildingFooting.BaseHeightOf(q, b);
            var tris = DrawnBlockFloor.CapOf(outline, inset);

            var pts = b.GetPoints();
            var centre = b.GetCenter();
            var poly = pts.Select(p => new Vector2(p.X, p.Z)).ToList();

            var samples = new List<Vector2>();
            for (int i = 0; i < pts.Count; ++i)
            {
                for (int k = 1; k < 8; ++k)
                {
                    Vector3 v = Vector3.Lerp(pts[i], centre, k / 8f);
                    samples.Add(new Vector2(v.X, v.Z));
                }
            }

            float x0 = poly.Min(p => p.X), x1 = poly.Max(p => p.X);
            float y0 = poly.Min(p => p.Y), y1 = poly.Max(p => p.Y);
            for (int i = 1; i < 16; ++i)
            for (int k = 1; k < 16; ++k)
            {
                samples.Add(new Vector2(
                    x0 + (x1 - x0) * i / 16f, y0 + (y1 - y0) * k / 16f));
            }

            foreach (var p in samples)
            {
                if (!DrawnBlockFloor.Contains(poly, p)) continue;

                float? h = DrawnBlockFloor.SurfaceAt(tris, p);
                if (!h.HasValue) continue;

                ++nChecked;
                Assert.True(baseY <= h.Value + 1e-3f,
                    $"{idString}/{size}: the building on the block at "
                    + $"{q.GetCenterPoint()} is {baseY - h.Value:F3} m above its own "
                    + $"floor at {p}");
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
     * The base IS the lowest pavement over this building's own footprint, and not merely
     * somewhere below it.
     *
     * Stated on identity rather than on a distance: sinking every building to sea level
     * would satisfy the guarantee above and nothing else. The identity is against the floor
     * as DrawnBlockFloor reads it back out of the mesh the operator emits, sampled over the
     * footprint, so it cannot be satisfied by a second copy of the production expression.
     *
     * ⚠️ SUPERSEDED FROM WP-O3 (§7w), NOT RE-BASELINED. This was
     * TheBaseIsTheLowestPavementOnTheBlock, and it asserted
     *
     *     Assert.Equal(lo + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset,
     *                  BuildingFooting.BaseHeightOf(q), 3);
     *
     * over the block's own corner range, with the comment "the base IS the lowest pavement
     * on the block". A block carries several buildings since WP-O2 and each is founded on
     * its own piece of ground, so that identity is no longer the rule; what replaces it is
     * an identity against the surface, which is strictly stronger.
     *
     * The block-wide corner range survives as the FALLBACK and as a sanity bound - no
     * building may be founded below its block's lowest corner either - and both are
     * asserted here.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheBaseIsTheLowestPavementOverTheFootprint(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, "shipped terrain");
        int nSpread = 0, nTight = 0, nChecked = 0;

        foreach (var (q, outline, inset, b) in _buildingsOf(quarters))
        {
            float lo = Single.MaxValue, hi = Single.MinValue;
            foreach (var d in q.GetDelims())
            {
                lo = Single.Min(lo, q.CornerGroundHeightAt(d));
                hi = Single.Max(hi, q.CornerGroundHeightAt(d));
            }

            float baseY = BuildingFooting.BaseHeightOf(q, b);
            ++nChecked;

            /*
             * The old rule survives as a bound: the floor cannot leave the block's corner
             * range, so neither may its minimum over any piece of the block.
             */
            Assert.InRange(
                baseY,
                lo + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset - 1e-3f,
                hi + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset + 1e-3f);

            /*
             * And it IS the lowest point of the drawn floor over this footprint, as an
             * IDENTITY against DrawnBlockFloor's own enumeration over the triangles it
             * reads back out of the emitted mesh. A tolerance would be satisfied by any
             * expression that happens to answer low, and a distribution would be satisfied
             * by the block-wide bound on most blocks.
             */
            var tris = DrawnBlockFloor.CapOf(outline, inset);
            var poly = b.GetPoints().Select(p => new Vector2(p.X, p.Z)).ToList();
            var (dLo, dHi) = DrawnBlockFloor.BoundsOver(tris, poly);

            if (dLo < Single.MaxValue)
            {
                ++nTight;
                Assert.Equal(dLo, baseY, 3);
                Assert.Equal(dHi - dLo, BuildingFooting.HeightOf(q, b, 0f), 3);
            }

            if (hi - lo > 3f) ++nSpread;
        }

        Assert.True(nSpread > 0,
            $"no block of {idString}/{size} on the shipped terrain has 3 m between its "
            + "highest and lowest corner, so this proves nothing about a slope");

        Assert.True(nTight > nChecked / 2,
            $"only {nTight} of {nChecked} buildings of {idString}/{size} have a footprint "
            + "the drawn floor covers at all, so the identity above proves too little");
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
                float baseY = BuildingFooting.BaseHeightOf(q, b);
                float design = b.GetHeight();
                float roof = baseY + BuildingFooting.HeightOf(q, b, design);

                var tris = DrawnBlockFloor.CapOf(outline, inset);

                foreach (var p in b.GetPoints())
                {
                    float? h = DrawnBlockFloor.SurfaceAt(tris, new Vector2(p.X, p.Z));
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

                    float sill = BuildingFooting.StoreyGroundAt(q, b, plan)
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
                float g = BuildingFooting.StoreyGroundAt(q, b, plan);

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

            /*
             * The expression that shipped, at the building centre, against the one that
             * ships now.
             */
            foreach (var est in q.GetEstates())
            foreach (var b in est.GetBuildings())
            {
                float baseY = BuildingFooting.BaseHeightOf(q, b);
                var c = b.GetCenter();
                float wasY = 2.5f + q.GroundHeightAt(new Vector2(c.X, c.Z));

                Assert.Equal(0.35f, wasY - baseY, 4);

                /*
                 * ⚠️ EXACTLY, not to four decimals: WP-O3 reads the base off the block
                 * floor's own cap, and a flat cap is at the cluster's own average height at
                 * every one of its vertices and at every point between them. Anything that
                 * blended three equal heights as a weighted SUM would come back a unit in
                 * the last place away, and the storey index below would be the ceiling of
                 * that rather than zero.
                 */
                Assert.Equal(
                    cd.AverageHeight + MetaGen.ClusterStreetHeight
                    + MetaGen.QuarterSidewalkOffset, baseY);

                Assert.Equal(b.GetHeight(), BuildingFooting.HeightOf(q, b, b.GetHeight()));

                foreach (var sf in b.GetShopFronts())
                {
                    Vector2 plan = BuildingFooting.PlanOf(sf);

                    Assert.Equal(0, BuildingFooting.StoreyAt(q, b, plan));

                    /*
                     * Bit for bit: the shopfront quad, the shop POI and the TALE door.
                     * Vector3 addition is commutative, so the shopfront's old
                     * `2.05f + pad` and the new `ground + 2.05f` are the same float.
                     */
                    float g = BuildingFooting.StoreyGroundAt(q, b, plan);

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

            var tris = DrawnBlockFloor.CapOf(outline, inset);
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

                float? h = DrawnBlockFloor.SurfaceAt(tris, p);
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
     * ⚠️ A BLOCK CARRIES ONE ESTATE, AND THAT ESTATE MAY NOW CARRY SEVERAL BUILDINGS - SO
     * THE JUSTIFICATION FOR THE BLOCK-WIDE BOUND HAS STOPPED BEING TRUE.
     *
     * SUPERSEDED BY WP-O2 (§7v), NOT RE-BASELINED. This was
     * ABlockCarriesOneEstateAndAtMostOneBuilding and it asserted
     * `est.GetBuildings().Count <= 1`, with the comment:
     *
     *     "This is what makes the block-wide bound the right one rather than a lazy one: a
     *      footprint IS the block, inset by 1-6 m. The day a block carries several
     *      buildings this test fails, and the bound has to be taken over each footprint
     *      instead - which is a real difference, since the exact minimum over a footprint
     *      sits up to 3.7 m above the block's own minimum on the worst building measured."
     *
     * That day is today, and the gate did exactly what it was written to do: WP-O2 gives
     * every piece of a block's buildable land its own building, so a block whose estate the
     * pavement inset, a structure or a dead-end spur splits carries two or three. This test
     * now records how many and says whose problem it is, rather than asserting a premise the
     * shipped generator no longer honours.
     *
     * ⚠️ WHAT IT COSTS, AND IT IS NOT SMALL. BuildingFooting.BaseHeightOf answers the
     * BLOCK's lowest corner; §7t.10.7 measured the extra burial imposed on the higher piece
     * of a split block over the shipped world at a median 7.46 m flag off and 6.21 m flag on,
     * worst 21.2 and 28.1 m, over 5 m on 12 of 21 and 84 of 150 blocks. It is EXACTLY ZERO on
     * a flat city, which is why it has to be measured on the ground the game ships.
     *
     * ⚠️ THAT IS WP-O3 AND IT IS NOT WP-O2's. The bound is still a bound - a building is
     * still never above the floor under it, which ABuildingsBaseIsNeverAboveTheFloorUnderIt
     * asserts unchanged - it is merely looser than it needs to be for the smaller piece.
     *
     * ONE ESTATE PER BLOCK IS STILL ASSERTED and is still load bearing: GenerateShopsOperator
     * and GenerateTreesOperator both walk a block's estates, and PlayerStart.PoseIn puts a new
     * game on the first estate with no building.
     */
    [Theory]
    //                                     estates with >1 building  most on one
    [InlineData("seed000", 500f, 0, 1)]
    [InlineData("Yelukhdidru", 800f, 0, 1)]
    [InlineData("seed000", 1500f, 1, 2)]
    [InlineData("Yelukhdidru", 3000f, 1, 2)]
    public void ABlockCarriesOneEstateAndThatEstateMayCarrySeveral(
        string idString, float size, int expectedMulti, int expectedMost)
    {
        var (_, quarters) = _city(idString, size, "shipped terrain");
        int nBuildings = 0, nMulti = 0, most = 0;

        foreach (var q in quarters.GetQuarters())
        {
            Assert.Single(q.GetEstates());

            foreach (var est in q.GetEstates())
            {
                int here = est.GetBuildings().Count;
                nBuildings += here;
                if (here > 1) ++nMulti;
                most = Math.Max(most, here);
            }
        }

        Assert.True(nBuildings > 0);
        Assert.Equal(expectedMulti, nMulti);
        Assert.Equal(expectedMost, most);
    }


    /**
     * ⚠️ THE SURFACE BuildingFooting FOUNDS A BUILDING ON IS THE ONE THE FLOOR IS DRAWN AS,
     * PLUS TWO CONSTANTS, EXACTLY.
     *
     * generation.BlockFloor carries GROUND heights and the emitted cap carries the road
     * offset and the kerb on top, so the two are one surface at an offset - which is what
     * lets the storey index be a difference of ground heights with no constant in it while
     * the base is still the drawn floor. Asserted at every vertex of the emitted cap, on
     * three grounds, because a surface that agreed to a tolerance would be a second model
     * of the floor rather than the floor.
     *
     * The two sides come from different calls: DrawnBlockFloor runs the whole of
     * ExtrudePoly.BuildGeom and picks the cap out of the mesh by position, BlockFloor asks
     * ExtrudePoly.BuildCap for the cap directly.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheDrawnFloorIsThisSurfacePlusTwoConstants(string idString, float size)
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
                var drawn = DrawnBlockFloor.CapOf(outline, inset);
                var floor = BlockFloor.Of(q);

                Assert.NotNull(floor);

                foreach (var (a, b, c) in drawn)
                foreach (var v in new[] { a, b, c })
                {
                    Assert.True(floor.TryHeightAt(new Vector2(v.X, v.Z), out float g),
                        $"{idString}/{size} on {ground}: the cap of the block at "
                        + $"{q.GetCenterPoint()} is drawn at {v} and BlockFloor does not "
                        + "cover that position at all");

                    /*
                     * As an absolute difference and not Assert.Equal(.., 3), which ROUNDS
                     * both sides - two values a millionth apart land on opposite sides of a
                     * decimal and the gate fails for nothing. §7q's own lesson.
                     */
                    float delta = v.Y - (g + MetaGen.ClusterStreetHeight
                                           + MetaGen.QuarterSidewalkOffset);

                    Assert.True(Single.Abs(delta) < 1e-3f,
                        $"{idString}/{size} on {ground}: the block at {q.GetCenterPoint()} "
                        + $"is drawn at {v.Y:F4} where BlockFloor says "
                        + $"{g + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset:F4}");

                    ++nChecked;
                }
            }

            Assert.True(nChecked > 100, $"only {nChecked} cap vertices on {ground}");
        }
    }


    /**
     * ⚠️ THE CAP'S OWN CORNERS INSIDE A POLYGON ARE PART OF THE ANSWER, DRIVEN DIRECTLY.
     *
     * The extremes of a surface that is affine on each triangle are at the corners of the
     * region's intersection with one of them, and one of those three kinds of corner is a
     * TRIANGLE corner inside the region. It fires on nothing the game builds - a cap has no
     * vertex in the block's interior, and a footprint is the block outline inset by exactly
     * the width the pavement's inner edge stands at, so
     * BuildingFootingWorldTests.NoCornerOfTheFloorIsInsideAFootprint counts zero over
     * seventy cities. Deleting the term consequently passes every other gate here and there,
     * which is what mutation testing found.
     *
     * So it is driven with a polygon that has no other candidate in it at all: the block's
     * bounding box, grown by a metre. None of ITS corners is on the cap, and no cap edge
     * crosses its boundary, so the only thing that can answer is the cap's own corners - and
     * the answer has to be the block's corner range exactly, since the outline's corners are
     * cap vertices carrying exactly those heights.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void OnlyTheCapsOwnCornersCanAnswerForAPolygon(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, "shipped terrain");
        int nChecked = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var floor = BlockFloor.Of(q);
            if (null == floor) continue;

            float x0 = Single.MaxValue, y0 = Single.MaxValue;
            float x1 = Single.MinValue, y1 = Single.MinValue;
            foreach (var d in q.GetDelims())
            {
                x0 = Single.Min(x0, d.StartPoint.X); x1 = Single.Max(x1, d.StartPoint.X);
                y0 = Single.Min(y0, d.StartPoint.Y); y1 = Single.Max(y1, d.StartPoint.Y);
            }

            var box = new List<Vector3>
            {
                new(x0 - 1f, 0f, y0 - 1f), new(x1 + 1f, 0f, y0 - 1f),
                new(x1 + 1f, 0f, y1 + 1f), new(x0 - 1f, 0f, y1 + 1f)
            };

            Assert.True(floor.TryBoundsOver(box, out float lo, out float hi),
                $"{idString}/{size}: the block at {q.GetCenterPoint()} answers nothing for "
                + "a polygon that contains the whole of it, so the cap's own corners are "
                + "not being counted at all");

            Assert.Equal(BuildingFooting.MinGroundOf(q), lo);
            Assert.Equal(BuildingFooting.MaxGroundOf(q), hi);

            ++nChecked;
        }

        Assert.True(nChecked > 0, $"no block of {idString}/{size} has a floor");
    }


    /**
     * ⚠️ THE RULE WP-O3 WAS BRIEFED TO BUILD IS NOT A BOUND, AND HERE IS WHAT IT COSTS.
     *
     * "The lowest of the piece's own boundary heights" - BuildingFooting.GroundAt at each
     * footprint corner, which is the block's boundary ring read at that corner - is the
     * obvious per-piece rule and the one this work package set out to build. It is not a
     * bound on the floor: the cap's interior is one tessellation of the ring inside the
     * pavement rim, and a triangle may run clean across the block and carry the height of a
     * corner the piece never comes near underneath it.
     *
     * Recorded as counts on the pinned baselines rather than described, so that a later
     * simplification back to the cheap rule fails here. The world-wide figure is in
     * BuildingFootingWorldTests.
     */
    [Theory]
    //                                    buildings it floats, and the worst of them
    [InlineData("seed000", 500f, 0, 0f, 0f)]
    [InlineData("Yelukhdidru", 800f, 2, 0.4f, 0.6f)]
    [InlineData("seed000", 1500f, 28, 1.4f, 1.6f)]
    [InlineData("Yelukhdidru", 3000f, 45, 9.7f, 9.9f)]
    public void TheObviousPerPieceBoundFloatsABuilding(
        string idString, float size, int expectedFloating, float worstLo, float worstHi)
    {
        var (_, quarters) = _city(idString, size, "shipped terrain");
        int nFloating = 0, nChecked = 0;
        float worst = 0f;

        foreach (var (q, outline, inset, b) in _buildingsOf(quarters))
        {
            var drawn = DrawnBlockFloor.CapOf(outline, inset);
            var poly = b.GetPoints().Select(p => new Vector2(p.X, p.Z)).ToList();
            var (dLo, _) = DrawnBlockFloor.BoundsOver(drawn, poly);
            if (dLo == Single.MaxValue) continue;

            float floorMin = dLo - MetaGen.ClusterStreetHeight - MetaGen.QuarterSidewalkOffset;

            float naive = Single.MaxValue;
            foreach (var v in poly) naive = Single.Min(naive, BuildingFooting.GroundAt(q, v));

            ++nChecked;
            if (naive > floorMin + 1e-3f)
            {
                ++nFloating;
                worst = Single.Max(worst, naive - floorMin);
            }
        }

        Assert.True(nChecked > 0);
        Assert.Equal(expectedFloating, nFloating);
        Assert.InRange(worst, worstLo, worstHi);
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

        /*
         * ⚠️ AND IT FOUNDS EACH BUILDING ON ITS OWN PIECE. The block-wide overload is gone,
         * so a call site that has only a Quarter does not compile - but nothing in the type
         * system stops a site from handing over SOME building of the block instead of the
         * one it is drawing, and on a block with two estates that is the whole defect back
         * again with a different sign. The scan names the variable the loop is over.
         */
        Assert.Contains(".BaseHeightOf(quarter, building)", source);
        Assert.Contains(".HeightOf(quarter, building, building.GetHeight())", source);
        Assert.Contains("StoreyGroundAt(\n                                                quarter, building,",
            source.Replace("\r\n", "\n"));
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

        /*
         * ...on the storey of the building the shopfront belongs to, which this operator
         * has to carry out of the loop that picked it. Naming the variable rather than
         * merely the call, for the reason above.
         */
        Assert.Contains("StoreyGroundAt(\n                    quarter, shopBuilding,",
            source.Replace("\r\n", "\n"));
        Assert.Contains("shopBuilding = myBuilding;", source);
    }
}
