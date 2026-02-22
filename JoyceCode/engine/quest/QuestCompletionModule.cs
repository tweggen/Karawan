using System;
using System.Numerics;
using builtin.tools;
using engine.draw;
using engine.draw.components;
using engine.news;

namespace engine.quest;

/// <summary>
/// Handles generic quest completion feedback via on-screen toast notifications.
/// Displays "Finished {Title}" for successful quests and "Missed {Title}" for failed ones.
/// </summary>
public class QuestCompletionModule : AModule
{
    private void _onQuestDeactivated(Event ev)
    {
        if (ev is QuestDeactivatedEvent qde)
        {
            _engine.QueueMainThreadAction(() =>
            {
                string toastText = qde.IsSuccess
                    ? $"Finished {qde.Title}"
                    : $"Missed {qde.Title}";
                uint textColor = qde.IsSuccess ? 0xff44cc44u : 0xffcc4444u;

                var eToast = _engine.CreateEntity("quest.completion.toast");
                eToast.Set(new OSDText(
                    new Vector2(393f - 150f, 120f), new Vector2(300f, 50f),
                    toastText,
                    36, textColor, 0x88000000u,
                    HAlign.Center));
                eToast.Set(new engine.behave.components.Behavior(
                    new AutoRemoveBehavior() { Lifetime = 3.0f })
                { MaxDistance = short.MaxValue });
            });
        }
    }

    protected override void OnModuleActivate()
    {
        Subscribe(QuestDeactivatedEvent.EVENT_TYPE, _onQuestDeactivated);
    }

    protected override void OnModuleDeactivate()
    {
        Unsubscribe(QuestDeactivatedEvent.EVENT_TYPE, _onQuestDeactivated);
    }
}
