using System;
using System.Collections.Generic;

namespace engine.tale;


/**
 * TALE S2 (KAR-498): how far opinions of the player have travelled in a set of NPCs - the
 * instrument of S2-d's reach measurement and the TALE panel. Pure; the clock and the group
 * of interest are parameters. Only present hearsay counts (weight above the floor at
 * <c>gameNow</c>); NPCs that know the player first-hand count separately.
 */
public sealed record HearsayReading(
    int NpcCount,
    int Holders,
    int HoldersInGroup,
    int HoldersOutsideGroup,
    int KnowFirstHand,
    IReadOnlyDictionary<int, int> ByGeneration,
    IReadOnlyList<int> AbsRegardQuartiles)
{
    public double Share => NpcCount > 0 ? (double)Holders / NpcCount : 0.0;

    public string ToLogLine()
    {
        var gens = new List<string>();
        foreach (var (g, n) in ByGeneration) gens.Add($"gen {g}: {n}");
        return $"hearsay {Holders}/{NpcCount} ({Share:P1}; in group {HoldersInGroup}, outside {HoldersOutsideGroup}; " +
               $"{string.Join(", ", gens)}; |regard| quartiles {string.Join("/", AbsRegardQuartiles)}), " +
               $"first-hand {KnowFirstHand}";
    }
}


public static class HearsayMetrics
{
    public static HearsayReading Measure(IEnumerable<NpcSchedule> npcs, DateTime gameNow, int groupId = -1)
    {
        int count = 0, holders = 0, inGroup = 0, outside = 0, firstHand = 0;
        var byGen = new SortedDictionary<int, int>();
        var quartiles = new int[4];
        foreach (var npc in npcs)
        {
            if (npc == null) continue;
            count++;
            if (PlayerTrust.Knows(npc)) firstHand++;
            var h = npc.Hearsay;
            if (h == null || !h.IsPresentAt(gameNow)) continue;
            holders++;
            if (groupId >= 0 && npc.GroupId == groupId) inGroup++;
            else outside++;
            byGen[h.Generation] = byGen.GetValueOrDefault(h.Generation) + 1;
            quartiles[Math.Min(3, (int)(Math.Abs(h.Regard) * 4f))]++;
        }
        return new HearsayReading(count, holders, inGroup, outside, firstHand, byGen, quartiles);
    }
}
