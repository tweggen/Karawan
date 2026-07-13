using System.Collections.Generic;
using System.Linq;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// GroupTypeRegistry classification tests. The registry drives the group
/// Type strings baked into sc-{hash} scenarios; an empty registry silently
/// classifies every group as "social" (the pre-Phase-E Chushi bug), so the
/// first test documents that behavior as a regression guard.
/// </summary>
public class GroupTypeRegistryTests
{
    private static NpcSchedule MakeNpc(int id, string role,
        float wealth, float morality, float reputation = 0.5f)
    {
        return new NpcSchedule
        {
            NpcId = id,
            Seed = id,
            Role = role,
            Properties = new Dictionary<string, float>
            {
                { "wealth", wealth },
                { "morality", morality },
                { "reputation", reputation },
                { "anger", 0f },
                { "fear", 0f }
            }
        };
    }

    private static Dictionary<int, NpcSchedule> ToDict(params NpcSchedule[] npcs)
        => npcs.ToDictionary(n => n.NpcId);

    /// <summary>
    /// Builds the same registry content as models/nogame.group-types.json
    /// (mirrored by Chushi/ConsoleMain.cs and TestRunner/TestRunnerMain.cs).
    /// </summary>
    private static GroupTypeRegistry MakeDefaultRegistry()
    {
        var r = new GroupTypeRegistry();
        r.Add("patrol_unit", new GroupTypeDefinition
        {
            Id = "patrol_unit", DisplayName = "Patrol Unit", Priority = 100,
            Rules = { new GroupClassificationRule
                { Type = "authority_threshold", Parameters = { ["minimum_ratio"] = 0.5f } } }
        });
        r.Add("criminal", new GroupTypeDefinition
        {
            Id = "criminal", DisplayName = "Criminal Syndicate", Priority = 90,
            Rules = { new GroupClassificationRule
                { Type = "combined_threshold", Parameters = { ["wealth_max"] = 0.3f, ["morality_max"] = 0.4f } } }
        });
        r.Add("trade", new GroupTypeDefinition
        {
            Id = "trade", DisplayName = "Trading Partnership", Priority = 80,
            Rules = { new GroupClassificationRule
                { Type = "combined_threshold", Parameters = { ["wealth_min"] = 0.5f, ["reputation_min"] = 0.5f } } }
        });
        r.Add("social", new GroupTypeDefinition
        {
            Id = "social", DisplayName = "Social Circle", Priority = 0
        });
        return r;
    }

    [Fact]
    public void EmptyRegistry_ClassifiesEverythingAsSocial()
    {
        var registry = new GroupTypeRegistry();
        var npcs = ToDict(
            MakeNpc(1, "drifter", wealth: 0.05f, morality: 0.05f),
            MakeNpc(2, "drifter", wealth: 0.05f, morality: 0.05f),
            MakeNpc(3, "drifter", wealth: 0.05f, morality: 0.05f));

        // Even an unambiguously criminal group falls through to "social"
        // when no types are registered.
        Assert.Equal("social", registry.ClassifyGroup(npcs.Keys.ToList(), npcs));
    }

    [Fact]
    public void CriminalRule_FiresOnPoorImmoralGroup()
    {
        var registry = MakeDefaultRegistry();
        var npcs = ToDict(
            MakeNpc(1, "drifter", wealth: 0.1f, morality: 0.2f),
            MakeNpc(2, "hustler", wealth: 0.2f, morality: 0.3f),
            MakeNpc(3, "drifter", wealth: 0.15f, morality: 0.1f));

        Assert.Equal("criminal", registry.ClassifyGroup(npcs.Keys.ToList(), npcs));
    }

    [Fact]
    public void TradeRule_FiresOnWealthyReputableGroup()
    {
        var registry = MakeDefaultRegistry();
        var npcs = ToDict(
            MakeNpc(1, "merchant", wealth: 0.7f, morality: 0.6f, reputation: 0.6f),
            MakeNpc(2, "merchant", wealth: 0.8f, morality: 0.7f, reputation: 0.7f),
            MakeNpc(3, "worker", wealth: 0.6f, morality: 0.5f, reputation: 0.5f));

        Assert.Equal("trade", registry.ClassifyGroup(npcs.Keys.ToList(), npcs));
    }

    [Fact]
    public void PatrolRule_FiresOnMajorityAuthority()
    {
        var registry = MakeDefaultRegistry();
        var npcs = ToDict(
            MakeNpc(1, "authority", wealth: 0.4f, morality: 0.8f),
            MakeNpc(2, "authority", wealth: 0.4f, morality: 0.8f),
            MakeNpc(3, "worker", wealth: 0.4f, morality: 0.8f));

        Assert.Equal("patrol_unit", registry.ClassifyGroup(npcs.Keys.ToList(), npcs));
    }

    [Fact]
    public void PriorityOrder_PatrolBeatsCriminalWhenBothMatch()
    {
        var registry = MakeDefaultRegistry();
        // Poor, immoral AND majority-authority: patrol_unit (priority 100)
        // must win over criminal (priority 90).
        var npcs = ToDict(
            MakeNpc(1, "authority", wealth: 0.1f, morality: 0.2f),
            MakeNpc(2, "authority", wealth: 0.1f, morality: 0.2f),
            MakeNpc(3, "drifter", wealth: 0.1f, morality: 0.2f));

        Assert.Equal("patrol_unit", registry.ClassifyGroup(npcs.Keys.ToList(), npcs));
    }

    [Fact]
    public void NoRuleMatches_FallsBackToSocial()
    {
        var registry = MakeDefaultRegistry();
        // Middle-of-the-road group: not poor enough for criminal, not
        // wealthy enough for trade, no authority members.
        var npcs = ToDict(
            MakeNpc(1, "worker", wealth: 0.4f, morality: 0.6f, reputation: 0.4f),
            MakeNpc(2, "worker", wealth: 0.45f, morality: 0.7f, reputation: 0.4f),
            MakeNpc(3, "socialite", wealth: 0.4f, morality: 0.8f, reputation: 0.45f));

        Assert.Equal("social", registry.ClassifyGroup(npcs.Keys.ToList(), npcs));
    }

    [Fact]
    public void EmptyMemberList_ReturnsUnknown()
    {
        var registry = MakeDefaultRegistry();
        Assert.Equal("unknown", registry.ClassifyGroup(new List<int>(), new Dictionary<int, NpcSchedule>()));
    }
}
