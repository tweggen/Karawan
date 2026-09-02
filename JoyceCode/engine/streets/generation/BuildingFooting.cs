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
 * **Why the block's own corners are enough.** Everything on a block hangs off the heights
 * of its corner junctions:
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
 * So every vertex of the floor's cap carries a corner height or a blend of two, and a
 * piecewise linear surface over those vertices is bounded below by the LOWEST corner and
 * above by the highest. The bound needs no reference to the mesh, holds for any height
 * source, and is exact.
 *
 * **Over the whole block rather than over the building's own footprint**, because measured
 * over the baselines a block carries exactly ONE estate and an estate at most ONE building
 * - 1x82, 1x445 estates per block; 149 buildings on 445 blocks, never two on one. An
 * estate IS the block outline inset by Quarter.SidewalkWidth (1-6 m), so a footprint spans
 * essentially the whole block: the exact minimum of the cap over a footprint sits only
 * 0.19-0.61 m above this bound at the median, 1.5 m at p90, 3.7 m at the worst building of
 * the four cities. Should blocks ever carry several buildings, that slack becomes the
 * amount by which a small building on a large block is over-sunk, and the bound has to be
 * taken over the footprint instead.
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
     * The block floor is the boundary ring raised by ClusterStreetHeight and extruded up
     * by QuarterSidewalkOffset, and its top face IS the pavement - there is no separate
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
     * The one planar level every building on this block is founded at.
     *
     * At or below the pavement everywhere on the block, with equality only at the block's
     * lowest corner - where the two surfaces are tangent at a point rather than coplanar,
     * so there is nothing for the depth buffer to fight over. No margin is subtracted: a
     * margin would move the shipped flat city by more than the 0.35 m this change already
     * costs it, and would buy nothing, since the building's own floor cap faces DOWN
     * (ExtrudePoly emits it clockwise) and is culled from above in any case.
     */
    public static float BaseHeightOf(Quarter quarter)
        => _pavementOf(MinGroundOf(quarter));


    /**
     * The height to build a house of the given design height to, so that it still stands
     * that height above the ground.
     *
     * Sinking the base to the block's lowest corner would otherwise swallow the building
     * from the uphill side: measured on the shipped terrain, without this the roof of
     * 64 of 149 buildings in Yelukhdidru/3000 falls below the block floor somewhere over
     * its own footprint, and the median 24 m building shows 4.5 m above the ground at its
     * highest corner. Adding the block's corner spread puts the roof exactly its design
     * height above the block's HIGHEST corner, which is the upper bound of the floor
     * surface for the same reason the base is the lower one.
     *
     * Exactly zero on a flat block, where every corner is at one height.
     */
    public static float HeightOf(Quarter quarter, float designHeight)
        => designHeight + (MaxGroundOf(quarter) - MinGroundOf(quarter));


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
    public static int StoreyAt(Quarter quarter, in Vector2 v2Cluster)
    {
        float rise = GroundAt(quarter, v2Cluster) - MinGroundOf(quarter);
        if (!(rise > 0f))
        {
            return 0;
        }

        return (int)Single.Ceiling(rise / StoryHeight);
    }


    /**
     * The ground height a shopfront at a plan position is aligned to: the block's lowest
     * corner, raised by whole storeys until it clears the pavement in front of the shop.
     *
     * Returned as a GROUND height, in the same terms Quarter.CornerGroundHeightAt answers
     * in, so that a caller which today adds its own constant to a ground height keeps
     * adding exactly that constant. That is what lets the shop window, the shop POI and
     * the TALE door each stay bit for bit where they are in the flat city while all three
     * follow the same storey on a slope.
     */
    public static float StoreyGroundAt(Quarter quarter, in Vector2 v2Cluster)
        => MinGroundOf(quarter) + StoreyAt(quarter, v2Cluster) * StoryHeight;


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
