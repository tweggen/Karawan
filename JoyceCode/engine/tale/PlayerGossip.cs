using System;
using System.Collections.Generic;

namespace engine.tale;


/**
 * Opinions of the player passed on when two NPCs meet (TALE S2, KAR-496; plan
 * docs/tale/plans/proposed/TALE-S2-GOSSIP.md §2.2, §2.3). Pure: it reads both NPCs and
 * replaces their PlayerHearsay records; it never writes Trust[-1] or a player_fact.*.
 *
 * Called by all three encounter sites (S2-b) under their locks. Draws from the given
 * source only when at least one side has something to tell, so a run without any player
 * entry draws nothing - the bake stays as it is.
 */
public static class PlayerGossip
{
    /*
     * The facts a first-hand fact becomes when told. A bribe is never told (owner,
     * 2026-11-22); nothing not listed here travels either.
     */
    private static readonly (string FirstHand, string Heard)[] _factMap =
    {
        ("player_fact.hit_me", "hurt_someone"),
        ("player_fact.saw_assault", "hurt_someone"),
        ("player_fact.ran_me_over", "ran_someone_over"),
        ("player_fact.saw_run_over", "ran_someone_over"),
        ("player_fact.helped_me", "helped_someone"),
        ("player_fact.paid_me", "pays_people"),
    };


    /** What one NPC tells: the regard, its generation, and the candidate facts with their odds. */
    private readonly record struct Telling(float Told, int Generation, List<(string Fact, float Chance)> Facts);


    private static Telling? _tellingOf(NpcSchedule npc, DateTime time, GossipSettings s)
    {
        float source;
        int generation;
        var facts = new List<(string, float)>();

        if (PlayerTrust.Knows(npc))
        {
            source = PlayerStandingCalculator.Regard(PlayerTrust.Get(npc));
            generation = 0;
            if (npc.Properties != null)
            {
                foreach (var (firstHand, heard) in _factMap)
                {
                    if (npc.Properties.TryGetValue(firstHand, out float v) && v > 0f
                        && !facts.Exists(f => f.Item1 == heard))
                        facts.Add((heard, s.FactTell));
                }
            }
        }
        else
        {
            var heard = npc.Hearsay;
            if (heard == null || !heard.IsPresentAt(time, s)) return null;
            if (time - heard.HeardAt < s.RetellAfter) return null;
            source = heard.Regard;
            generation = heard.Generation;
        }

        // Heard facts travel too, at their own odds, from knowers and hearers alike.
        var held = npc.Hearsay;
        if (held != null && held.IsPresentAt(time, s))
        {
            foreach (var f in held.Facts)
                if (!facts.Exists(x => x.Item1 == f)) facts.Add((f, s.FactRelay));
        }

        float told = s.Relay * source;
        if (Math.Abs(told) < s.MinTold) return null;
        return new Telling(told, generation, facts);
    }


    /**
     * The two NPCs met at <paramref name="time"/>. trustAB is a's trust in b, trustBA b's in
     * a (the DES passes its symmetric pair trust for both). Both tellings come from the
     * state before the exchange. Returns whether each side's record changed.
     */
    public static (bool AChanged, bool BChanged) Exchange(
        NpcSchedule a, NpcSchedule b, float trustAB, float trustBA, DateTime time, Random rng,
        GossipSettings settings = null)
    {
        if (a == null || b == null || ReferenceEquals(a, b)) return (false, false);
        var s = settings ?? GossipSettings.Default;

        var fromA = _tellingOf(a, time, s);
        var fromB = _tellingOf(b, time, s);
        if (fromA == null && fromB == null) return (false, false);

        bool bChanged = fromA != null && _listen(b, fromA.Value, trustBA, time, rng, s);
        bool aChanged = fromB != null && _listen(a, fromB.Value, trustAB, time, rng, s);
        return (aChanged, bChanged);
    }


    private static bool _listen(NpcSchedule listener, Telling t, float trustInTeller, DateTime time,
        Random rng, GossipSettings s)
    {
        float w = s.Listen * Math.Clamp(trustInTeller, 0f, 1f);
        if (w <= 0f) return false;

        // The facts that get through: one draw per candidate, in a fixed order.
        var told = new List<string>();
        foreach (var (fact, chance) in t.Facts)
        {
            if (rng.NextDouble() < chance) told.Add(fact);
        }

        var cur = listener.Hearsay;
        if (cur != null && !cur.IsPresentAt(time, s)) cur = null;
        int generation = t.Generation + 1;

        if (cur == null)
        {
            listener.Hearsay = new PlayerHearsay(t.Told, Math.Min(1f, w), told, time, time, generation);
            return true;
        }

        float weight = cur.WeightAt(time, s.HalfLifeDays);
        float regard = cur.Regard;
        bool confirmation = Math.Sign(t.Told) == Math.Sign(regard) && Math.Abs(t.Told) <= Math.Abs(regard);
        if (!confirmation)
            regard += w * (t.Told - regard) / (weight + w);

        var facts = new List<string>(cur.Facts);
        facts.AddRange(told);
        listener.Hearsay = new PlayerHearsay(regard, Math.Min(1f, weight + w), facts,
            cur.HeardAt, time, Math.Min(cur.Generation, generation));
        return true;
    }
}
