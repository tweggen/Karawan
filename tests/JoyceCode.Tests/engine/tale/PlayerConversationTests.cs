using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S1-c (KAR-456): what a conversation applies at its end, when the
/// player turns violent, and through tale.interact / tale.pay. The nogame
/// wiring (TaleModule handlers, TaleConversationBehavior) calls exactly these.
/// </summary>
public class PlayerConversationTests
{
    private const string Script = "tale.role.merchant";
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);

    private static NpcSchedule Npc(int index = 1) => new()
    {
        NpcId = NpcSchedule.MakeNpcId(3, index), ClusterIndex = 3, NpcIndex = index, Role = "merchant",
        Properties = new Dictionary<string, float> { ["fear"] = 0.1f, ["anger"] = 0.1f, ["wealth"] = 0.2f },
        Trust = new Dictionary<int, float>()
    };

    private static (TaleManager Tm, NpcSchedule Npc, ConversationSession Session) Setup()
    {
        var tm = ShippedInteractions.Manager();
        var npc = Npc();
        tm.RegisterNpc(npc);
        return (tm, npc, new ConversationSession(npc.NpcId, Script));
    }


    [Fact]
    public void NaturalEnd_AppliesChatThroughTheRegistry_RateLimited()
    {
        var (tm, npc, session) = Setup();

        var first = PlayerConversation.OnEnded(tm, session, Script, T0);
        Assert.True(first.Applied);
        Assert.Equal(PlayerTrust.FirstContact + 0.02f * 0.85f, npc.Trust[PlayerTrust.Key], 4);

        // Talking again ten game minutes later counts a quarter (chat: 60 min, × 0.25).
        var again = PlayerConversation.OnEnded(tm, new ConversationSession(npc.NpcId, Script), Script, T0.AddMinutes(10));
        Assert.Equal(0.25f, again.Scale);
    }


    [Fact]
    public void WithoutTheChatInteraction_AConversationChangesNothing()
    {
        var tm = ShippedInteractions.Manager(n => (string)n["id"] != "chat");
        var npc = Npc();
        tm.RegisterNpc(npc);

        Assert.False(PlayerConversation.OnEnded(tm, new ConversationSession(npc.NpcId, Script), Script, T0).Applied);
        Assert.False(PlayerTrust.Knows(npc));
    }


    [Fact]
    public void AnotherScriptEnding_AppliesNothing()
    {
        var (tm, npc, session) = Setup();

        // A quest narration started during the conversation ends; the conversation does not.
        Assert.False(PlayerConversation.OnEnded(tm, session, "nogame.quests.VisitAgentTwelve", T0).Applied);
        Assert.False(PlayerConversation.OnEnded(tm, null, Script, T0).Applied);
        Assert.False(PlayerTrust.Knows(npc));
    }


    [Fact]
    public void HittingThePartner_EndsTheConversation_AppliesAssault_AndNoChat()
    {
        var (tm, npc, session) = Setup();
        npc.Trust[PlayerTrust.Key] = 0.5f;

        Assert.True(PlayerConversation.OnViolence(tm, session, npc.NpcId, PlayerAction.Assault, T0));
        Assert.True(session.CancelledByViolence);
        Assert.Equal(0.25f, npc.Trust[PlayerTrust.Key], 4);
        Assert.Equal(1f, npc.Properties["player_fact.hit_me"]);

        // The cancel pushes ScriptEnded for the conversation's own script: no chat on top.
        Assert.False(PlayerConversation.OnEnded(tm, session, Script, T0.AddSeconds(1)).Applied);
        Assert.Equal(0.25f, npc.Trust[PlayerTrust.Key], 4);

        // The strategy's hit handler reports the same hit again: nothing more.
        Assert.False(PlayerConversation.OnViolence(tm, session, npc.NpcId, PlayerAction.None, T0.AddSeconds(1)));
    }


    [Fact]
    public void ACrashOnThePartner_EndsTheConversation_WithoutAttribution()
    {
        var (tm, npc, session) = Setup();

        Assert.True(PlayerConversation.OnViolence(tm, session, npc.NpcId, PlayerAction.None, T0));
        Assert.True(session.CancelledByViolence);
        Assert.False(PlayerTrust.Knows(npc));
        Assert.False(PlayerConversation.OnEnded(tm, session, Script, T0).Applied);
    }


    [Fact]
    public void HittingABystander_LeavesTheConversationAlone()
    {
        var (tm, npc, session) = Setup();
        var bystander = Npc(2);
        tm.RegisterNpc(bystander);

        Assert.False(PlayerConversation.OnViolence(tm, session, bystander.NpcId, PlayerAction.Assault, T0));
        Assert.False(session.CancelledByViolence);
        Assert.False(PlayerTrust.Knows(bystander));
        Assert.True(PlayerConversation.OnEnded(tm, session, Script, T0).Applied);
    }


    [Fact]
    public void Interact_AppliesTheInteraction_AndReplacesTheDefaultChat()
    {
        var (tm, npc, session) = Setup();

        var r = PlayerConversation.Interact(tm, session, "help", T0);

        Assert.Equal(0.49f, r.TrustAfter, 4);
        Assert.True(session.AppliedInteraction);
        Assert.False(PlayerConversation.OnEnded(tm, session, Script, T0).Applied);
        Assert.Equal(0.49f, npc.Trust[PlayerTrust.Key], 4);
    }


    [Fact]
    public void Pay_NeedsEnoughCash_SubtractsIt_RaisesTrustAndWealth()
    {
        var (tm, npc, session) = Setup();

        int cash = 15;
        Assert.False(PlayerConversation.Pay(tm, session, "pay", 20, ref cash, T0));
        Assert.Equal(15, cash);
        Assert.False(PlayerTrust.Knows(npc));
        Assert.False(session.AppliedInteraction);

        cash = 30;
        Assert.True(PlayerConversation.Pay(tm, session, "pay", 20, ref cash, T0));
        Assert.Equal(10, cash);
        Assert.Equal(PlayerTrust.FirstContact + 0.06f * 0.85f, npc.Trust[PlayerTrust.Key], 4);
        Assert.Equal(0.24f, npc.Properties["wealth"], 4);           // 0.2 + 20 × 0.002
        Assert.True(session.AppliedInteraction);
        Assert.False(PlayerConversation.OnEnded(tm, session, Script, T0).Applied);
    }


    [Theory]
    [InlineData("help", 10)]
    [InlineData("assault", 10)]
    [InlineData(null, 10)]
    [InlineData("pay", 0)]
    [InlineData("pay", -5)]
    public void Pay_RefusesNonPaymentsAndNonPositiveAmounts(string id, int amount)
    {
        var (tm, npc, session) = Setup();
        int cash = 100;
        Assert.False(PlayerConversation.Pay(tm, session, id, amount, ref cash, T0));
        Assert.Equal(100, cash);
        Assert.False(PlayerTrust.Knows(npc));
    }


    [Fact]
    public void AfterViolence_ScriptActionsDoNothing()
    {
        var (tm, npc, session) = Setup();
        PlayerConversation.OnViolence(tm, session, npc.NpcId, PlayerAction.None, T0);

        int cash = 100;
        Assert.False(PlayerConversation.Pay(tm, session, "pay", 20, ref cash, T0));
        Assert.Equal(100, cash);
        Assert.False(PlayerConversation.Interact(tm, session, "help", T0).Applied);
    }


    /* The scripts that use the new events: what is offered when. */
    private static JsonNode Conversation(string file) => JsonNode.Parse(File.ReadAllText(
        Path.Combine(ShippedInteractions.RepoRoot(), "models", "tale", "conversations", file)))!;

    private static IEnumerable<JsonNode> Choices(JsonNode script, string node) =>
        script["nodes"]![node]!["flow"]!.AsArray().Where(f => f!["choices"] != null)
            .SelectMany(f => f!["choices"]!.AsArray())!;

    private static JsonNode Event(JsonNode script, string node) =>
        script["nodes"]![node]!["flow"]!.AsArray().First(f => f!["events"] != null)!["events"]![0]!;


    [Fact]
    public void MerchantScript_OffersPayOnlyWithCash_AndHelp()
    {
        var merchant = Conversation("tale.role.merchant.json");
        var choices = Choices(merchant, "desperate").ToList();

        var pay = choices.Single(c => (string)c["goto"] == "paid");
        Assert.Equal("props.player.cash >= 20", (string)pay["condition"]);
        Assert.Equal("tale.pay", (string)Event(merchant, "paid")["type"]);
        Assert.Equal("pay", (string)Event(merchant, "paid")["id"]);
        Assert.Equal(20, (int)Event(merchant, "paid")["amount"]);
        Assert.Equal("help", (string)Event(merchant, "helped")["id"]);
        Assert.Contains(choices, c => (string)c["goto"] == "leave" && c["condition"] == null);
    }


    [Fact]
    public void AuthorityScript_OffersABribe_AndPayIdsAreKnown()
    {
        var authority = Conversation("tale.role.authority.json");
        var bribe = Choices(authority, "acquaintance").Single(c => (string)c["goto"] == "bribed");
        Assert.Equal("props.player.cash >= 50", (string)bribe["condition"]);
        Assert.Equal("bribe", (string)Event(authority, "bribed")["id"]);
        Assert.Equal(50, (int)Event(authority, "bribed")["amount"]);
        Assert.All(new[] { "pay", "bribe" }, id => Assert.Contains(id, PlayerConversation.PaymentInteractions));
    }


    [Theory]
    [InlineData("tale.role.merchant.json")]
    [InlineData("tale.generic.json")]
    [InlineData("tale.tag.routine.json")]
    public void HostileBranch_ComesFirst_ForSomeoneThePlayerHit(string file)
    {
        var entry = Conversation(file)["nodes"]!["entry"]!["flow"]![0]!["goto"]!.AsArray();
        Assert.Equal("props.npc.player_fact.hit_me == 1", (string)entry[0]!["if"]);
        Assert.Equal("hostile", (string)entry[0]!["goto"]);
        Assert.Equal("hit_me", ShippedInteractions.Registry().Get("assault").Player.TargetFact);
    }
}
