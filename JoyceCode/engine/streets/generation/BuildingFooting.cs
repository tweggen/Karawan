using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.streets.generation;


/**
 * Where a building on a city block stands, and which storey its shop windows are on.
 *
 * **The guarantee this exists to make: a building's base is never above the ground under
 * it.** Not "usually" - the report that started this was a house hanging in the air with
 * its underside on show, and a heuristic that is right for most blocks would leave that
 * exact sighting possible.
 *
 * **The floor stays planar.** That is a design decision and not a limitation: real
 * buildings have level floors, and a shopfront is aligned per storey rather than ramped
 * gradually along the pavement. A footprint-following base was considered and rejected.
 *
 * **Over the building's own footprint, read off the floor's own surface** (WP-O3, §7w).
 * Until WP-O2 a block carried exactly ONE estate and at most ONE building, and the bound
 * was the block's LOWEST CORNER: sound, because everything on a block hangs off the heights
 * of its corner junctions -
 *
 *   - the block floor's outline takes each corner's own junction height exactly
 *     (Quarter.CornerGroundHeightAt, see GenerateClusterQuartersOperator.FloorOutlineOf);
 *   - the pavement is that outline raised by QuarterSidewalkOffset;
 *   - the rim's inset points each carry the height their own outer EDGE has at their own
 *     projection onto it, i.e. a convex combination of that edge's two corner heights
 *     (generation.SidewalkRing) - measured over the four baseline cities, no inset point's
 *     projection falls outside its edge and no inset height falls outside the block's
 *     corner range, on any of them.
 *
 * - so every vertex of the floor's cap carries a corner height or a blend of two, and a
 * piecewise linear surface over those vertices cannot leave the block's corner range. It
 * was tight because an estate IS the block outline inset by Quarter.SidewalkWidth (1-6 m),
 * so a footprint spanned essentially the whole block: the exact minimum of the cap over a
 * footprint sat only 0.19-0.61 m above the bound at the median and 3.7 m at the worst
 * building of the four baseline cities.
 *
 * WP-O2 gives every piece of a block's buildable land its own building, and that slack
 * becomes the amount by which the higher piece of a split block is over-sunk: a median
 * 6-7 m and up to 28 m over the shipped world (§7t.10.7). That is what WP-O3 repairs.
 *
 * ⚠️ **AND THE OBVIOUS PER-PIECE BOUND IS NOT A BOUND.** The corner argument says a
 * piecewise linear surface over the CAP's vertices cannot leave the BLOCK's corner range.
 * It says nothing whatever about a sub-region of the block, because the cap's interior is
 * one tessellation of the ring left inside the pavement rim and the tessellator is free to
 * run a triangle clean across the block. Measured over the four baseline cities on the
 * shipped terrain, taking the lowest of a piece's own boundary heights - the obvious
 * per-piece rule - floats 0 / 2 / 28 / 45 buildings, by up to 9.77 m, and nothing local to
 * the piece can see it coming.
 *
 * So the answer is read off the surface itself. generation.BlockFloor is the cap the floor
 * is DRAWN as, through the same ExtrudePoly.BuildCap the emission goes through, and
 * MinGroundOf takes its EXACT minimum over the footprint - at the footprint's corners, at
 * the cap's corners inside the footprint, and where the two boundaries cross. That is the
 * tightest bound there is; it is a bound rather than a sample because it is a minimum over
 * the whole footprint rather than a reading at one point of it. A block with no cap, and a
 * footprint that does not meet its block's cap, fall back to the block's corner range,
 * which is the old rule - still sound, still loose.
 *
 * **What it costs, measured on the shipped diamond-square terrain with the shipped grade
 * policy** - burial at a footprint vertex, i.e. how far the block floor is above the base
 * there: median 4.9-9.4 m, p90 8.6-23.3 m, worst 53.9 m. That is the accepted price of a
 * planar floor on a block up to 150 m across whose kerb falls 13 m; floating is not
 * accepted at any price. It is also why HeightOf exists.
 */
public static class BuildingFooting
{
    /**
     * How tall one storey is.
     *
     * Shops snap to a storey, so this has to be the SAME number the building geometry is
     * built from; MetaGen.StoryHeight is the one copy, and QuarterGenerator (which sizes
     * buildings in storeys) and nogame's house operator (which sizes shop windows in them)
     * both read it.
     */
    public static float StoryHeight => world.MetaGen.StoryHeight;


    /**
     * The ground height of the block's boundary at a plan position, in cluster space.
     *
     * The block's boundary is a closed ring of edges, each running between two junctions
     * and carrying their two heights linearly - which, since the pavement rim is level
     * ACROSS its width, is the height of the pavement anywhere along that edge. So the
     * answer for a point is its own nearest edge's height at its own projection onto it.
     *
     * Note this is the GROUND, not the pavement: MetaGen.ClusterStreetHeight and
     * QuarterSidewalkOffset are added by PavementHeightAt. Keeping the two apart is what
     * lets the storey index below be a difference of ground heights with no constants in
     * it at all, and so exactly zero on a flat city.
     */
    public static float GroundAt(Quarter quarter, in Vector2 v2Cluster)
    {
        var delims = quarter.GetDelims();
        int n = delims.Count;
        if (0 == n)
        {
            /*
             * A block with no corners is not a block, and nothing this file serves can
             * reach one - an estate only exists on a traced ring. Answer with what the
             * block itself would say rather than inventing a height here.
             */
            return quarter.GroundHeightAt(v2Cluster);
        }

        float bestD2 = Single.MaxValue;
        float bestH = quarter.CornerGroundHeightAt(delims[0]);

        for (int i = 0; i < n; ++i)
        {
            Vector2 a = delims[i].StartPoint;
            Vector2 b = delims[(i + 1) % n].StartPoint;

            Vector2 ab = b - a;
            float l2 = Vector2.Dot(ab, ab);
            if (!(l2 > 1e-8f))
            {
                continue;
            }

            float t = Single.Clamp(Vector2.Dot(v2Cluster - a, ab) / l2, 0f, 1f);
            float d2 = (v2Cluster - (a + t * ab)).LengthSquared();

            if (d2 < bestD2)
            {
                bestD2 = d2;

                float ha = quarter.CornerGroundHeightAt(delims[i]);
                float hb = quarter.CornerGroundHeightAt(delims[(i + 1) % n]);
                bestH = ha + t * (hb - ha);
            }
        }

        return bestH;
    }


    /**
     * The lowest ground height the block floor reaches over one building's footprint.
     *
     * The bound a building is founded on. Read off the floor's own cap
     * (generation.BlockFloor), which is the only thing that can see a tessellation running
     * across the block, and never above the surface anywhere over the footprint.
     *
     * Falls back to the block's own lowest corner - the rule that shipped before WP-O3 -
     * for a block whose cap cannot be built and for a footprint that does not meet it. That
     * is still a sound bound for exactly the reason given on the class; it is merely the
     * loose one.
     */
    public static float MinGroundOf(Quarter quarter, Building building)
        => _boundsOf(quarter, building).Lo;


    /**
     * The highest ground height the block floor reaches over one building's footprint.
     */
    public static float MaxGroundOf(Quarter quarter, Building building)
        => _boundsOf(quarter, building).Hi;


    private static (float Lo, float Hi) _boundsOf(Quarter quarter, Building building)
    {
        if (null != building && building.TryGetFooting(out float was, out float wasHi))
        {
            return (was, wasHi);
        }

        var floor = BlockFloor.Of(quarter);
        if (null != floor && null != building
            && floor.TryBoundsOver(building.GetPoints(), out float lo, out float hi))
        {
            building.SetFooting(lo, hi);
            return (lo, hi);
        }

        (float loBlock, float hiBlock) = (MinGroundOf(quarter), MaxGroundOf(quarter));
        building?.SetFooting(loBlock, hiBlock);

        return (loBlock, hiBlock);
    }


    /**
     * The lowest ground height any of this block's corners has.
     */
    public static float MinGroundOf(Quarter quarter)
    {
        var delims = quarter.GetDelims();
        if (0 == delims.Count)
        {
            return quarter.GroundHeightAt(quarter.GetCenterPoint());
        }

        float h = Single.MaxValue;
        foreach (var delim in delims)
        {
            h = Single.Min(h, quarter.CornerGroundHeightAt(delim));
        }

        return h;
    }


    /**
     * The highest ground height any of this block's corners has.
     */
    public static float MaxGroundOf(Quarter quarter)
    {
        var delims = quarter.GetDelims();
        if (0 == delims.Count)
        {
            return quarter.GroundHeightAt(quarter.GetCenterPoint());
        }

        float h = Single.MinValue;
        foreach (var delim in delims)
        {
            h = Single.Max(h, quarter.CornerGroundHeightAt(delim));
        }

        return h;
    }


    /**
     * The height of the pavement surface at a plan position on this block.
     *
     * The block floor is the boundary ring raised by the street offset and extruded up by
     * QuarterSidewalkOffset, and its top face IS the pavement - there is no separate
     * sidewalk object anywhere in the codebase.
     */
    public static float PavementHeightAt(Quarter quarter, in Vector2 v2Cluster)
        => _pavementOf(GroundAt(quarter, v2Cluster));


    /**
     * The pavement height at a plan position, if that position is ON this block.
     *
     * For a caller that HAS a block but is not sure the point is on it - a walker's travel
     * destination may well be on another one, and answering from the wrong block is worse
     * than answering from the terrain.
     *
     * Here rather than at the call site because the call site is in nogameCode, which the
     * test assembly does not reference: a scan can see that PavementHeightAt is named there
     * and cannot see whether the branch that names it is ever taken. Writing `if (false)`
     * around it in GoToStrategyPart passed the entire suite, which is what this exists to
     * make impossible.
     *
     * The AABB rather than the polygon: a block's delimiters are a closed ring and a point
     * in polygon test over them would be exact, but the answer for a point just outside the
     * ring is the kerb's own height either way, and the box is what QuarterStore already
     * indexes on.
     */
    public static bool TryPavementHeightAt(
        Quarter quarter, in Vector2 v2Cluster, out float height)
    {
        height = 0f;
        if (null == quarter) return false;

        var aabb = quarter.AABB;
        if (!aabb.Contains(new Vector3(v2Cluster.X, aabb.Center.Y, v2Cluster.Y)))
        {
            return false;
        }

        height = PavementHeightAt(quarter, v2Cluster);
        return true;
    }


    /**
     * The one planar level this building is founded at.
     *
     * At or below the pavement everywhere over its own footprint, with equality only at the
     * lowest point the floor reaches there - where the two surfaces are tangent at a point
     * rather than coplanar, so there is nothing for the depth buffer to fight over. No
     * margin is subtracted: a margin would move the shipped flat city by more than the
     * 0.35 m the original change already cost it, and would buy nothing, since the
     * building's own floor cap faces DOWN (ExtrudePoly emits it clockwise) and is culled
     * from above in any case.
     *
     * ⚠️ PER BUILDING AND NOT PER BLOCK, and there is deliberately no block-wide overload
     * left to call: a block carries several buildings since WP-O2, and founding all of them
     * at the block's lowest corner buries the higher ones by a median 6-7 m. A caller that
     * still has only a Quarter does not compile.
     */
    public static float BaseHeightOf(Quarter quarter, Building building)
        => _pavementOf(MinGroundOf(quarter, building));


    /**
     * The height to build a house of the given design height to, so that it still stands
     * that height above the ground.
     *
     * Sinking the base to the lowest ground under the footprint would otherwise swallow the
     * building from the uphill side: measured on the shipped terrain, without this the roof
     * of 64 of 149 buildings in Yelukhdidru/3000 fell below the block floor somewhere over
     * its own footprint, and the median 24 m building showed 4.5 m above the ground at its
     * highest corner. Adding the floor's own spread over the footprint puts the roof
     * exactly its design height above the HIGHEST ground it stands on, for the same reason
     * the base is the lowest.
     *
     * Exactly zero on a flat block, where the whole floor is at one height.
     */
    public static float HeightOf(Quarter quarter, Building building, float designHeight)
    {
        var (lo, hi) = _boundsOf(quarter, building);
        return designHeight + (hi - lo);
    }


    /**
     * Which storey of a building on this block is the lowest one at or above the pavement
     * at a plan position.
     *
     * The owner's constraint is that a shop is reachable - at the same level as the
     * pavement or above it, never below - and the design steer was to align to storeys and
     * leave stairs out. So a shopfront rises in 3 m steps rather than ramping with the
     * kerb, and is at most one storey above the pavement in front of it (measured:
     * sill minus local pavement is 1.6 m at the median and below 3 m always, by
     * construction).
     *
     * Both the base and the pavement carry the same ClusterStreetHeight and
     * QuarterSidewalkOffset, so those cancel and this is a difference of ground heights.
     * That is not tidiness: it makes the storey exactly 0 on a flat city, rather than the
     * ceiling of a rounding error, which is what keeps every shopfront in the shipped flat
     * city on the vertex it is on today.
     */
    public static int StoreyAt(Quarter quarter, Building building, in Vector2 v2Cluster)
    {
        float rise = GroundAt(quarter, v2Cluster) - MinGroundOf(quarter, building);
        if (!(rise > 0f))
        {
            return 0;
        }

        return (int)Single.Ceiling(rise / StoryHeight);
    }


    /**
     * The ground height a shopfront at a plan position is aligned to: its own building's
     * founding level, raised by whole storeys until it clears the pavement in front of the
     * shop.
     *
     * Returned as a GROUND height, in the same terms Quarter.CornerGroundHeightAt answers
     * in, so that a caller which today adds its own constant to a ground height keeps
     * adding exactly that constant. That is what lets the shop window, the shop POI and
     * the TALE door each stay bit for bit where they are in the flat city while all three
     * follow the same storey on a slope.
     */
    public static float StoreyGroundAt(
        Quarter quarter, Building building, in Vector2 v2Cluster)
        => MinGroundOf(quarter, building)
           + StoreyAt(quarter, building, v2Cluster) * StoryHeight;


    /**
     * A shopfront's plan position: the middle of the strip it was cut from.
     */
    public static Vector2 PlanOf(ShopFront shopFront)
    {
        var p = shopFront.GetPoints();
        if (null == p || 0 == p.Count)
        {
            return Vector2.Zero;
        }

        Vector3 mid = (p[0] + p[^1]) / 2f;

        return new Vector2(mid.X, mid.Z);
    }


    private static float _pavementOf(float ground)
        => ground + world.MetaGen.ClusterStreetHeight + world.MetaGen.QuarterSidewalkOffset;
}
