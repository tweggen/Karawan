using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules.satnav.desc;
using engine.navigation;

namespace builtin.modules.satnav;


/**
 * Planning one route: two cursors, an A* between them, and the truncation of the last
 * lane at the target.
 *
 * Separate from Route because Route is engine plumbing - it hops onto the logical thread
 * to search and back onto it to deliver - and nothing that needs a booted engine can be
 * exercised. Everything here needs only a NavCluster, so the part that decides which
 * network the player is sent over is testable, and a hardcoded transport type in it fails
 * a test instead of being drawn on the pavement for a year.
 */
internal static class RoutePlan
{
    /**
     * @param transportType
     *     Which network to plan over. The SAME one is handed to the cursors and to the
     *     pathfinder deliberately: a cursor refuses to return a lane of the wrong kind, so
     *     mixing the two does not route badly, it finds no route at all.
     *
     * @returns
     *     The lanes to follow, or null when there is no path.
     */
    internal static async Task<List<NavLane>> PlanAsync(
        NavCluster ncTop, Vector3 v3From, Vector3 v3To, TransportationType transportType)
    {
        var navCursors = await Task.WhenAll(
            ncTop.TryCreateCursor(v3From, transportType),
            ncTop.TryCreateCursor(v3To, transportType)
        );

        var listLanes = new LocalPathfinder(
            navCursors[0], navCursors[1], transportType).Pathfind();

        if (null == listLanes || 0 == listLanes.Count)
        {
            return listLanes;
        }

        TruncateAtTarget(listLanes, v3To);

        return listLanes;
    }


    /**
     * Cut the route short where it passes closest to the target.
     *
     * The last two lanes are both considered because the nearest junction to a target is
     * routinely just past it, so the route overshoots and comes back.
     */
    internal static void TruncateAtTarget(List<NavLane> listLanes, Vector3 v3Target)
    {
        int bestIdx = listLanes.Count - 1;
        float bestDist = Single.MaxValue;
        Vector3 bestProj = listLanes[^1].End.Position;

        int startCheck = Math.Max(0, listLanes.Count - 2);
        for (int i = startCheck; i < listLanes.Count; i++)
        {
            var lane = listLanes[i];
            Vector3 ab = lane.End.Position - lane.Start.Position;
            Vector3 ap = v3Target - lane.Start.Position;
            float abLenSq = Vector3.Dot(ab, ab);
            float t = abLenSq > 0f
                ? Math.Clamp(Vector3.Dot(ap, ab) / abLenSq, 0f, 1f)
                : 0f;
            Vector3 proj = lane.Start.Position + t * ab;
            float dist = (v3Target - proj).Length();
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i;
                bestProj = proj;
            }
        }

        while (listLanes.Count > bestIdx + 1)
        {
            listLanes.RemoveAt(listLanes.Count - 1);
        }

        /*
         * The synthesised end junction goes through the factory, so it carries a ground
         * height consistent with its position rather than a zero nobody would notice.
         */
        NavJunction njEnd = NavJunction.AtNavigationHeight(bestProj);
        var bestLane = listLanes[bestIdx];
        listLanes[bestIdx] = new NavLane()
        {
            Start = bestLane.Start,
            End = njEnd,
            Length = (bestProj - bestLane.Start.Position).Length(),
            MaxSpeed = bestLane.MaxSpeed,

            /*
             * ⚠️ The truncated lane is the SAME piece of road, shortened - so it runs along
             * the same carriageway, and losing this made the LAST lane of every route fall
             * back to the chord between its two junctions: exactly the defect §7r removed
             * from all the other lanes, on the segment nearest the destination.
             *
             * §7r's own note on this junction said the opposite - *"the lane it belongs to
             * is the real one, so the ribbon's last quad is right and only the synthetic
             * junction's own field is off"* - and it is not the real one, it is a new
             * object with a new lane's defaults.
             *
             * Nothing moves in a flat city: a level surface answers one height everywhere,
             * which is what the chord between two equal junction heights answered before.
             *
             * KerbSide and AllowedTypes are deliberately NOT copied here, and that is a
             * separate open defect rather than an oversight - restoring a pedestrian lane's
             * kerb side would move the satnav walker's last waypoint by 1.5 m in the SHIPPED
             * FLAT city (§7g), which is a move to be measured and stated on its own.
             */
            Surface = bestLane.Surface
        };
    }
}
