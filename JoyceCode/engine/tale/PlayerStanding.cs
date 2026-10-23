using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// An NPC's trust in the player, Trust[-1] (TALE S1-a). No entry means the NPC
/// does not know the player; the first contact starts at FirstContact, the
/// stranger/acquaintance boundary in nogame.relationship-tiers.json. Every
/// write goes through TaleManager.ApplyPlayerTrust, which uses Apply below.
/// </summary>
public static class PlayerTrust
{
    public const int Key = -1;

    /// <summary>Where trust starts at first contact, before the interaction's change.</summary>
    public const float FirstContact = 0.15f;


    /// <summary>The NPC has a trust entry for the player.</summary>
    public static bool Knows(NpcSchedule npc) => npc?.Trust != null && npc.Trust.ContainsKey(Key);


    /// <summary>The NPC's trust in the player: its entry, or FirstContact if it has none.</summary>
    public static float Get(NpcSchedule npc) =>
        npc?.Trust != null && npc.Trust.TryGetValue(Key, out float t) ? t : FirstContact;


    /// <summary>
    /// Create the entry at FirstContact if needed, add <paramref name="delta"/>,
    /// clamp to [0, 1]; returns the new value. Only TaleManager.ApplyPlayerTrust
    /// calls this, so the contact is always marked.
    /// </summary>
    internal static float Apply(NpcSchedule npc, float delta)
    {
        npc.Trust ??= new Dictionary<int, float>();
        float next = Math.Clamp(Get(npc) + delta, 0f, 1f);
        npc.Trust[Key] = next;
        return next;
    }
}


/// <summary>A group's standing with the player and how many of its members know the player.</summary>
public readonly record struct GroupStanding(float Standing, int Known)
{
    public static readonly GroupStanding None = new(0f, 0);
}


/// <summary>
/// TALE S1-e: a group's standing with the player, from what its members think
/// of the player. Pure; computed on demand, nothing cached or saved.
/// </summary>
public static class PlayerStandingCalculator
{
    /// <summary>K: the standing is shrunk towards 0 by one neutral member's worth of prior.</summary>
    public const float PriorWeight = 1f;

    /// <summary>A member's weight is WeightBase + the other members' mean trust in them.</summary>
    public const float WeightBase = 0.25f;


    /// <summary>
    /// A member's regard for the player in [−1, 1], around the first-contact
    /// value B0: (t − B0)/B0 below, (t − B0)/(1 − B0) above. Harm weighs more
    /// per unit of trust than help near the baseline.
    /// </summary>
    public static float Regard(float trust)
    {
        const float b0 = PlayerTrust.FirstContact;
        float r = trust < b0 ? (trust - b0) / b0 : (trust - b0) / (1f - b0);
        return Math.Clamp(r, -1f, 1f);
    }


    /// <summary>
    /// Standing over the members who know the player:
    /// Σ ŵ_i·r(t_i) / (Σ ŵ_i + K), where ŵ is each member's standing inside the
    /// group (WeightBase + the others' mean trust in them, normalised to mean 1
    /// over the roster). 0 when nobody knows the player; the group's size does
    /// not dilute it. Known counts the members who know the player, except
    /// <paramref name="excludeId"/> (the speaker). Null entries (members without
    /// a schedule) are skipped.
    /// </summary>
    public static GroupStanding ForGroup(IReadOnlyList<NpcSchedule> members, int excludeId = int.MinValue)
    {
        if (members == null) return GroupStanding.None;
        var roster = new List<NpcSchedule>(members.Count);
        foreach (var m in members) if (m != null) roster.Add(m);
        if (roster.Count == 0) return GroupStanding.None;

        var weights = new float[roster.Count];
        float weightSum = 0f;
        for (int i = 0; i < roster.Count; i++)
        {
            float trustSum = 0f;
            int others = 0;
            for (int j = 0; j < roster.Count; j++)
            {
                if (i == j) continue;
                others++;
                if (roster[j].Trust != null && roster[j].Trust.TryGetValue(roster[i].NpcId, out float t))
                    trustSum += t;
            }
            weights[i] = WeightBase + (others > 0 ? trustSum / others : 0f);
            weightSum += weights[i];
        }
        float normalise = roster.Count / weightSum;

        float sum = 0f, knownWeight = 0f;
        int known = 0;
        for (int i = 0; i < roster.Count; i++)
        {
            if (!PlayerTrust.Knows(roster[i])) continue;
            float w = weights[i] * normalise;
            sum += w * Regard(PlayerTrust.Get(roster[i]));
            knownWeight += w;
            if (roster[i].NpcId != excludeId) known++;
        }
        return knownWeight == 0f
            ? GroupStanding.None
            : new GroupStanding(sum / (knownWeight + PriorWeight), known);
    }
}
