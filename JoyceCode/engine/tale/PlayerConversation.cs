using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// TALE S1-c: one conversation of the player with one NPC, from "E to talk"
/// until its script ends. Logical thread only.
/// </summary>
public sealed class ConversationSession
{
    public ConversationSession(int npcId, string scriptName)
    {
        NpcId = npcId;
        ScriptName = scriptName;
    }

    public int NpcId { get; }

    /// <summary>The conversation script; a ScriptEndedEvent of any other script is not this conversation's end.</summary>
    public string ScriptName { get; }

    /// <summary>The script applied an interaction itself (tale.interact, tale.pay): no default chat at the end.</summary>
    public bool AppliedInteraction { get; set; }

    /// <summary>The player hit or rammed the partner: the conversation ends without a chat.</summary>
    public bool CancelledByViolence { get; set; }
}


/// <summary>
/// What a conversation with the player does to the NPC (Phase C4; S1-c). A
/// conversation that ends naturally applies the "chat" interaction through
/// the registry (rate-limited by its player block), unless the script
/// applied an interaction of its own or the player turned violent.
/// tale.npc.remember stores a player_fact.
/// </summary>
public static class PlayerConversation
{
    public const string ChatInteraction = "chat";

    /// <summary>Interactions tale.pay accepts.</summary>
    public static readonly IReadOnlySet<string> PaymentInteractions = new HashSet<string> { "pay", "bribe" };


    /// <summary>
    /// <paramref name="endedScriptName"/> ended. Applies the default chat if it
    /// is the session's script, the conversation was not cut short by violence
    /// and the script applied nothing itself; otherwise nothing.
    /// </summary>
    public static PlayerInteractionResult OnEnded(TaleManager manager, ConversationSession session,
        string endedScriptName, DateTime gameNow)
    {
        if (session == null || endedScriptName != session.ScriptName) return PlayerInteractionResult.None;
        if (session.CancelledByViolence || session.AppliedInteraction) return PlayerInteractionResult.None;
        return manager.ApplyPlayerInteraction(session.NpcId, ChatInteraction, gameNow);
    }


    /// <summary>
    /// The player hit (<see cref="PlayerAction.Assault"/>) or rammed
    /// (<see cref="PlayerAction.RunOver"/>) NPC <paramref name="npcId"/>. If it
    /// is the conversation partner, the session is marked cancelled by
    /// violence, the action is applied to the NPC, and true is returned: the
    /// caller then closes the dialogue. A bystander's injury leaves the
    /// conversation alone (S1-d attributes those).
    /// </summary>
    public static bool OnViolence(TaleManager manager, ConversationSession session, int npcId,
        PlayerAction action, DateTime gameNow)
    {
        if (session == null || session.NpcId != npcId || session.CancelledByViolence) return false;
        session.CancelledByViolence = true;
        string id = PlayerActionClassifier.InteractionId(action);
        if (id != null) manager.ApplyPlayerInteraction(npcId, id, gameNow);
        return true;
    }


    /// <summary>tale.interact: the script applies <paramref name="interactionId"/> to the partner.</summary>
    public static PlayerInteractionResult Interact(TaleManager manager, ConversationSession session,
        string interactionId, DateTime gameNow)
    {
        if (session == null || session.CancelledByViolence) return PlayerInteractionResult.None;
        session.AppliedInteraction = true;
        return manager.ApplyPlayerInteraction(session.NpcId, interactionId, gameNow);
    }


    /// <summary>
    /// tale.pay: the player gives the partner <paramref name="amount"/> of
    /// <paramref name="cash"/> as <paramref name="interactionId"/> (pay or
    /// bribe). Refused, with cash unchanged, if the id is no payment, the
    /// amount is not positive or the player cannot afford it.
    /// </summary>
    public static bool Pay(TaleManager manager, ConversationSession session, string interactionId,
        int amount, ref int cash, DateTime gameNow)
    {
        if (session == null || session.CancelledByViolence) return false;
        if (interactionId == null || !PaymentInteractions.Contains(interactionId)) return false;
        if (amount <= 0 || amount > cash) return false;
        if (manager.GetSchedule(session.NpcId) == null) return false;

        cash -= amount;
        session.AppliedInteraction = true;
        manager.ApplyPlayerInteraction(session.NpcId, interactionId, gameNow, amount);
        return true;
    }


    /// <summary>tale.npc.remember: the NPC remembers <paramref name="fact"/> about the player.</summary>
    public static void Remember(NpcSchedule npc, string fact)
    {
        npc.Properties ??= new Dictionary<string, float>();
        npc.Properties[$"player_fact.{fact}"] = 1f;
    }
}
