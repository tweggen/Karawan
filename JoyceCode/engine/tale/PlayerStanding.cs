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
