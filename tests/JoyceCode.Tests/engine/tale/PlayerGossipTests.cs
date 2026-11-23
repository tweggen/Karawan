using System;
using System.Collections.Generic;
using System.Linq;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-496 (TALE S2-a): hearsay and the gossip exchange, as the plan
 * docs/tale/plans/proposed/TALE-S2-GOSSIP.md §2 and the owner's answers (§6) define them.
 */
public class PlayerGossipTests
{
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);
    private const float Eps = 1e-4f;

    private static NpcSchedule _npc(int id) => new()
    {
        NpcId = id, Seed = id, Role = "worker",
        Properties = new Dictionary<string, float>(), Trust = new Dictionary<int, float>(),
    };

    /** A victim of an assault: trust 0 (0.15 - 0.25, clamped), regard -1, the fact. */
    private static NpcSchedule _victim(int id)
    {
        var v = _npc(id);
        v.Trust[PlayerTrust.Key] = 0f;
        v.Properties["player_fact.hit_me"] = 1f;
        return v;
    }

    /** Counts its draws; returns a fixed value, so facts always pass when the chance is above it. */
    private sealed class CountingRandom : Random
    {
        public int Draws;
        private readonly double _value;
        public CountingRandom(double value = 0.0) => _value = value;
        public override double NextDouble() { Draws++; return _value; }
        public override int Next() { Draws++; return 0; }
        public override int Next(int maxValue) { Draws++; return 0; }
        public override int Next(int minValue, int maxValue) { Draws++; return minValue; }
    }


    [Fact]
    public void TheWorkedExampleHolds_HarmTravelsTwoHopsOverDays()
    {
        var victim = _victim(1);
        var friend = _npc(2);
        var friendOfFriend = _npc(3);
        var third = _npc(4);
        var rng = new CountingRandom();

        // Day 0: the victim tells a friend (trust 1): -0.5, generation 1, with the fact.
        PlayerGossip.Exchange(victim, friend, 1f, 1f, T0, rng);
        Assert.Equal(-0.5f, friend.Hearsay.Regard, 4);
        Assert.Equal(1, friend.Hearsay.Generation);
        Assert.True(friend.Hearsay.Has("hurt_someone"));

        // Same day: the friend does not retell yet (owner: no second generation within a day).
        PlayerGossip.Exchange(friend, friendOfFriend, 1f, 1f, T0.AddHours(6), rng);
        Assert.Null(friendOfFriend.Hearsay);

        // A day later it does: -0.25, generation 2.
        PlayerGossip.Exchange(friend, friendOfFriend, 1f, 1f, T0.AddDays(1).AddHours(1), rng);
        Assert.Equal(-0.25f, friendOfFriend.Hearsay.Regard, 4);
        Assert.Equal(2, friendOfFriend.Hearsay.Generation);

        // Its retelling would be -0.125 < MinTold: the rumour stops.
        PlayerGossip.Exchange(friendOfFriend, third, 1f, 1f, T0.AddDays(3), rng);
        Assert.Null(third.Hearsay);
    }


    [Fact]
    public void HelpTravelsOneHop()
    {
        var helped = _npc(1);
        helped.Trust[PlayerTrust.Key] = 0.49f;                  // one help: 0.15 + 0.4 x 0.85
        helped.Properties["player_fact.helped_me"] = 1f;
        var friend = _npc(2);
        var next = _npc(3);
        var rng = new CountingRandom();

        PlayerGossip.Exchange(helped, friend, 1f, 1f, T0, rng);
        Assert.Equal(0.2f, friend.Hearsay.Regard, 2);
        Assert.True(friend.Hearsay.Has("helped_someone"));

        PlayerGossip.Exchange(friend, next, 1f, 1f, T0.AddDays(2), rng);
        Assert.Null(next.Hearsay);                               // 0.1 < MinTold
    }


    [Fact]
    public void ATellingIsAtMostHalfItsSource_AndAWeakOpinionIsNotTold()
    {
        var mild = _npc(1);
        mild.Trust[PlayerTrust.Key] = 0.17f;                    // one chat: regard ~0.02
        var listener = _npc(2);
        var rng = new CountingRandom();
        PlayerGossip.Exchange(mild, listener, 1f, 1f, T0, rng);
        Assert.Null(listener.Hearsay);
        Assert.Equal(0, rng.Draws);

        var source = _victim(3);
        PlayerGossip.Exchange(source, listener, 1f, 1f, T0, rng);
        Assert.True(Math.Abs(listener.Hearsay.Regard) <= 0.5f + Eps);
    }


    [Fact]
    public void FirstHandTrustIsNeverChanged_AndNoPlayerFactIsWritten()
    {
        var victim = _victim(1);
        var knower = _npc(2);
        knower.Trust[PlayerTrust.Key] = 0.8f;
        PlayerGossip.Exchange(victim, knower, 1f, 1f, T0, new CountingRandom());

        Assert.Equal(0.8f, knower.Trust[PlayerTrust.Key]);
        Assert.Equal(0f, victim.Trust[PlayerTrust.Key]);
        Assert.DoesNotContain(knower.Properties.Keys, k => k.StartsWith("player_fact.", StringComparison.Ordinal));
        Assert.NotNull(knower.Hearsay);                          // it keeps what it heard
        Assert.False(_npc(9).IsKnownToPlayer);
        var hearer = _npc(10);
        PlayerGossip.Exchange(victim, hearer, 1f, 1f, T0, new CountingRandom());
        Assert.False(hearer.IsKnownToPlayer);                    // hearsay does not make it known
    }


    [Fact]
    public void AListenerWeighsATellingByItsTrustInTheTeller()
    {
        var stranger = _npc(2);
        PlayerGossip.Exchange(_victim(1), stranger, 1f, 0f, T0, new CountingRandom());
        Assert.Null(stranger.Hearsay);

        var acquaintance = _npc(3);
        PlayerGossip.Exchange(_victim(1), acquaintance, 1f, 0.4f, T0, new CountingRandom());
        Assert.Equal(0.2f, acquaintance.Hearsay.Weight, 4);      // Listen 0.5 x 0.4
    }


    [Fact]
    public void AConfirmationRaisesOnlyTheWeight()
    {
        var a = _npc(1);
        a.Hearsay = new PlayerHearsay(-0.5f, 0.4f, new[] { "hurt_someone" }, T0.AddDays(-2), T0, 1);
        var b = _npc(2);
        b.Hearsay = new PlayerHearsay(-0.5f, 0.4f, new[] { "hurt_someone" }, T0.AddDays(-2), T0, 1);

        // Each tells the other -0.25: a weaker version of what both hold.
        PlayerGossip.Exchange(a, b, 1f, 1f, T0, new CountingRandom());
        Assert.Equal(-0.5f, a.Hearsay.Regard, 4);
        Assert.Equal(-0.5f, b.Hearsay.Regard, 4);
        Assert.Equal(0.9f, b.Hearsay.Weight, 4);
    }


    [Fact]
    public void BribesNeverTravel()
    {
        var bribed = _npc(1);
        bribed.Trust[PlayerTrust.Key] = 0.9f;
        bribed.Properties["player_fact.bribed_me"] = 1f;
        var listener = _npc(2);
        PlayerGossip.Exchange(bribed, listener, 1f, 1f, T0, new CountingRandom());
        Assert.NotNull(listener.Hearsay);
        Assert.Empty(listener.Hearsay.Facts);
    }


    [Fact]
    public void DecayHalvesTheWeightPerHalfLife_AndReadingNeverWrites()
    {
        var h = new PlayerHearsay(-0.5f, 0.8f, Array.Empty<string>(), T0, T0, 1);
        Assert.Equal(0.4f, h.WeightAt(T0.AddDays(3)), 4);
        Assert.Equal(0.2f, h.WeightAt(T0.AddDays(6)), 4);
        Assert.Equal(0.8f, h.Weight);                            // unchanged by reading
        Assert.Equal(T0, h.UpdatedAt);
        Assert.True(h.IsPresentAt(T0.AddDays(10)));             // 0.8 x 2^-3.33 = 0.079
        Assert.False(h.IsPresentAt(T0.AddDays(13)));            // 0.8 x 2^-4.33 = 0.040 < 0.05
    }


    [Fact]
    public void AnExchangeWithNothingToTellDrawsNothing()
    {
        var rng = new CountingRandom();
        var (a, b) = PlayerGossip.Exchange(_npc(1), _npc(2), 1f, 1f, T0, rng);
        Assert.False(a || b);
        Assert.Equal(0, rng.Draws);
    }


    [Fact]
    public void TheSameInputsAndSeedGiveTheSameResult()
    {
        PlayerHearsay Run()
        {
            var listener = _npc(2);
            PlayerGossip.Exchange(_victim(1), listener, 0.7f, 0.7f, T0, new Random(42));
            return listener.Hearsay;
        }
        var x = Run();
        var y = Run();
        Assert.Equal(x.Regard, y.Regard);
        Assert.Equal(x.Weight, y.Weight);
        Assert.Equal(x.Facts, y.Facts);
    }


    /** KAR-499 (owner: phones and social media): close ties hear without meeting. */
    [Fact]
    public void ATellerTellsItsStrongestCloseTiesOncePerDay()
    {
        var victim = _victim(1);
        var all = new Dictionary<int, NpcSchedule> { [1] = victim };
        for (int i = 2; i <= 8; i++)
        {
            var n = _npc(i);
            n.Trust[1] = 0.8f;                                 // everyone trusts the victim
            all[i] = n;
        }
        // The victim's own ties: six close ones of different strength, one too weak.
        victim.Trust[2] = 0.9f; victim.Trust[3] = 0.6f; victim.Trust[4] = 0.95f;
        victim.Trust[5] = 0.7f; victim.Trust[6] = 0.55f; victim.Trust[7] = 0.5f; victim.Trust[8] = 0.3f;

        var told = new HashSet<long>();
        int heard = PlayerGossip.TellCloseTies(victim, id => all.GetValueOrDefault(id), T0,
            (t, l) => new CountingRandom(), told);
        Assert.Equal(4, heard);                                  // MaxTiesTold
        Assert.Equal(new[] { 2, 3, 4, 5 }, all.Values.Where(n => n.Hearsay != null).Select(n => n.NpcId).OrderBy(i => i));
        Assert.Equal(-0.5f, all[4].Hearsay.Regard, 4);
        Assert.Equal(0.4f, all[4].Hearsay.Weight, 4);            // Listen 0.5 x trust 0.8

        // Again the same day: nobody new hears, and nothing is told twice.
        Assert.Equal(0, PlayerGossip.TellCloseTies(victim, id => all.GetValueOrDefault(id), T0.AddHours(2),
            (t, l) => new CountingRandom(), told));
        Assert.Equal(0.4f, all[4].Hearsay.Weight, 4);
    }


    [Fact]
    public void AHearerTellsItsTiesOnlyADayLater()
    {
        var hearer = _npc(1);
        hearer.Hearsay = new PlayerHearsay(-0.5f, 0.5f, new[] { "hurt_someone" }, T0, T0, 1);
        hearer.Trust[2] = 0.9f;
        var friend = _npc(2);
        friend.Trust[1] = 0.9f;
        var all = new Dictionary<int, NpcSchedule> { [1] = hearer, [2] = friend };

        Assert.False(PlayerGossip.HasSomethingToTell(hearer, T0.AddHours(12)));
        Assert.Equal(0, PlayerGossip.TellCloseTies(hearer, id => all.GetValueOrDefault(id), T0.AddHours(12),
            (t, l) => new CountingRandom(), new HashSet<long>()));
        Assert.Equal(1, PlayerGossip.TellCloseTies(hearer, id => all.GetValueOrDefault(id), T0.AddDays(1).AddHours(1),
            (t, l) => new CountingRandom(), new HashSet<long>()));
        Assert.Equal(2, friend.Hearsay.Generation);
        Assert.Equal(-0.25f, friend.Hearsay.Regard, 4);
    }


    [Fact]
    public void HearsaySurvivesTheSnapshotAndBothJsonForms()
    {
        var npc = _npc(5);
        npc.Hearsay = new PlayerHearsay(-0.25f, 0.6f, new[] { "hurt_someone", "pays_people" },
            T0.AddDays(-1), T0, 2);

        var social = new ClusterSocialState { ClusterIndex = 3 };
        social.SnapshotNpc(npc);
        var restored = ClusterSocialState.FromJson(social.ToJson());
        var regenerated = _npc(5);
        Assert.True(restored.RestoreNpc(regenerated));
        _assertSame(npc.Hearsay, regenerated.Hearsay);

        var reloaded = NpcScheduleJson.FromJson(NpcScheduleJson.ToJson(npc));
        _assertSame(npc.Hearsay, reloaded.Hearsay);

        // An NPC without hearsay restores to none, and an old snapshot without the field too.
        var none = _npc(6);
        social.SnapshotNpc(none);
        var target = _npc(6);
        target.Hearsay = npc.Hearsay;
        Assert.True(ClusterSocialState.FromJson(social.ToJson()).RestoreNpc(target));
        Assert.Null(target.Hearsay);
    }


    private static void _assertSame(PlayerHearsay expected, PlayerHearsay actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Regard, actual.Regard, 5);
        Assert.Equal(expected.Weight, actual.Weight, 5);
        Assert.Equal(expected.Facts, actual.Facts);
        Assert.Equal(expected.HeardAt, actual.HeardAt);
        Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
        Assert.Equal(expected.Generation, actual.Generation);
    }
}
