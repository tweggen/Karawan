using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.physics;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S1-d (KAR-457): a world action of the player, without ECS — the
/// contact's layers and the ship's speed decide what it is, the victim takes
/// it, and the NPCs nearby who can see it take a weaker share. The nogame
/// half (router Code = "player" → TaleEntityStrategy → PlayerActionReporter)
/// feeds exactly these calls; it is checked in game.
/// </summary>
public class PlayerActionPipelineTests
{
    private const int Cluster = 3;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);

    private static NpcSchedule Npc(int index, int groupId = -1) => new()
    {
        NpcId = NpcSchedule.MakeNpcId(Cluster, index), ClusterIndex = Cluster, NpcIndex = index, GroupId = groupId,
        Properties = new Dictionary<string, float> { ["fear"] = 0.1f, ["anger"] = 0.1f, ["health"] = 0.8f },
        Trust = new Dictionary<int, float> { [PlayerTrust.Key] = 0.6f }
    };


    private static (TaleManager Tm, NpcSchedule[] Npcs, (int, Vector3)[] Positions) Street()
    {
        var tm = ShippedInteractions.Manager();
        var npcs = new[]
        {
            Npc(0, groupId: 7),     // the victim
            Npc(1, groupId: 7),     // groupmate, 5 m
            Npc(2),                 // stranger, 10 m
            Npc(3),                 // behind a wall, 8 m
            Npc(4),                 // too far for an assault, 30 m
        };
        foreach (var n in npcs) tm.RegisterNpc(n);
        var positions = new (int, Vector3)[]
        {
            (npcs[0].NpcId, Vector3.Zero),
            (npcs[1].NpcId, new Vector3(5, 0, 0)),
            (npcs[2].NpcId, new Vector3(0, 0, 10)),
            (npcs[3].NpcId, new Vector3(-8, 0, 0)),
            (npcs[4].NpcId, new Vector3(30, 0, 0)),
        };
        return (tm, npcs, positions);
    }


    private static Func<int, Vector3, bool> WallAt(int blockedId) => (id, _) => id != blockedId;


    [Fact]
    public void Punch_VictimAndWitnessesLoseTrust_TheWallAndDistanceProtect()
    {
        var (tm, npcs, positions) = Street();

        var action = PlayerActionClassifier.Attribute(CollisionProperties.Layers.PlayerMelee, 0f);
        Assert.Equal(PlayerAction.Assault, action);

        var report = tm.ReportPlayerAction(npcs[0].NpcId, PlayerActionClassifier.InteractionId(action),
            Vector3.Zero, T0, positions, WallAt(npcs[3].NpcId));

        Assert.True(report.Victim.Applied);
        Assert.Equal(new[] { npcs[1].NpcId, npcs[2].NpcId }, report.Witnesses.Select(w => w.NpcId));

        float victim = 0.6f - npcs[0].Trust[PlayerTrust.Key];
        float groupmate = 0.6f - npcs[1].Trust[PlayerTrust.Key];
        float stranger = 0.6f - npcs[2].Trust[PlayerTrust.Key];
        Assert.Equal(0.25f, victim, 4);
        Assert.True(victim > groupmate && groupmate > stranger && stranger > 0f,
            $"victim {victim}, groupmate {groupmate}, stranger {stranger}");
        Assert.Equal(0.6f, npcs[3].Trust[PlayerTrust.Key]);          // behind the wall
        Assert.Equal(0.6f, npcs[4].Trust[PlayerTrust.Key]);          // 30 m > 25 m
        Assert.Equal(1f, npcs[2].Properties["player_fact.saw_assault"]);
        Assert.False(npcs[3].Properties.ContainsKey("player_fact.saw_assault"));
        Assert.True(npcs[2].HasPlayerDeviation);
    }


    [Fact]
    public void SeveralContactEpisodesOfOnePunch_CountOnce_AndWitnessesSeeItOnce()
    {
        var (tm, npcs, positions) = Street();

        tm.ReportPlayerAction(npcs[0].NpcId, "assault", Vector3.Zero, T0, positions);
        var again = tm.ReportPlayerAction(npcs[0].NpcId, "assault", Vector3.Zero, T0.AddSeconds(0.2), positions);

        Assert.False(again.Victim.Applied);
        Assert.Empty(again.Witnesses);
        Assert.Equal(0.35f, npcs[0].Trust[PlayerTrust.Key], 4);
    }


    [Fact]
    public void RunOver_ReachesFurther_AndHurts()
    {
        var (tm, npcs, positions) = Street();

        var action = PlayerActionClassifier.Attribute(CollisionProperties.Layers.PlayerVehicle, 12f);
        var report = tm.ReportPlayerAction(npcs[0].NpcId, PlayerActionClassifier.InteractionId(action),
            Vector3.Zero, T0, positions);

        Assert.Equal(PlayerAction.RunOver, action);
        Assert.Contains(npcs[4].NpcId, report.Witnesses.Select(w => w.NpcId));      // 30 m < 40 m
        Assert.Equal(0.5f, npcs[0].Properties["health"], 4);
        Assert.Equal(1f, npcs[4].Properties["player_fact.saw_run_over"]);
    }


    [Theory]
    [InlineData(CollisionProperties.Layers.PlayerVehicle, 12f, PlayerAction.RunOver)]
    [InlineData(CollisionProperties.Layers.PlayerVehicle, 4f, PlayerAction.RunOver)]
    [InlineData(CollisionProperties.Layers.PlayerVehicle, 3.9f, PlayerAction.None)]      // touching a parked ship
    [InlineData(CollisionProperties.Layers.PlayerVehicle, 0f, PlayerAction.None)]
    [InlineData(CollisionProperties.Layers.PlayerVehicle | CollisionProperties.Layers.PlayerMelee, 0f, PlayerAction.Assault)]
    [InlineData(CollisionProperties.Layers.PlayerMelee, 0f, PlayerAction.Assault)]
    [InlineData(CollisionProperties.Layers.PlayerBullet, 30f, PlayerAction.Assault)]
    [InlineData(CollisionProperties.Layers.NpcVehicle, 30f, PlayerAction.None)]          // NPC cars produce nothing
    [InlineData(CollisionProperties.Layers.NpcMelee, 0f, PlayerAction.None)]
    public void Attribute_NeedsSpeedForARunOver(CollisionProperties.Layers layers, float speed, PlayerAction expected)
    {
        Assert.Equal(expected, PlayerActionClassifier.Attribute(layers, speed));
    }


    [Fact]
    public void NoneAction_ReportsNothing()
    {
        Assert.Null(PlayerActionClassifier.InteractionId(PlayerAction.None));
    }


    [Fact]
    public void OnlyTheEnvironmentBlocksAWitnessesView()
    {
        Assert.True(WitnessSelector.IsOccluder(null));
        Assert.True(WitnessSelector.IsOccluder(new CollisionProperties { SolidLayerMask = CollisionProperties.Layers.StaticEnvironment }));
        Assert.True(WitnessSelector.IsOccluder(new CollisionProperties { SolidLayerMask = CollisionProperties.Layers.MovableEnvironment }));
        Assert.False(WitnessSelector.IsOccluder(new CollisionProperties { SolidLayerMask = CollisionProperties.Layers.NpcCharacter }));
        Assert.False(WitnessSelector.IsOccluder(new CollisionProperties { SolidLayerMask = CollisionProperties.Layers.PlayerCharacter }));
        Assert.False(WitnessSelector.IsOccluder(new CollisionProperties { SolidLayerMask = CollisionProperties.Layers.NpcVehicle }));
    }


    [Fact]
    public void MaterialisedNpcWithoutSchedule_IsSkipped()
    {
        var (tm, npcs, positions) = Street();
        var withStranger = positions.Append((NpcSchedule.MakeNpcId(Cluster, 99), new Vector3(1, 0, 0))).ToArray();

        var report = tm.ReportPlayerAction(npcs[0].NpcId, "assault", Vector3.Zero, T0, withStranger);

        Assert.True(report.Victim.Applied);
        Assert.Equal(0.6f - 0.25f * 0.35f * 0.9f * 1.5f, npcs[1].Trust[PlayerTrust.Key], 4);
    }
}
