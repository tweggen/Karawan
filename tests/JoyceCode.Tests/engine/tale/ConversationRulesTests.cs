using System;
using System.Collections.Generic;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// The headless-checkable parts of the TALE conversation system (Phase C,
/// KAR-433): which script an NPC talks with, the mood words scripts use, and
/// what a finished conversation does to the NPC's trust in the player. The
/// rest of Phase C needs the running game; see docs/tale/docs/tests/PHASE_C.md.
/// </summary>
public class ConversationRulesTests
{
    private static StoryletDefinition Storylet(string id, string conversationScript = null, params string[] tags) =>
        new() { Id = id, ConversationScript = conversationScript, Tags = tags };

    private static Func<string, bool> Exists(params string[] scripts) => new HashSet<string>(scripts).Contains;


    // C2 01, C2 04: the explicit override wins over everything, even a missing script.
    [Fact]
    public void Resolve_ExplicitOverrideWins()
    {
        var s = Storylet("lunch_break", "tale.custom", "eating");
        Assert.Equal("tale.custom", ConversationScriptResolver.Resolve(s, "worker",
            Exists("tale.lunch_break", "tale.tag.eating", "tale.role.worker")));
    }

    [Fact]
    public void Resolve_StoryletIdBeforeTag()
    {
        var s = Storylet("lunch_break", null, "eating");
        Assert.Equal("tale.lunch_break", ConversationScriptResolver.Resolve(s, "worker",
            Exists("tale.lunch_break", "tale.tag.eating", "tale.role.worker")));
    }

    // C2 02, C2 03, C2 05: the first tag that has a script.
    [Fact]
    public void Resolve_FirstTagWithAScript()
    {
        var s = Storylet("commute", null, "outdoor", "routine", "eating");
        Assert.Equal("tale.tag.routine", ConversationScriptResolver.Resolve(s, "worker",
            Exists("tale.tag.routine", "tale.tag.eating", "tale.role.worker")));
    }

    // C1 06, C1 07: no storylet or tag script, so the role's.
    [Theory]
    [InlineData("worker")]
    [InlineData("merchant")]
    public void Resolve_RoleFallback(string role)
    {
        var s = Storylet("wander", null, "outdoor");
        Assert.Equal($"tale.role.{role}", ConversationScriptResolver.Resolve(s, role,
            Exists("tale.role.worker", "tale.role.merchant")));
    }

    [Fact]
    public void Resolve_GenericLast_AndForNoStorylet()
    {
        Assert.Equal("tale.generic", ConversationScriptResolver.Resolve(Storylet("wander"), "drifter", Exists()));
        Assert.Equal("tale.generic", ConversationScriptResolver.Resolve(null, "worker", Exists("tale.role.worker")));
    }


    // C3 01..04.
    [Theory]
    [InlineData(0.8f, 0.8f, 0.8f, 0.1f, "frustrated")]
    [InlineData(0.2f, 0.8f, 0.8f, 0.5f, "desperate")]
    [InlineData(0.2f, 0.3f, 0.8f, 0.1f, "desperate")]
    [InlineData(0.2f, 0.3f, 0.8f, 0.5f, "tired")]
    [InlineData(0.7f, 0.7f, 0.7f, 0.2f, "neutral")]
    public void Mood_ByThresholdAndOrder(float anger, float hunger, float fatigue, float wealth, string mood)
    {
        Assert.Equal(mood, NpcMoodText.Mood(anger, hunger, fatigue, wealth));
    }

    [Theory]
    [InlineData(0.1f, "broke")]
    [InlineData(0.15f, "struggling")]
    [InlineData(0.35f, "comfortable")]
    [InlineData(0.65f, "comfortable")]
    [InlineData(0.66f, "wealthy")]
    public void WealthLabel_ByThreshold(float wealth, string label)
    {
        Assert.Equal(label, NpcMoodText.WealthLabel(wealth));
    }


    // C4 01, 02 (scale since S1-a): the first conversation starts at the
    // first-contact value, every conversation adds 0.02, and the NPC is marked.
    [Fact]
    public void ConversationTrust_StartsAtFirstContact_AndRisesPerConversation()
    {
        var tm = new TaleManager();
        var npc = new NpcSchedule { NpcId = 3 };

        Assert.Equal(PlayerTrust.FirstContact + 0.02f, PlayerConversation.OnEnded(tm, npc), 4);
        Assert.Equal(PlayerTrust.FirstContact + 0.04f, PlayerConversation.OnEnded(tm, npc), 4);
        Assert.Equal(0.19f, npc.Trust[-1], 4);
        Assert.True(npc.HasPlayerDeviation);
    }

    [Fact]
    public void ConversationTrust_IsCappedAtOne()
    {
        var npc = new NpcSchedule { NpcId = 3, Trust = new Dictionary<int, float> { [-1] = 0.99f } };
        Assert.Equal(1f, PlayerConversation.OnEnded(new TaleManager(), npc));
    }

    private static float _gate(string script)
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "models", "tale", "conversations")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        string text = System.IO.File.ReadAllText(
            System.IO.Path.Combine(dir!.FullName, "models", "tale", "conversations", script));
        var m = System.Text.RegularExpressions.Regex.Match(text, @"props\.npc\.trust_player > ([0-9.]+)");
        Assert.True(m.Success, script);
        return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Conversations that must have ended before the gate opens.</summary>
    private static int _conversationsUntil(float gate)
    {
        var tm = new TaleManager();
        var npc = new NpcSchedule { NpcId = 3 };
        int n = 0;
        while (!(PlayerTrust.Knows(npc) && PlayerTrust.Get(npc) > gate) && n < 1000)
        {
            PlayerConversation.OnEnded(tm, npc);
            n++;
        }
        return n;
    }

    // S1-a moved first contact from 0.5 to 0.15; the role-script gates were
    // moved with it so they open after as many conversations as before.
    [Theory]
    [InlineData("tale.role.merchant.json", 3)]
    [InlineData("tale.role.authority.json", 8)]
    public void RoleScriptTrustGates_OpenAfterTheSameNumberOfConversations(string script, int conversations)
    {
        Assert.Equal(conversations, _conversationsUntil(_gate(script)));
    }


    // C4 06.
    [Fact]
    public void Remember_StoresAPlayerFact()
    {
        var npc = new NpcSchedule { NpcId = 3 };
        PlayerConversation.Remember(npc, "owes_money");
        Assert.Equal(1f, npc.Properties["player_fact.owes_money"]);
        Assert.True(npc.IsKnownToPlayer);
    }
}
