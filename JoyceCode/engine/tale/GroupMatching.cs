using System.Collections.Generic;
using System.Linq;

namespace engine.tale;

/// <summary>
/// Which old group a new community continues, by member overlap.
///
/// The one rule shared by <see cref="RuntimeSocialEvolver.Redetect"/> (which
/// keeps a group's id, name and hangout when its community survives a
/// re-detection) and <see cref="SocialMetricsHistory"/> (which needs to know
/// that a renumbered group is still the same group before it can count churn).
/// </summary>
public static class GroupMatching
{
    /// <summary>
    /// For each new roster, in order, the id of the old group it continues, or -1.
    ///
    /// Each new roster takes the old group with the largest member overlap that no
    /// earlier roster has claimed (ties go to the lower id); the match counts only
    /// if the overlap covers at least half of that old group's roster. Old groups
    /// are considered in the order given, which callers keep stable.
    /// </summary>
    public static int[] Match(
        IReadOnlyList<IReadOnlyCollection<int>> newRosters,
        IEnumerable<(int GroupId, IReadOnlyCollection<int> MemberIds)> oldGroups)
    {
        var old = oldGroups.ToList();
        var claimed = new HashSet<int>();
        var result = new int[newRosters.Count];

        for (int i = 0; i < newRosters.Count; i++)
        {
            var community = newRosters[i];
            int bestId = -1;
            int bestCount = 0;
            int bestOverlap = 0;
            foreach (var (groupId, members) in old)
            {
                if (claimed.Contains(groupId)) continue;
                int overlap = members.Count(community.Contains);
                if (overlap > bestOverlap ||
                    (overlap == bestOverlap && bestId != -1 && groupId < bestId))
                {
                    bestOverlap = overlap;
                    bestId = groupId;
                    bestCount = members.Count;
                }
            }

            if (bestId != -1 && bestOverlap * 2 >= bestCount)
            {
                claimed.Add(bestId);
                result[i] = bestId;
            }
            else
            {
                result[i] = -1;
            }
        }

        return result;
    }
}
