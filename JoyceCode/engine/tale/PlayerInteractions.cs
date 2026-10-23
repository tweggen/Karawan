using System;
using System.Collections.Generic;
using System.Numerics;
using static engine.Logger;

namespace engine.tale;

/// <summary>What one player interaction did to one NPC.</summary>
public readonly record struct PlayerInteractionResult(
    bool Applied, float TrustBefore, float TrustAfter, float Scale)
{
    public static readonly PlayerInteractionResult None = new(false, 0f, 0f, 0f);
}


/// <summary>What a world action of the player did: to the victim, and who saw it.</summary>
public sealed record PlayerActionReport(PlayerInteractionResult Victim, IReadOnlyList<Witness> Witnesses)
{
    public static readonly PlayerActionReport None = new(PlayerInteractionResult.None, Array.Empty<Witness>());
}


/// <summary>
/// TALE S1-b: applies an interaction with the player to an NPC — the victim
/// or partner (Apply) or a bystander who saw it (ApplyWitnessed). Trust is
/// written only through TaleManager.ApplyPlayerTrust. Times and distances
/// are parameters: no ECS, no clock, no randomness, so the result is
/// deterministic. Logical thread only, like every other schedule writer.
/// </summary>
public class PlayerInteractions
{
    private static readonly engine.Dc _dc = engine.Dc.TaleSocial;

    /// <summary>A witness of the victim's group reacts this much more strongly.</summary>
    public const float GroupmateFactor = 1.5f;

    private readonly TaleManager _manager;
    private readonly InteractionTypeRegistry _types;

    /*
     * Per (npc, key): when the last full application happened and how many
     * repeats followed inside its cooldown. Anti-farming state, not story
     * state: kept in memory only, never saved.
     */
    private readonly Dictionary<(int NpcId, string Key), (DateTime Since, int Repeats)> _cooldowns = new();


    public PlayerInteractions(TaleManager manager, InteractionTypeRegistry types)
    {
        _manager = manager;
        _types = types;
    }


    /// <summary>
    /// The player did <paramref name="interactionId"/> to the NPC. A known type
    /// without a player block falls back to its NPC–NPC trust change, without
    /// cooldown or properties. <paramref name="cashPaid"/> feeds WealthPerCash.
    /// </summary>
    public PlayerInteractionResult Apply(int npcId, string interactionId, DateTime gameNow, float cashPaid = 0f)
    {
        if (!_lookup(npcId, interactionId, out var npc, out var def)) return PlayerInteractionResult.None;

        var spec = def.Player;
        float trustDelta = spec?.TrustDelta ?? def.TrustDelta;
        float scale = spec == null ? 1f : _repeatScale(npcId, interactionId, spec, gameNow);

        // Money changes hands whatever the cooldown says; only the regard is rate-limited.
        if (spec != null && spec.WealthPerCash != 0f && cashPaid > 0f)
            _addProperty(npc, "wealth", cashPaid * spec.WealthPerCash);

        if (scale <= 0f)
        {
            Trace(_dc, $"player {interactionId} on npc={npcId}: repeat inside cooldown, no change");
            return new PlayerInteractionResult(false, PlayerTrust.Get(npc), PlayerTrust.Get(npc), 0f);
        }

        var result = _applyTrust(npc, trustDelta * scale, scale);
        if (spec != null)
        {
            _addProperties(npc, spec.TargetProperties, scale);
            _setFact(npc, spec.TargetFact);
        }

        Trace(_dc, $"player {interactionId} on npc={npcId}: trust {result.TrustBefore:F3} -> {result.TrustAfter:F3} "
                   + $"(scale {scale:F3}, cash {cashPaid:F0})");
        return result;
    }


    /// <summary>
    /// <paramref name="witnessId"/> saw the player do <paramref name="interactionId"/>
    /// to <paramref name="victimId"/> from <paramref name="distance"/> metres.
    /// Only types with a witness block have an effect; beyond its radius nothing happens.
    /// </summary>
    public PlayerInteractionResult ApplyWitnessed(int witnessId, int victimId, string interactionId,
        DateTime gameNow, float distance)
    {
        if (witnessId == victimId) return PlayerInteractionResult.None;
        if (!_lookup(witnessId, interactionId, out var witness, out var def)) return PlayerInteractionResult.None;

        var spec = def.Player;
        var w = spec?.Witness;
        if (w == null || w.Radius <= 0f || distance > w.Radius) return PlayerInteractionResult.None;

        float strength = WitnessStrength(w, distance, _areGroupmates(witness, _manager.GetSchedule(victimId)));
        float scale = _repeatScale(witnessId, "witness:" + interactionId, spec, gameNow);
        if (scale <= 0f) return new PlayerInteractionResult(false, PlayerTrust.Get(witness), PlayerTrust.Get(witness), 0f);

        var result = _applyTrust(witness, spec.TrustDelta * w.Factor * strength * scale, scale);
        _addProperties(witness, w.Properties, strength * scale);
        _setFact(witness, w.Fact);

        Trace(_dc, $"player {interactionId} on npc={victimId} witnessed by npc={witnessId} at {distance:F1} m: "
                   + $"trust {result.TrustBefore:F3} -> {result.TrustAfter:F3}");
        return result;
    }


    /// <summary>
    /// TALE S1-d: the player did <paramref name="interactionId"/> to
    /// <paramref name="victimId"/> at <paramref name="at"/>. Applies it to the
    /// victim and, if that counted (a repeat inside the cooldown does not),
    /// to the witnesses <see cref="WitnessSelector"/> picks from
    /// <paramref name="candidates"/>; <paramref name="lineOfSight"/> is asked
    /// only if the type's witness block wants it.
    /// </summary>
    public PlayerActionReport Report(int victimId, string interactionId, Vector3 at, DateTime gameNow,
        IEnumerable<(int NpcId, Vector3 Position)> candidates, Func<int, Vector3, bool> lineOfSight = null)
    {
        var victim = Apply(victimId, interactionId, gameNow);
        if (!victim.Applied) return new PlayerActionReport(victim, Array.Empty<Witness>());

        var w = _types.Get(interactionId).Player?.Witness;
        if (w == null || w.Radius <= 0f) return new PlayerActionReport(victim, Array.Empty<Witness>());

        var witnesses = WitnessSelector.Select(at, candidates, victimId, w.Radius, w.MaxCount,
            w.LineOfSight ? lineOfSight : null);
        foreach (var witness in witnesses)
            ApplyWitnessed(witness.NpcId, victimId, interactionId, gameNow, witness.Distance);
        return new PlayerActionReport(victim, witnesses);
    }


    /// <summary>
    /// Distance and group weighting of a witness (Factor not included):
    /// 1 at the event, 0.5 at the radius, × GroupmateFactor for the victim's groupmates.
    /// </summary>
    public static float WitnessStrength(PlayerWitnessSpec w, float distance, bool groupmate)
    {
        float falloff = 1f - 0.5f * Math.Clamp(distance / w.Radius, 0f, 1f);
        return falloff * (groupmate ? GroupmateFactor : 1f);
    }


    private bool _lookup(int npcId, string interactionId, out NpcSchedule npc, out InteractionTypeDefinition def)
    {
        npc = _manager.GetSchedule(npcId);
        def = string.IsNullOrEmpty(interactionId) || _types == null || !_types.Has(interactionId)
            ? null : _types.Get(interactionId);
        if (def == null)
        {
            Warning(_dc, $"Player interaction '{interactionId}' is not a known interaction type; npc={npcId} unchanged.");
            return false;
        }
        if (npc == null)
        {
            Warning(_dc, $"Player interaction '{interactionId}' on npc={npcId}, which has no schedule; nothing changed.");
            return false;
        }
        return true;
    }


    /*
     * 1 for the first application, then RepeatScale^n for the n-th repeat
     * while the cooldown since that first application runs; after it, a
     * fresh full application.
     */
    private float _repeatScale(int npcId, string key, PlayerInteractionSpec spec, DateTime gameNow)
    {
        if (spec.CooldownMinutes is not { } minutes || minutes <= 0f) return 1f;

        var k = (npcId, key);
        if (_cooldowns.TryGetValue(k, out var entry)
            && gameNow >= entry.Since && gameNow < entry.Since.AddMinutes(minutes))
        {
            int n = entry.Repeats + 1;
            _cooldowns[k] = (entry.Since, n);
            return MathF.Pow(spec.RepeatScale, n);
        }
        _cooldowns[k] = (gameNow, 0);
        return 1f;
    }


    /* A positive change is flattened by (1 − trust): it shrinks as trust nears 1. */
    private PlayerInteractionResult _applyTrust(NpcSchedule npc, float delta, float scale)
    {
        float before = PlayerTrust.Get(npc);
        if (delta > 0f) delta *= 1f - before;
        float after = _manager.ApplyPlayerTrust(npc, delta);
        return new PlayerInteractionResult(true, before, after, scale);
    }


    private static void _addProperties(NpcSchedule npc, Dictionary<string, float> properties, float scale)
    {
        if (properties == null) return;
        foreach (var (key, value) in properties) _addProperty(npc, key, value * scale);
    }


    private static void _addProperty(NpcSchedule npc, string key, float delta)
    {
        npc.Properties ??= new Dictionary<string, float>();
        float current = npc.Properties.TryGetValue(key, out float v) ? v : 0.5f;
        npc.Properties[key] = Math.Clamp(current + delta, 0f, 1f);
    }


    private void _setFact(NpcSchedule npc, string fact)
    {
        if (string.IsNullOrEmpty(fact)) return;
        PlayerConversation.Remember(npc, fact);
        _manager.MarkPlayerContact(npc);
    }


    private static bool _areGroupmates(NpcSchedule a, NpcSchedule b) =>
        a != null && b != null && a.GroupId >= 0 && a.GroupId == b.GroupId && a.ClusterIndex == b.ClusterIndex;
}
