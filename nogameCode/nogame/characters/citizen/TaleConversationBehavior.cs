using System;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.behave.components;
using engine.news;
using engine.tale;
using nogame.modules.story;
using nogame.modules.tale;
using nogame.tools;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Behavior for TALE NPCs: allows player to initiate conversations via E key.
/// Extends ANearbyBehavior to handle "E to Talk" prompt and interaction.
/// Attaches/detaches based on activity phase and indoor/outdoor status.
/// </summary>
public class TaleConversationBehavior : ANearbyBehavior
{
    private TaleManager _taleManager;
    private Narration _narration;
    private int _npcId;

    public override string Prompt => "E to Talk";

    public override string Name => "TaleConversation";

    public override float Distance { get; set; } = 12f;

    public TaleConversationBehavior(int npcId)
    {
        _npcId = npcId;
    }

    protected override void OnAction(Event ev)
    {
        try
        {
            _taleManager = I.Get<TaleManager>();
            _narration = I.Get<Narration>();

            if (_taleManager == null || _narration == null)
            {
                Trace($"TALE CONVERSATION: TaleManager or Narration not available");
                return;
            }

            var schedule = _taleManager.GetSchedule(_npcId);
            if (schedule == null)
            {
                Trace($"TALE CONVERSATION: NPC {_npcId} schedule not found");
                return;
            }

            // Get current storylet to determine conversation script
            var currentStorylet = _taleManager.GetCurrentStorylet(_npcId);
            if (currentStorylet == null)
            {
                Trace($"TALE CONVERSATION: NPC {_npcId} has no current storylet");
                return;
            }

            // Inject TALE properties into narration Props
            TaleNarrationBindings.InjectNpcProps(schedule);

            // Resolve conversation script using 5-level fallback
            string scriptName = TaleNarrationBindings.ResolveScript(currentStorylet, schedule.Role);

            Trace($"TALE CONVERSATION: NPC {_npcId} ({schedule.Role}) triggered conversation, using script '{scriptName}'");

            // Trigger conversation in narration system
            _narration.TriggerConversation(scriptName, _npcId.ToString());
        }
        catch (Exception e)
        {
            Error($"TALE CONVERSATION: Exception in OnAction: {e.Message}\n{e.StackTrace}");
        }
    }
}
