using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-498 (TALE S2-c): what reads hearsay - the first-contact prejudice (owner: ±0.2), the
 * standing level's "recognised" (owner: hearing the name counts), the metrics and panel,
 * and the conversations' hearsay greetings.
 */
public class HearsayReadersTests
{
    private const int Cluster = 3;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);

    private static NpcSchedule Npc(int index, int groupId = -1) => new()
    {
        NpcId = NpcSchedule.MakeNpcId(Cluster, index), ClusterIndex = Cluster, NpcIndex = index,
        GroupId = groupId, Role = "worker",
        Properties = new Dictionary<string, float> { ["fear"] = 0.1f, ["anger"] = 0.1f },
        Trust = new Dictionary<int, float>()
    };

    private static PlayerHearsay Heard(float regard, float weight, params string[] facts) =>
        new(regard, weight, facts, T0, T0, 1);


    [Fact]
    public void AFirstContactStartsFromWhatTheNpcHeard()
    {
        var tm = ShippedInteractions.Manager();
        var warned = Npc(0);
        warned.Hearsay = Heard(-0.5f, 1f, "hurt_someone");
        var told = Npc(1);
        told.Hearsay = Heard(0.4f, 1f, "helped_someone");
        var stranger = Npc(2);
        foreach (var n in new[] { warned, told, stranger }) tm.RegisterNpc(n);

        // Start = 0.15 + 0.2 x regard x weight; Get stays 0.15 for an NPC without an entry.
        Assert.Equal(0.05f, PlayerTrust.StartFor(warned, T0), 4);
        Assert.Equal(0.23f, PlayerTrust.StartFor(told, T0), 4);
        Assert.Equal(0.15f, PlayerTrust.Get(told));
        Assert.Equal(0.15f, PlayerTrust.StartFor(told));         // no time: no prejudice

        // A help acts on where the NPC starts, flattening included: 0.23 + 0.4 x 0.77.
        var helped = tm.ApplyPlayerInteraction(told.NpcId, "help", T0);
        Assert.Equal(0.23f, helped.TrustBefore, 4);
        Assert.Equal(0.538f, helped.TrustAfter, 3);
        var plain = tm.ApplyPlayerInteraction(stranger.NpcId, "help", T0);
        Assert.Equal(0.49f, plain.TrustAfter, 3);

        // An entry, once there, is what counts.
        Assert.Equal(0.538f, PlayerTrust.StartFor(told, T0), 3);
    }


    [Fact]
    public void ARumourFadesOutOfThePrejudice()
    {
        var npc = Npc(0);
        npc.Hearsay = Heard(-0.5f, 0.8f);
        Assert.Equal(0.15f - 0.2f * 0.5f * 0.8f, PlayerTrust.StartFor(npc, T0), 4);
        Assert.Equal(0.15f - 0.2f * 0.5f * 0.4f, PlayerTrust.StartFor(npc, T0.AddDays(3)), 4);

        // Trust is clamped: a strong, firm rumour starts at 0, not below.
        npc.Hearsay = Heard(-1f, 1f);
        Assert.Equal(0f, PlayerTrust.StartFor(npc, T0), 4);
        Assert.Equal(0.15f, PlayerTrust.StartFor(npc, T0.AddDays(30)), 4);   // below the floor: absent
    }


    [Fact]
    public void HearingTheNameMakesThePlayerRecognised()
    {
        var tm = ShippedInteractions.Manager();
        var npcs = Enumerable.Range(0, 6).Select(i => Npc(i)).ToList();
        foreach (var n in npcs) tm.RegisterNpc(n);
        npcs[0].Hearsay = Heard(-0.5f, 0.5f, "hurt_someone");
        npcs[1].Hearsay = Heard(-0.25f, 0.4f);
        npcs[2].Hearsay = Heard(0.2f, 0.3f, "helped_someone");
        npcs[3].Hearsay = Heard(-0.5f, 0.1f);                    // too faint
        npcs[4].Hearsay = Heard(0.1f, 0.9f);                     // too mild

        Assert.Equal(StandingLevel.Anonymous, tm.GetPlayerStanding(Cluster).Level);   // no time: not read
        var reading = tm.GetPlayerStanding(Cluster, T0);
        Assert.Equal(StandingLevel.Recognised, reading.Level);
        Assert.Equal(3, reading.Inputs.KnownEnter);

        // Ten days on the rumours have faded.
        Assert.Equal(StandingLevel.Anonymous, tm.GetPlayerStanding(Cluster, T0.AddDays(10)).Level);
    }


    [Fact]
    public void TheMetricsCountHoldersByGroupAndGeneration()
    {
        var npcs = Enumerable.Range(0, 5).Select(i => Npc(i, groupId: i < 3 ? 7 : -1)).ToList();
        npcs[0].Trust[PlayerTrust.Key] = 0f;
        npcs[1].Hearsay = Heard(-0.5f, 0.4f);
        npcs[2].Hearsay = new PlayerHearsay(-0.25f, 0.4f, null, T0, T0, 2);
        npcs[3].Hearsay = Heard(0.2f, 0.4f);
        npcs[4].Hearsay = Heard(-0.5f, 0.01f);                   // below the floor

        var m = HearsayMetrics.Measure(npcs, T0, groupId: 7);
        Assert.Equal(3, m.Holders);
        Assert.Equal(2, m.HoldersInGroup);
        Assert.Equal(1, m.HoldersOutsideGroup);
        Assert.Equal(1, m.KnowFirstHand);
        Assert.Equal(2, m.ByGeneration[1]);
        Assert.Equal(1, m.ByGeneration[2]);
        Assert.Equal(new[] { 1, 1, 1, 0 }, m.AbsRegardQuartiles);
    }


    [Fact]
    public void ThePanelSnapshotShowsHearsay()
    {
        var tm = ShippedInteractions.Manager();
        var npc = Npc(0);
        npc.Hearsay = Heard(-0.5f, 0.6f, "hurt_someone");
        tm.RegisterNpc(npc);
        tm.RegisterNpc(Npc(1));

        var snap = TaleDebugSnapshotBuilder.Build(tm, Cluster, T0.AddDays(3), Vector3.Zero, npc.NpcId);
        Assert.Equal(1, snap.Hearsay.Holders);
        Assert.NotNull(snap.Selected.Hearsay);
        Assert.Equal(0.3f, snap.Selected.Hearsay.Weight, 4);     // decayed one half-life
        Assert.Equal(new[] { "hurt_someone" }, snap.Selected.Hearsay.Facts);
    }


    private static string Root => Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));


    [Theory]
    [InlineData("tale.generic.json")]
    [InlineData("tale.tag.routine.json")]
    public void TheConversationsGreetFromHearsayAfterTheGroupGreeting(string file)
    {
        var doc = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "models", "tale", "conversations", file)))!;
        var nodes = doc["nodes"]!.AsObject();
        var gotos = nodes["entry"]!["flow"]![0]!["goto"]!.AsArray()
            .Select(g => g!["goto"]?.GetValue<string>() ?? g!["else"]?.GetValue<string>()).ToList();

        int cold = gotos.IndexOf("group_cold");
        Assert.True(cold >= 0);
        Assert.Equal(new[] { "heard_hurt", "heard_bad", "heard_good" }, gotos.Skip(cold + 1).Take(3));
        foreach (var n in new[] { "heard_hurt", "heard_bad", "heard_good" }) Assert.True(nodes.ContainsKey(n));

        var conditions = nodes["entry"]!["flow"]![0]!["goto"]!.AsArray()
            .Select(g => g!["if"]?.GetValue<string>() ?? "").Where(c => c.Contains("heard_")).ToList();
        Assert.All(conditions, c => Assert.Contains("props.npc.met_player == 'false'", c));
        Assert.All(conditions, c => Assert.Contains("props.npc.heard_weight >= 0.2", c));
    }


    /** The bindings live in nogame; pinned by source: every NPC gets the props, a reset is 0. */
    [Fact]
    public void TheNarrationPropsAreSetForEveryNpcAndResetToZero()
    {
        string src = File.ReadAllText(Path.Combine(Root, "nogameCode", "nogame", "modules", "tale", "TaleNarrationBindings.cs"));
        Assert.Contains("Props.Set(\"npc.heard_player\", present ? heard.Regard : 0f);", src);
        Assert.Contains("Props.Set(\"npc.heard_weight\", present ? heard.WeightAt(now) : 0f);", src);
        Assert.Contains("foreach (var fact in PlayerGossip.HeardFacts)", src);
        Assert.Contains("else if (key.StartsWith(\"npc.heard_\"))", src);
        int reset = src.IndexOf("else if (key.StartsWith(\"npc.heard_\"))", StringComparison.Ordinal);
        Assert.Contains("Props.Set(key, 0f);", src.Substring(reset, 120));
    }
}
