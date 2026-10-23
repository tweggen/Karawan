using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using engine.physics;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S1-b (KAR-455): the player interaction model — player blocks on
/// interaction types, PlayerInteractions, witness selection and the action
/// classifier. Most tests use the shipped nogame.interactions.json, so they
/// pin the numbers the S1 done-when criteria depend on.
/// </summary>
public class PlayerInteractionsTests
{
    private const int Cluster = 3;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);
    private static readonly string[] PlayerOnlyIds = { "assault", "run_over", "pay", "bribe", "drive" };


    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "models", "nogame.interactions.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir.FullName;
    }


    /* Deserialises like TaleRegistryLoader. */
    private static InteractionTypeRegistry Registry(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var registry = new InteractionTypeRegistry();
        foreach (var node in JsonNode.Parse(json)!.AsArray())
        {
            var def = JsonSerializer.Deserialize<InteractionTypeDefinition>(node!.ToJsonString(), options);
            registry.Add(def!.Id, def);
        }
        registry.FinalizeOrder();
        return registry;
    }


    private static InteractionTypeRegistry Shipped() =>
        Registry(File.ReadAllText(Path.Combine(RepoRoot(), "models", "nogame.interactions.json")));


    private static NpcSchedule Npc(int index, int groupId = -1) => new()
    {
        NpcId = NpcSchedule.MakeNpcId(Cluster, index), ClusterIndex = Cluster, NpcIndex = index,
        GroupId = groupId, Role = "worker",
        Properties = new Dictionary<string, float> { ["fear"] = 0.1f, ["anger"] = 0.1f, ["health"] = 0.8f,
            ["wealth"] = 0.3f, ["morality"] = 0.6f },
        Trust = new Dictionary<int, float>()
    };


    private static (TaleManager Tm, PlayerInteractions Pi) Setup(params NpcSchedule[] npcs)
    {
        var tm = new TaleManager();
        foreach (var npc in npcs) tm.RegisterNpc(npc);
        var pi = new PlayerInteractions(tm, Shipped());
        tm.PlayerInteractions = pi;
        return (tm, pi);
    }


    #region data

    [Fact]
    public void ShippedJson_CarriesThePlayerBlocks()
    {
        var r = Shipped();

        var chat = r.Get("chat").Player;
        Assert.Equal(0.02f, chat.TrustDelta);
        Assert.Equal(60f, chat.CooldownMinutes);
        Assert.Equal(0.25f, chat.RepeatScale);

        var help = r.Get("help").Player;
        Assert.Equal(0.4f, help.TrustDelta);
        Assert.Equal(720f, help.CooldownMinutes);

        var assault = r.Get("assault").Player;
        Assert.Equal(-0.25f, assault.TrustDelta);
        Assert.Equal(0.25f, assault.TargetProperties["fear"]);
        Assert.Equal(0f, assault.RepeatScale);
        Assert.Equal(25f, assault.Witness.Radius);
        Assert.Equal(0.35f, assault.Witness.Factor);
        Assert.True(assault.Witness.LineOfSight);

        Assert.Equal(-0.3f, r.Get("run_over").Player.TargetProperties["health"]);
        Assert.Equal(40f, r.Get("run_over").Player.Witness.Radius);
        Assert.True(r.Get("pay").Player.WealthPerCash > 0f);
        Assert.Null(r.Get("drive").Player.CooldownMinutes);

        // NPC-only types carry no block; player-only types are never auto-selected.
        Assert.Null(r.Get("greet").Player);
        Assert.Null(r.Get("rob").Player);
        Assert.All(PlayerOnlyIds, id => Assert.Null(r.Get(id).ConditionClassName));
    }


    [Fact]
    public void Json_WithAndWithoutPlayerBlock_Deserialises()
    {
        var r = Registry("""
        [
          { "id": "plain", "trustDelta": 0.05, "conditionClassName": null, "conditionParameters": {} },
          { "id": "full", "trustDelta": 0.01, "conditionClassName": null, "conditionParameters": {},
            "player": { "trustDelta": -0.2, "targetProperties": { "fear": 0.1 }, "targetFact": "x",
                        "cooldownMinutes": 5, "repeatScale": 0.5, "wealthPerCash": 0.01,
                        "witness": { "factor": 0.5, "radius": 10, "fact": "saw_x" } } }
        ]
        """);

        Assert.Null(r.Get("plain").Player);
        var p = r.Get("full").Player;
        Assert.Equal(-0.2f, p.TrustDelta);
        Assert.Equal("x", p.TargetFact);
        Assert.Equal(0.5f, p.RepeatScale);
        Assert.Equal(8, p.Witness.MaxCount);          // default
        Assert.True(p.Witness.LineOfSight);           // default
        Assert.Null(p.Witness.Properties);
    }


    [Fact]
    public void PlayerOnlyIds_AreNeverSelectedForNpcPairs()
    {
        // The shipped types, without patrol_check: AuthorityCondition needs the RoleRegistry.
        var shipped = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "models", "nogame.interactions.json")))!
            .AsArray().Where(n => (string)n!["id"]! != "patrol_check").Select(n => n!.DeepClone()).ToArray();
        var r = Registry(new JsonArray(shipped).ToJsonString());
        var rng = new Random(7);
        var seen = new HashSet<string>();
        for (int i = 0; i < 20000; i++)
        {
            var a = Npc(1);
            var b = Npc(2);
            foreach (var key in a.Properties.Keys.ToList())
            {
                a.Properties[key] = (float)rng.NextDouble();
                b.Properties[key] = (float)rng.NextDouble();
            }
            a.Properties["desperation"] = (float)rng.NextDouble();
            a.Properties["reputation"] = (float)rng.NextDouble();
            string id = r.EvaluateConditions(a, b, (float)rng.NextDouble(), rng);
            if (id != null) seen.Add(id);
        }

        Assert.NotEmpty(seen);
        Assert.Empty(seen.Intersect(PlayerOnlyIds));
    }

    #endregion


    #region Apply

    [Fact]
    public void Help_FromFirstContact_StartsAtTheBaseline_ThenFlattensTheChange()
    {
        var npc = Npc(1);
        var (tm, _) = Setup(npc);

        var r = tm.ApplyPlayerInteraction(npc.NpcId, "help", T0);

        // 0.15 + 0.4 × (1 − 0.15) = 0.49: the owner's "one help counts a lot".
        Assert.True(r.Applied);
        Assert.Equal(PlayerTrust.FirstContact, r.TrustBefore);
        Assert.Equal(0.49f, r.TrustAfter, 4);
        Assert.Equal(0.49f, npc.Trust[PlayerTrust.Key], 4);
        Assert.Equal(1f, npc.Properties["player_fact.helped_me"]);
        Assert.True(npc.HasPlayerDeviation);
        Assert.Equal(1, tm.PlayerTrustVersion);
    }


    [Fact]
    public void Assault_LowersTrust_RaisesFearAndAnger_AndIsRemembered()
    {
        var npc = Npc(1);
        npc.Trust[PlayerTrust.Key] = 0.5f;
        var (tm, _) = Setup(npc);

        var r = tm.ApplyPlayerInteraction(npc.NpcId, "assault", T0);

        Assert.Equal(0.25f, r.TrustAfter, 4);
        Assert.Equal(0.35f, npc.Properties["fear"], 4);
        Assert.Equal(0.25f, npc.Properties["anger"], 4);
        Assert.Equal(1f, npc.Properties["player_fact.hit_me"]);
        Assert.True(npc.HasPlayerDeviation);
    }


    [Fact]
    public void RunOver_HurtsAndClamps()
    {
        var npc = Npc(1);
        npc.Properties["health"] = 0.2f;
        var (tm, _) = Setup(npc);

        var r = tm.ApplyPlayerInteraction(npc.NpcId, "run_over", T0);

        Assert.Equal(0f, r.TrustAfter);                          // 0.15 − 0.45, clamped
        Assert.Equal(0f, npc.Properties["health"]);              // 0.2 − 0.3, clamped
        Assert.Equal(1f, npc.Properties["player_fact.ran_me_over"]);
    }


    [Fact]
    public void Assault_RepeatsInsideTheCooldownCountOnce_ThenCountAgain()
    {
        var npc = Npc(1);
        npc.Trust[PlayerTrust.Key] = 0.8f;
        var (tm, _) = Setup(npc);

        Assert.True(tm.ApplyPlayerInteraction(npc.NpcId, "assault", T0).Applied);
        // A punch makes several contact episodes; repeat scale 0 drops them.
        var again = tm.ApplyPlayerInteraction(npc.NpcId, "assault", T0.AddSeconds(1));
        var later = tm.ApplyPlayerInteraction(npc.NpcId, "assault", T0.AddMinutes(2.9));
        Assert.False(again.Applied);
        Assert.False(later.Applied);
        Assert.Equal(0.55f, npc.Trust[PlayerTrust.Key], 4);
        Assert.Equal(0.35f, npc.Properties["fear"], 4);

        // The cooldown counts from the full application: three minutes on, it counts again.
        var next = tm.ApplyPlayerInteraction(npc.NpcId, "assault", T0.AddMinutes(3));
        Assert.True(next.Applied);
        Assert.Equal(0.30f, npc.Trust[PlayerTrust.Key], 4);
    }


    [Fact]
    public void Chat_RepeatsAreScaled_AndTheCooldownResets()
    {
        var npc = Npc(1);
        npc.Trust[PlayerTrust.Key] = 0.15f;
        var (tm, _) = Setup(npc);

        var scales = new[] { 0, 10, 20, 59 }
            .Select(m => tm.ApplyPlayerInteraction(npc.NpcId, "chat", T0.AddMinutes(m)).Scale).ToArray();
        Assert.Equal(new[] { 1f, 0.25f, 0.0625f, 0.015625f }, scales);

        Assert.Equal(1f, tm.ApplyPlayerInteraction(npc.NpcId, "chat", T0.AddMinutes(60)).Scale);
    }


    [Fact]
    public void Cooldowns_ArePerNpcAndPerType()
    {
        var a = Npc(1);
        var b = Npc(2);
        var (tm, _) = Setup(a, b);

        tm.ApplyPlayerInteraction(a.NpcId, "assault", T0);
        Assert.True(tm.ApplyPlayerInteraction(b.NpcId, "assault", T0).Applied);
        Assert.True(tm.ApplyPlayerInteraction(a.NpcId, "run_over", T0).Applied);
    }


    [Fact]
    public void PositiveChanges_FlattenNearOne_NegativeOnesDoNot()
    {
        var friend = Npc(1);
        friend.Trust[PlayerTrust.Key] = 0.95f;
        var victim = Npc(2);
        victim.Trust[PlayerTrust.Key] = 0.95f;
        var (tm, _) = Setup(friend, victim);

        Assert.Equal(0.97f, tm.ApplyPlayerInteraction(friend.NpcId, "help", T0).TrustAfter, 4);   // + 0.4 × 0.05
        Assert.Equal(0.70f, tm.ApplyPlayerInteraction(victim.NpcId, "assault", T0).TrustAfter, 4);
    }


    [Fact]
    public void Pay_AddsTrustAndWealthFromCash()
    {
        var npc = Npc(1);
        var (tm, _) = Setup(npc);

        var r = tm.ApplyPlayerInteraction(npc.NpcId, "pay", T0, cashPaid: 100f);

        Assert.Equal(0.15f + 0.06f * 0.85f, r.TrustAfter, 4);
        Assert.Equal(0.5f, npc.Properties["wealth"], 4);          // 0.3 + 100 × 0.002
        Assert.Equal(1f, npc.Properties["player_fact.paid_me"]);

        // A second payment the same game day buys no more trust, but the money still counts.
        var again = tm.ApplyPlayerInteraction(npc.NpcId, "pay", T0.AddHours(2), cashPaid: 50f);
        Assert.False(again.Applied);
        Assert.Equal(r.TrustAfter, npc.Trust[PlayerTrust.Key], 4);
        Assert.Equal(0.6f, npc.Properties["wealth"], 4);
        Assert.True(tm.ApplyPlayerInteraction(npc.NpcId, "pay", T0.AddDays(1), cashPaid: 1f).Applied);
    }


    [Fact]
    public void Bribe_AlsoCostsMorality()
    {
        var npc = Npc(1);
        var (tm, _) = Setup(npc);

        tm.ApplyPlayerInteraction(npc.NpcId, "bribe", T0, cashPaid: 10f);

        Assert.Equal(0.58f, npc.Properties["morality"], 4);
        Assert.Equal(0.32f, npc.Properties["wealth"], 4);
    }


    [Fact]
    public void UnknownInteractionOrNpc_ChangesNothing()
    {
        var npc = Npc(1);
        var (tm, _) = Setup(npc);

        Assert.False(tm.ApplyPlayerInteraction(npc.NpcId, "tickle", T0).Applied);
        Assert.False(tm.ApplyPlayerInteraction(npc.NpcId, null, T0).Applied);
        Assert.False(tm.ApplyPlayerInteraction(NpcSchedule.MakeNpcId(Cluster, 99), "help", T0).Applied);

        Assert.False(PlayerTrust.Knows(npc));
        Assert.False(npc.HasPlayerDeviation);
        Assert.Equal(0, tm.PlayerTrustVersion);
    }


    [Fact]
    public void KnownTypeWithoutPlayerBlock_FallsBackToItsNpcTrustChange()
    {
        var npc = Npc(1);
        var (tm, _) = Setup(npc);

        var r = tm.ApplyPlayerInteraction(npc.NpcId, "greet", T0);

        Assert.Equal(0.15f + 0.04f * 0.85f, r.TrustAfter, 4);
        Assert.Equal(1f, tm.ApplyPlayerInteraction(npc.NpcId, "greet", T0).Scale);   // no cooldown
    }

    #endregion


    #region witnesses

    [Fact]
    public void Witness_IsWeakerThanTheVictim_AndAGroupmateStrongerThanAStranger()
    {
        var victim = Npc(1, groupId: 4);
        var stranger = Npc(2);
        var groupmate = Npc(3, groupId: 4);
        foreach (var n in new[] { victim, stranger, groupmate }) n.Trust[PlayerTrust.Key] = 0.6f;
        var (tm, _) = Setup(victim, stranger, groupmate);

        var v = tm.ApplyPlayerInteraction(victim.NpcId, "assault", T0);
        var s = tm.ApplyWitnessedPlayerInteraction(stranger.NpcId, victim.NpcId, "assault", T0, distance: 10f);
        var g = tm.ApplyWitnessedPlayerInteraction(groupmate.NpcId, victim.NpcId, "assault", T0, distance: 10f);

        float drop(PlayerInteractionResult r) => r.TrustBefore - r.TrustAfter;
        Assert.True(drop(v) > drop(g) && drop(g) > drop(s) && drop(s) > 0f,
            $"victim {drop(v)}, groupmate {drop(g)}, stranger {drop(s)}");
        // −0.25 × 0.35 × (1 − 0.5 × 10/25) = −0.07; the groupmate × 1.5.
        Assert.Equal(0.07f, drop(s), 4);
        Assert.Equal(0.105f, drop(g), 4);
        Assert.Equal(1f, stranger.Properties["player_fact.saw_assault"]);
        Assert.False(stranger.Properties.ContainsKey("player_fact.hit_me"));
        Assert.Equal(0.18f, stranger.Properties["fear"], 4);          // 0.1 + 0.1 × 0.8
        Assert.True(stranger.HasPlayerDeviation);
    }


    [Fact]
    public void Witness_FallsOffWithDistance_AndBeyondTheRadiusSeesNothing()
    {
        var victim = Npc(1);
        var near = Npc(2);
        var far = Npc(3);
        var outside = Npc(4);
        foreach (var w in new[] { near, far }) w.Trust[PlayerTrust.Key] = 0.5f;
        var (tm, _) = Setup(victim, near, far, outside);

        var n = tm.ApplyWitnessedPlayerInteraction(near.NpcId, victim.NpcId, "run_over", T0, 0f);
        var f = tm.ApplyWitnessedPlayerInteraction(far.NpcId, victim.NpcId, "run_over", T0, 40f);
        var o = tm.ApplyWitnessedPlayerInteraction(outside.NpcId, victim.NpcId, "run_over", T0, 40.5f);

        Assert.Equal(0.5f - 0.45f * 0.4f, n.TrustAfter, 4);
        Assert.Equal(0.5f - 0.45f * 0.4f * 0.5f, f.TrustAfter, 4);
        Assert.False(o.Applied);
        Assert.False(PlayerTrust.Knows(outside));
    }


    [Fact]
    public void Witness_OnlyForTypesWithAWitnessBlock_NotTheVictimItself_AndOncePerCooldown()
    {
        var victim = Npc(1);
        var bystander = Npc(2);
        var (tm, _) = Setup(victim, bystander);

        Assert.False(tm.ApplyWitnessedPlayerInteraction(bystander.NpcId, victim.NpcId, "help", T0, 1f).Applied);
        Assert.False(tm.ApplyWitnessedPlayerInteraction(victim.NpcId, victim.NpcId, "assault", T0, 0f).Applied);

        Assert.True(tm.ApplyWitnessedPlayerInteraction(bystander.NpcId, victim.NpcId, "assault", T0, 5f).Applied);
        Assert.False(tm.ApplyWitnessedPlayerInteraction(bystander.NpcId, victim.NpcId, "assault", T0.AddMinutes(1), 5f).Applied);
        // Being a victim and having witnessed are separate cooldowns.
        Assert.True(tm.ApplyPlayerInteraction(bystander.NpcId, "assault", T0.AddMinutes(1)).Applied);
    }


    [Fact]
    public void WitnessStrength_IsOneAtTheEvent_HalfAtTheRadius()
    {
        var w = new PlayerWitnessSpec { Radius = 20f };
        Assert.Equal(1f, PlayerInteractions.WitnessStrength(w, 0f, false));
        Assert.Equal(0.5f, PlayerInteractions.WitnessStrength(w, 20f, false));
        Assert.Equal(0.75f, PlayerInteractions.WitnessStrength(w, 20f, true));
    }


    private static readonly (int, Vector3)[] Crowd =
    {
        (10, new Vector3(3, 0, 0)),
        (11, new Vector3(0, 0, 3)),     // same distance as 10: id breaks the tie
        (12, new Vector3(1, 0, 0)),
        (13, new Vector3(30, 0, 0)),    // outside
        (14, new Vector3(0, 0, -6)),
        (15, new Vector3(0, 0, 0)),     // the victim
        (16, new Vector3(4, 3, 0)),     // 5 m
    };


    [Fact]
    public void WitnessSelector_FiltersByRadius_ExcludesTheVictim_SortsByDistanceThenId()
    {
        var w = WitnessSelector.Select(Vector3.Zero, Crowd, excludeId: 15, radius: 10f, maxCount: 8);

        Assert.Equal(new[] { 12, 10, 11, 16, 14 }, w.Select(x => x.NpcId));
        Assert.Equal(new[] { 1f, 3f, 3f, 5f, 6f }, w.Select(x => x.Distance));
    }


    [Fact]
    public void WitnessSelector_CapsTheCount_AndAsksLineOfSightOnlyAlongTheSortedPrefix()
    {
        var asked = new List<int>();
        bool lineOfSight(int id, Vector3 _)
        {
            asked.Add(id);
            return id != 10;                     // 10 stands behind a wall
        }

        var w = WitnessSelector.Select(Vector3.Zero, Crowd, 15, 10f, maxCount: 3, lineOfSight);

        Assert.Equal(new[] { 12, 11, 16 }, w.Select(x => x.NpcId));
        Assert.Equal(new[] { 12, 10, 11, 16 }, asked);       // 14 is never ray-cast
    }


    [Fact]
    public void WitnessSelector_DoesNotDependOnTheCandidatesOrder()
    {
        var expected = WitnessSelector.Select(Vector3.Zero, Crowd, 15, 10f, 4).ToArray();
        var rng = new Random(3);
        for (int i = 0; i < 50; i++)
        {
            var shuffled = Crowd.OrderBy(_ => rng.Next()).ToArray();
            Assert.Equal(expected, WitnessSelector.Select(Vector3.Zero, shuffled, 15, 10f, 4).ToArray());
        }
    }


    [Fact]
    public void WitnessSelector_EmptyInputs()
    {
        Assert.Empty(WitnessSelector.Select(Vector3.Zero, null, 1, 10f, 8));
        Assert.Empty(WitnessSelector.Select(Vector3.Zero, Crowd, 1, 0f, 8));
        Assert.Empty(WitnessSelector.Select(Vector3.Zero, Crowd, 1, 10f, 0));
    }

    #endregion


    #region classifier

    [Theory]
    [InlineData(CollisionProperties.Layers.PlayerMelee, PlayerAction.Assault)]
    [InlineData(CollisionProperties.Layers.PlayerBullet, PlayerAction.Assault)]
    [InlineData(CollisionProperties.Layers.PlayerVehicle, PlayerAction.RunOver)]
    [InlineData(CollisionProperties.Layers.PlayerVehicle | CollisionProperties.Layers.PlayerMelee, PlayerAction.RunOver)]
    [InlineData(CollisionProperties.Layers.NpcVehicle, PlayerAction.None)]
    [InlineData(CollisionProperties.Layers.NpcMelee, PlayerAction.None)]
    [InlineData(CollisionProperties.Layers.PlayerCharacter, PlayerAction.None)]
    [InlineData(CollisionProperties.Layers.StaticEnvironment, PlayerAction.None)]
    public void Classifier_ReadsWhoDidItFromTheLayers(CollisionProperties.Layers layers, PlayerAction expected)
    {
        Assert.Equal(expected, PlayerActionClassifier.Classify(layers));
    }


    [Fact]
    public void Classifier_MapsActionsToInteractionIds()
    {
        Assert.Equal("assault", PlayerActionClassifier.InteractionId(PlayerAction.Assault));
        Assert.Equal("run_over", PlayerActionClassifier.InteractionId(PlayerAction.RunOver));
        Assert.Null(PlayerActionClassifier.InteractionId(PlayerAction.None));
        Assert.All(new[] { PlayerAction.Assault, PlayerAction.RunOver },
            a => Assert.NotNull(Shipped().Get(PlayerActionClassifier.InteractionId(a))?.Player));
    }

    #endregion
}
