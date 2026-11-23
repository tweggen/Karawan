using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace engine.tale;


/**
 * What an NPC has heard of the player, apart from what it knows first-hand (TALE S2,
 * KAR-496; plan docs/tale/plans/proposed/TALE-S2-GOSSIP.md §2.1).
 *
 * Not Trust[-1] and not a player_fact.*: having heard of the player does not make an NPC
 * "met", known or deviated. It travels in the cluster's social snapshot.
 *
 * Immutable. A change builds a new record and replaces NpcSchedule.Hearsay, only under
 * TALE's locks; readers on the logical thread read the current reference without locks and
 * never write. The weight fades from UpdatedAt and is computed on read (WeightAt).
 */
public sealed class PlayerHearsay
{
    /** What the NPC has heard: the regard scale of PlayerStandingCalculator.Regard, [-1, 1]. */
    public float Regard { get; }

    /** How firmly, at UpdatedAt, [0, 1]. Read it through WeightAt. */
    public float Weight { get; }

    /** Heard fact tags (hurt_someone, helped_someone, ...), sorted. */
    public IReadOnlyList<string> Facts { get; }

    /** When the NPC first heard of the player; it retells only a game day later (§2.2). */
    public DateTime HeardAt { get; }

    /** When the record last changed; the weight fades from here. */
    public DateTime UpdatedAt { get; }

    /** Hops from a first-hand source: 1 = heard from the victim or a witness. */
    public int Generation { get; }


    public PlayerHearsay(float regard, float weight, IEnumerable<string> facts,
        DateTime heardAt, DateTime updatedAt, int generation)
    {
        Regard = Math.Clamp(regard, -1f, 1f);
        Weight = Math.Clamp(weight, 0f, 1f);
        Facts = (facts ?? Array.Empty<string>()).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToArray();
        HeardAt = heardAt;
        UpdatedAt = updatedAt;
        Generation = Math.Max(1, generation);
    }


    /** The weight at <paramref name="now"/>: halved every <paramref name="halfLifeDays"/>. */
    public float WeightAt(DateTime now, float halfLifeDays = GossipSettings.DefaultHalfLifeDays)
    {
        if (now <= UpdatedAt || halfLifeDays <= 0f) return Weight;
        double days = (now - UpdatedAt).TotalDays;
        return (float)(Weight * Math.Pow(0.5, days / halfLifeDays));
    }


    /** Whether the record still counts at <paramref name="now"/> (its weight above the floor). */
    public bool IsPresentAt(DateTime now, GossipSettings settings = null)
    {
        settings ??= GossipSettings.Default;
        return WeightAt(now, settings.HalfLifeDays) >= settings.DropBelow;
    }


    public bool Has(string fact)
    {
        foreach (var f in Facts) if (f == fact) return true;
        return false;
    }


    public JsonObject ToJson()
    {
        var facts = new JsonArray();
        foreach (var f in Facts) facts.Add(f);
        return new JsonObject
        {
            ["regard"] = Regard,
            ["weight"] = Weight,
            ["facts"] = facts,
            ["heardAtTicks"] = HeardAt.Ticks,
            ["updatedAtTicks"] = UpdatedAt.Ticks,
            ["generation"] = Generation,
        };
    }


    /** Null for a missing or unreadable record: an old save simply has no hearsay. */
    public static PlayerHearsay FromJson(JsonNode node)
    {
        if (node is not JsonObject jo) return null;
        try
        {
            var facts = new List<string>();
            if (jo["facts"] is JsonArray jf)
                foreach (var f in jf) if (f != null) facts.Add(f.GetValue<string>());
            return new PlayerHearsay(
                jo["regard"]?.GetValue<float>() ?? 0f,
                jo["weight"]?.GetValue<float>() ?? 0f,
                facts,
                new DateTime(jo["heardAtTicks"]?.GetValue<long>() ?? 0L),
                new DateTime(jo["updatedAtTicks"]?.GetValue<long>() ?? 0L),
                jo["generation"]?.GetValue<int>() ?? 1);
        }
        catch (Exception)
        {
            return null;
        }
    }
}


/** The tunables of gossip (plan §2.2–2.4, owner's answers in §6). */
public sealed class GossipSettings
{
    public const float DefaultHalfLifeDays = 3f;

    /** Every telling, the first included, carries this share of the teller's regard. */
    public float Relay = 0.5f;

    /** A telling weaker than this is not told. */
    public float MinTold = 0.15f;

    /** A listener's weight for a telling: this times its trust in the teller. */
    public float Listen = 0.5f;

    /** Chance that a first-hand fact is passed on. */
    public float FactTell = 0.8f;

    /** Chance that a heard fact is passed on. */
    public float FactRelay = 0.5f;

    public float HalfLifeDays = DefaultHalfLifeDays;

    /** A record whose weight fell below this counts as absent. */
    public float DropBelow = 0.05f;

    /**
     * Hearsay is retold only once it has been held this long (owner, 2026-11-22: witnesses
     * know at once, the first generation within a day, no further generation within it).
     * First-hand knowledge is told at once.
     */
    public TimeSpan RetellAfter = TimeSpan.FromDays(1);

    /*
     * Close ties hear without a chance meeting (owner, 2026-11-23: phones and social media):
     * a teller tells its strongest ties of at least CloseTie trust, at most MaxTiesTold of
     * them, each once per game day.
     */
    public float CloseTie = 0.5f;
    public int MaxTiesTold = 4;

    public static readonly GossipSettings Default = new();
}
