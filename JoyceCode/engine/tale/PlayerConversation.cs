using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// What a finished conversation with the player does to the NPC (Phase C4):
/// its trust in the player rises by TrustPerConversation, from
/// PlayerTrust.FirstContact on the first one (S1-a), and tale.npc.remember
/// stores a player_fact. S1-c replaces the flat step with the chat interaction.
/// </summary>
public static class PlayerConversation
{
    public const float TrustPerConversation = 0.02f;


    /// <summary>Raise the NPC's trust in the player for one ended conversation; returns the new value.</summary>
    public static float OnEnded(TaleManager manager, NpcSchedule npc)
        => manager.ApplyPlayerTrust(npc, TrustPerConversation);


    /// <summary>tale.npc.remember: the NPC remembers <paramref name="fact"/> about the player.</summary>
    public static void Remember(NpcSchedule npc, string fact)
    {
        npc.Properties ??= new Dictionary<string, float>();
        npc.Properties[$"player_fact.{fact}"] = 1f;
    }
}
