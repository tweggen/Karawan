using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using engine.tale;
using engine.tale.bake;
using JoyceCode.Tests.engine.tale.bake;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-499 (TALE S2-d), a measurement, not a gate (CI excludes Category=Perf): how far one
 * punch and one help travel live in Ufyo (4095 NPCs, shipped storylets), per generation and
 * day - the owner's target is generation 1 within the first day, generation 2 (harm only)
 * from the second.
 *
 * As the game does: the city is populated, a baked "medium" scenario is applied (the only
 * category baked in development builds), the background tick moves everyone, the social
 * tick runs every 10 game minutes and its encounters are drained. The victim is a matched
 * NPC with baked ties. Witnesses are a game-layer matter (positions, line of sight) and are
 * left out: the victim is the only first-hand source here.
 */
public class GossipReachPerfTests : IClassFixture<ScenarioCompilerTests.RegistryFixture>
{
    private readonly ITestOutputHelper _out;
    public GossipReachPerfTests(ScenarioCompilerTests.RegistryFixture _, ITestOutputHelper output) => _out = output;

    private static readonly DateTime T0 = new(2000, 1, 2, 8, 0, 0);


    private static Scenario _mediumScenario()
    {
        string generated = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), "..", "nogame", "generated"));
        foreach (var file in Directory.GetFiles(generated, "sc-*").OrderBy(f => f, StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(file);
            var scenario = ScenarioExporter.ReadFromStream(stream);
            if (scenario?.Category == "medium") return scenario;
        }
        throw new InvalidOperationException("no baked medium scenario: build nogame first");
    }


    [Theory]
    [Trait("Category", "Perf")]
    [InlineData("assault")]
    [InlineData("help")]
    public void OneActInUfyoOverThreeDays(string act)
    {
        var (cd, model, generate) = LiveSocialPerfTests._ufyo();
        var tm = ShippedInteractions.Manager();
        tm.Initialize(LiveSocialPerfTests._shipped());
        var scenario = _mediumScenario();
        var applicator = new ScenarioApplicator();
        tm.PopulateClusterWith(cd.Index, model, T0, _ => generate(), npcs => applicator.Apply(scenario, npcs).Groups);
        var npcs = tm.GetNpcsInCluster(cd.Index);

        int withTies = npcs.Count(n => n.Trust != null && n.Trust.Keys.Any(k => k >= 0 && n.Trust[k] >= 0.3f));
        var victim = npcs.Where(n => n.GroupId >= 0 && n.Trust != null)
            .OrderByDescending(n => n.Trust.Count(kv => kv.Key >= 0 && kv.Value >= 0.5f)).ThenBy(n => n.NpcId).First();
        int victimTies = victim.Trust.Count(kv => kv.Key >= 0 && kv.Value >= 0.5f);
        int groupSize = npcs.Count(n => n.GroupId == victim.GroupId);

        var result = tm.ApplyPlayerInteraction(victim.NpcId, act, T0);
        Assert.True(result.Applied);
        _out.WriteLine($"{act} on NPC {victim.NpcId} ({victim.Role}, group {victim.GroupId} of {groupSize}, " +
                       $"{victimTies} ties >= 0.5): trust {result.TrustBefore:F2} -> {result.TrustAfter:F2}; " +
                       $"scenario {scenario.Category}/{scenario.Index} ({scenario.NpcCount} NPCs); " +
                       $"{withTies}/{npcs.Count} NPCs have a tie >= 0.3");

        var evolver = new RuntimeSocialEvolver();
        evolver.TickEncounters(cd.Index, npcs, model, T0);
        DateTime lastSocial = T0;
        for (var now = T0.AddMinutes(1); now <= T0.AddDays(3); now = now.AddMinutes(1))
        {
            tm.AdvanceAbstractSchedules(now, int.MaxValue, int.MaxValue);
            if ((now - lastSocial).TotalMinutes >= 10)
            {
                lastSocial = now;
                tm.CatchUpCluster(cd.Index, now);
                evolver.TickEncounters(cd.Index, tm.GetNpcsInCluster(cd.Index), model, now,
                    (a, b, t) => tm.OnLiveEncounter(cd.Index, a, b, t));
            }
            tm.TickLiveSocial(now, int.MaxValue);

            if (now.Minute == 0 && (now - T0).TotalHours is 6 or 12 or 23 or 36 or 48 or 72)
            {
                var r = HearsayMetrics.Measure(tm.GetNpcsInCluster(cd.Index), now, victim.GroupId);
                int greet = tm.GetNpcsInCluster(cd.Index).Count(n => !PlayerTrust.Knows(n) && n.Hearsay is { } h
                    && h.WeightAt(now) >= 0.2f && (h.Regard <= -0.2f || h.Regard >= 0.15f));
                _out.WriteLine($"  +{(now - T0).TotalHours:F0} h: {r.ToLogLine()}; would greet from hearsay: {greet}; " +
                               $"standing {tm.GetPlayerStanding(cd.Index, now).Name}");
            }
        }

        var end = HearsayMetrics.Measure(tm.GetNpcsInCluster(cd.Index), T0.AddDays(3), victim.GroupId);
        Assert.Equal(1, end.KnowFirstHand);                      // hearing is not meeting
    }
}
