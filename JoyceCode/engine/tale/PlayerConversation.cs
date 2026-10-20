using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// What a finished conversation with the player does to the NPC (Phase C4):
/// its trust in the player (Trust[-1]) starts at FirstContactTrust and rises
/// by TrustPerConversation, and tale.npc.remember stores a player_fact.
/// Pure: TaleModule calls it and then marks the contact (KAR-433).
/// </summary>
public static class PlayerConversation
{
    public const float FirstContactTrust = 0.5f;
    public const float TrustPerConversation = 0.02f;


    /// <summary>Raise Trust[-1] for one ended conversation; returns the new value.</summary>
    public static float OnEnded(NpcSchedule npc)
    {
        npc.Trust ??= new Dictionary<int, float>();
        float current = npc.Trust.GetValueOrDefault(SocialMetricsCalculator.PlayerTrustKey, FirstContactTrust);
        float next = Math.Min(1f, current + TrustPerConversation);
        npc.Trust[SocialMetricsCalculator.PlayerTrustKey] = next;
        return next;
    }


    /// <summary>tale.npc.remember: the NPC remembers <paramref name="fact"/> about the player.</summary>
    public static void Remember(NpcSchedule npc, string fact)
    {
        npc.Properties ??= new Dictionary<string, float>();
        npc.Properties[$"player_fact.{fact}"] = 1f;
    }
}
