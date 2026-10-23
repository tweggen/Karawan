using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// TALE S1-b: what an interaction type does to an NPC when the player is the
/// other side. The optional "player" block of an entry in
/// nogame.interactions.json; it applies only NPC→player (the player has no
/// trust store). Read by PlayerInteractions.
/// </summary>
public class PlayerInteractionSpec
{
    /// <summary>Change of the NPC's trust in the player; overrides the type's NPC–NPC TrustDelta.</summary>
    public float TrustDelta { get; set; }

    /// <summary>Added to the NPC's properties (e.g. fear, anger, health), clamped to [0, 1].</summary>
    public Dictionary<string, float> TargetProperties { get; set; }

    /// <summary>Sets player_fact.&lt;TargetFact&gt; = 1 on the NPC.</summary>
    public string TargetFact { get; set; }

    /// <summary>
    /// Game minutes after a full application during which repeats are scaled
    /// by RepeatScale^n. Null: no rate limit.
    /// </summary>
    public float? CooldownMinutes { get; set; }

    /// <summary>Scale of the n-th repeat inside the cooldown is RepeatScale^n; 0 drops repeats.</summary>
    public float RepeatScale { get; set; } = 1f;

    /// <summary>Wealth added per unit of cash the player paid (pay, bribe).</summary>
    public float WealthPerCash { get; set; }

    /// <summary>What bystanders take from it; null: nobody else reacts.</summary>
    public PlayerWitnessSpec Witness { get; set; }
}


/// <summary>
/// TALE S1-b: how a bystander reacts to a player interaction it witnessed.
/// Strength = Factor × (1 − 0.5·d/Radius), × 1.5 for the victim's groupmates.
/// </summary>
public class PlayerWitnessSpec
{
    /// <summary>Witness's trust change as a share of the victim's.</summary>
    public float Factor { get; set; }

    /// <summary>Metres around the event within which NPCs witness it.</summary>
    public float Radius { get; set; }

    /// <summary>At most this many witnesses, nearest first.</summary>
    public int MaxCount { get; set; } = 8;

    /// <summary>Sets player_fact.&lt;Fact&gt; = 1 on the witness (e.g. "saw_assault").</summary>
    public string Fact { get; set; }

    /// <summary>Added to the witness's properties, scaled by distance and group, clamped to [0, 1].</summary>
    public Dictionary<string, float> Properties { get; set; }

    /// <summary>Only witnesses with a clear line of sight count.</summary>
    public bool LineOfSight { get; set; } = true;
}
