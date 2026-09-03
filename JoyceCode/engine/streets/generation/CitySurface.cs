using System.Collections.Generic;
using System.Numerics;

namespace engine.streets.generation;


/**
 * How high the surface a city is BUILT on is, at a plan position, rather than how high the
 * terrain under it is.
 *
 * The two are different quantities and the difference is not small. Street heights are
 * relaxed to buildable gradients, so a road cuts into the hill it crosses and stands proud
 * of the dip it spans, and the conforming pass (§2c) can only pull the ground back toward
 * them on a 20 m grid with a 60 m smoothstep. Measured at every junction of the four
 * baseline cities on the shipped terrain, the conformed TERRAIN sits between 9.1 m above
 * and 8.3 m below the pavement of the junction standing on it, and is below it at 88-93 %
 * of them. So anything that is meant to sit ON the city and asks
 * ClusterDesc.GroundHeightAt is asking the wrong question - which is how the quest marker
 * came to be drawn half a metre into the road at the median junction and 9.8 m into it at
 * the worst.
 *
 * A junction is the one place in a city where "how high is the built surface here" has an
 * exact answer rather than a sample near one: it is one node of the stroke graph with one
 * height, and every stroke, junction cap, kerb and block corner meeting there reads that
 * same number. That is what this answers from.
 *
 * **Nearest junction, not nearest point on the road.** Everything that asks this today is
 * placed AT a junction - engine.Placer with Reference.StreetPoint puts a quest destination
 * exactly on sp.Pos3 - so the nearest junction is the junction it was placed at and the
 * answer is exact, which is a property a test can assert by identity. Away from a junction
 * the answer degrades to the nearest one's height, and the honest bound on that is the
 * grade policy: at most 14 % of the distance to it. A caller standing somewhere else
 * should say so and get a better answer built for it, rather than this quietly becoming a
 * road query it is not.
 */
public static class CitySurface
{
    /**
     * The junction nearest a plan position, in cluster relative coordinates.
     *
     * Ties are broken by the lower Id, so that a position exactly between two junctions -
     * which a hand written test will produce and a generated city will not - answers the
     * same junction on every run and on every machine.
     */
    public static StreetPoint NearestJunctionTo(
        IReadOnlyList<StreetPoint> junctions, in Vector2 v2Cluster)
    {
        StreetPoint best = null;
        float bestD2 = float.MaxValue;

        int n = null == junctions ? 0 : junctions.Count;
        for (int i = 0; i < n; ++i)
        {
            StreetPoint sp = junctions[i];
            if (null == sp) continue;

            float d2 = (sp.Pos - v2Cluster).LengthSquared();
            if (d2 < bestD2 || (d2 == bestD2 && null != best && sp.Id < best.Id))
            {
                bestD2 = d2;
                best = sp;
            }
        }

        return best;
    }


    /**
     * The world height of the highest thing built at a junction.
     *
     * The carriageway is RoadSurface.HeightAtJunction - the junction cap's own height,
     * which is what the deck, the cap fan and the cap's collider are all built at - and the
     * pavements of the blocks that corner on it are exactly one kerb above that, because a
     * block floor's outline takes each corner's own junction height (§7c) and the floor is
     * extruded up by QuarterSidewalkOffset (§7j). So the pavement is the upper of the two
     * and anything resting on it clears both.
     */
    public static float HeightAtJunction(IStreetHeightSource heights, in StreetPoint sp)
        => RoadSurface.HeightAtJunction(heights, sp)
           + world.MetaGen.QuarterSidewalkOffset;


    /**
     * The world height of the built city surface at a plan position in cluster relative
     * coordinates, taken from the junction nearest it.
     *
     * @param distance
     *     How far that junction actually is, so a caller can tell an exact answer from an
     *     extrapolated one instead of having to trust the whole city is junctions.
     * @returns
     *     false where the city has no junctions at all, in which case there is no built
     *     surface to stand on and the caller keeps whatever it had.
     */
    public static bool TryHeightAt(
        IStreetHeightSource heights, IReadOnlyList<StreetPoint> junctions,
        in Vector2 v2Cluster, out float height, out float distance)
    {
        StreetPoint sp = NearestJunctionTo(junctions, v2Cluster);
        if (null == sp)
        {
            height = 0f;
            distance = float.MaxValue;
            return false;
        }

        height = HeightAtJunction(heights, sp);
        distance = (sp.Pos - v2Cluster).Length();
        return true;
    }
}
