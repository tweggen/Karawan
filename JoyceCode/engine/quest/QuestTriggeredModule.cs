using System;
using System.Numerics;
using DefaultEcs;
using engine.draw;
using engine.draw.components;
using engine.news;
using engine.quest.components;
using static engine.Logger;

namespace engine.quest;

/// <summary>
/// Handles quest triggered feedback via on-screen toast notifications.
/// Displays "{Title}" with "{ShortDescription}" when a quest is triggered.
/// </summary>
public class QuestTriggeredModule : AModule
{
    private void _onQuestTriggered(Event ev)
    {
        if (ev is not QuestTriggeredEvent qte)
        {
            return;
        }

        _engine.QueueMainThreadAction(() =>
        {
            try
            {
                // Find the quest entity by questId
                var world = _engine.GetEcsWorld();
                var questEntity = default(Entity);

                foreach (var entity in world.GetEntities().With<QuestInfo>())
                {
                    ref var qi = ref entity.Get<QuestInfo>();
                    if (qi.QuestId == qte.Code)
                    {
                        questEntity = entity;
                        break;
                    }
                }

                if (!questEntity.IsAlive)
                {
                    Warning($"QuestTriggeredModule: could not find quest entity for {qte.Code}");
                    return;
                }

                ref var questInfo = ref questEntity.Get<QuestInfo>();

                string toastText = questInfo.Title;
                if (!string.IsNullOrWhiteSpace(questInfo.ShortDescription))
                {
                    toastText += "\n" + questInfo.ShortDescription;
                }

                var eToast = _engine.CreateEntity("quest.triggered.toast");
                eToast.Set(new OSDText(
                    new Vector2(393f - 150f, 50f), new Vector2(300f, 80f),
                    toastText,
                    24, 0xff22aaaau, 0x88000000u,
                    HAlign.Center, VAlign.Center));
                eToast.Set(new engine.behave.components.Behavior(
                    new AutoRemoveBehavior() { Lifetime = 4.0f })
                { MaxDistance = short.MaxValue });
            }
            catch (Exception ex)
            {
                Warning($"QuestTriggeredModule._onQuestTriggered: {ex.Message}");
            }
        });
    }

    protected override void OnModuleActivate()
    {
        Subscribe(QuestTriggeredEvent.EVENT_TYPE, _onQuestTriggered);
    }

    protected override void OnModuleDeactivate()
    {
        Unsubscribe(QuestTriggeredEvent.EVENT_TYPE, _onQuestTriggered);
    }
}
