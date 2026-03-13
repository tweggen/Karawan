using System;
using System.Collections.Generic;

namespace engine.tale;

public struct PresenceWindow
{
    public int NpcId;
    public DateTime From;
    public DateTime To;
}

public struct EncounterEvent
{
    public int NpcA;
    public int NpcB;
    public int LocationId;
    public DateTime Time;
}

/// <summary>
/// Probabilistic encounter detection from overlapping time-space windows.
/// Stay encounters: P = 1 - (1 - p_loc)^(overlap / quantum).
/// Per-pair-per-day dedup prevents duplicate encounters.
/// Uses active-presence tracking for O(active NPCs) per check.
/// </summary>
public class EncounterResolver
{
    public float P_Venue = 0.07f;
    public float P_Street = 0.015f;
    public float P_Transport = 0.002f;
    public float P_Workplace = 0.04f;
    public float TimeQuantumMinutes = 15f;

    // Active presences: locationId → list of (NpcId, From, To)
    // Only contains presences that haven't ended yet relative to current time
    private readonly Dictionary<int, List<PresenceWindow>> _activeByLocation = new();
    private readonly HashSet<long> _dailyPairDedup = new();
    private readonly List<(int otherId, DateTime time)> _encounterResults = new();
    public int TotalEncounters { get; private set; }


    private static long PairKey(int a, int b)
    {
        int min = Math.Min(a, b);
        int max = Math.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }


    public void RegisterPresence(int npcId, int locationId, DateTime from, DateTime to)
    {
        if (!_activeByLocation.TryGetValue(locationId, out var list))
        {
            list = new List<PresenceWindow>(16);
            _activeByLocation[locationId] = list;
        }
        list.Add(new PresenceWindow { NpcId = npcId, From = from, To = to });
    }


    /// <summary>
    /// Check for encounters between the given NPC and all other NPCs present
    /// at the same location during overlapping time windows.
    /// Returns list of (otherNpcId, encounterTime) via a reusable buffer.
    /// Lazily prunes expired presences during scan.
    /// </summary>
    public IReadOnlyList<(int otherId, DateTime time)> CheckEncounters(
        int npcId, int locationId, DateTime from, DateTime to,
        string locationType, Random rng)
    {
        _encounterResults.Clear();
        if (!_activeByLocation.TryGetValue(locationId, out var list)) return _encounterResults;

        float pBase = locationType switch
        {
            "social_venue" => P_Venue,
            "workplace" => P_Workplace,
            "street_segment" => P_Street,
            _ => P_Street
        };

        // Scan with inline compaction: remove expired entries
        int writeIdx = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var presence = list[i];

            // Prune expired presences (ended before our start)
            if (presence.To <= from)
                continue;

            // Compact: keep this entry
            if (writeIdx != i)
                list[writeIdx] = presence;
            writeIdx++;

            if (presence.NpcId == npcId) continue;

            // Daily pair dedup
            long pairKey = PairKey(npcId, presence.NpcId);
            if (_dailyPairDedup.Contains(pairKey)) continue;

            // Compute temporal overlap
            var overlapStart = from > presence.From ? from : presence.From;
            var overlapEnd = to < presence.To ? to : presence.To;
            if (overlapStart >= overlapEnd) continue;

            float overlapMinutes = (float)(overlapEnd - overlapStart).TotalMinutes;
            float p = 1f - MathF.Pow(1f - pBase, overlapMinutes / TimeQuantumMinutes);

            if (rng.NextDouble() < p)
            {
                TotalEncounters++;
                _dailyPairDedup.Add(pairKey);
                _encounterResults.Add((presence.NpcId, overlapStart));
            }
        }

        // Trim list to compacted size
        if (writeIdx < list.Count)
            list.RemoveRange(writeIdx, list.Count - writeIdx);

        return _encounterResults;
    }


    public void ClearDailyDedup()
    {
        _dailyPairDedup.Clear();
    }


    public void ClearBefore(DateTime cutoff)
    {
        foreach (var list in _activeByLocation.Values)
        {
            int writeIdx = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].To >= cutoff)
                {
                    if (writeIdx != i)
                        list[writeIdx] = list[i];
                    writeIdx++;
                }
            }
            if (writeIdx < list.Count)
                list.RemoveRange(writeIdx, list.Count - writeIdx);
        }
    }
}
