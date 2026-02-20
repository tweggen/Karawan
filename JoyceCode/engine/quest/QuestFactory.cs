using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DefaultEcs;
using engine.news;
using engine.quest.components;
using static engine.Logger;

namespace engine.quest;

/// <summary>
/// Lightweight service for creating and managing strategy-based quest entities.
/// </summary>
public class QuestFactory
{
    private Engine _engine;

    /// <summary>
    /// Strategy factories: given an engine and a quest entity, set QuestInfo + Strategy components.
    /// May perform async work (e.g. computing target locations).
    /// </summary>
    private SortedDictionary<string, Func<Engine, Entity, Task>> _strategyFactories = new();


    public void RegisterQuest(string questId, Func<Engine, Entity, Task> strategyFactory)
    {
        _strategyFactories[questId] = strategyFactory;
    }


    public bool HasQuest(string questId) => _strategyFactories.ContainsKey(questId);


    public async Task TriggerQuest(string questId, bool activate)
    {
        Entity existing = default;
        await _engine.TaskMainThread(() =>
        {
            existing = _engine.GetEcsWorld().GetEntities()
                .With<QuestInfo>()
                .AsEnumerable()
                .FirstOrDefault(e => e.Get<QuestInfo>().QuestId == questId);
        });

        if (existing.IsAlive)
        {
            if (activate)
            {
                await _engine.TaskMainThread(() =>
                {
                    ref var qi = ref existing.Get<QuestInfo>();
                    if (!qi.IsActive)
                    {
                        qi.IsActive = true;
                    }
                    else
                    {
                        Warning($"Quest {questId} already active.");
                    }
                });
            }

            return;
        }

        if (!_strategyFactories.TryGetValue(questId, out var factory))
        {
            Error($"Unknown quest: {questId}");
            return;
        }

        Entity eQuest = default;
        await _engine.TaskMainThread(() =>
        {
            eQuest = _engine.CreateEntity($"quest {questId}");
        });

        // Run factory — sets QuestInfo + Strategy on the entity.
        await factory(_engine, eQuest);

        if (activate)
        {
            await _engine.TaskMainThread(() =>
            {
                ref var qi = ref eQuest.Get<QuestInfo>();
                qi.IsActive = true;
            });
        }

        I.Get<EventQueue>().Push(new QuestTriggeredEvent(questId));
    }


    /// <summary>
    /// Deactivate and dispose a quest entity.
    /// Call this from strategy completion handlers.
    /// </summary>
    public void DeactivateQuest(Entity eQuest, bool isSuccess = true)
    {
        _engine.QueueMainThreadAction(() =>
        {
            if (!eQuest.IsAlive) return;

            string questId = null;
            string title = "";
            if (eQuest.Has<QuestInfo>())
            {
                ref var qi = ref eQuest.Get<QuestInfo>();
                questId = qi.QuestId;
                title = qi.Title ?? "";
                qi.IsActive = false;
            }

            try
            {
                // Removing Strategy triggers StrategyManager → OnExit + OnDetach.
                if (eQuest.Has<engine.behave.components.Strategy>())
                {
                    eQuest.Remove<engine.behave.components.Strategy>();
                }

                eQuest.Dispose();
                I.Get<Saver>().Save("quest deactivated");
            }
            finally
            {
                if (questId != null)
                {
                    I.Get<EventQueue>().Push(new QuestDeactivatedEvent(questId, title, isSuccess));
                }
            }
        });
    }


    public QuestFactory()
    {
        _engine = I.Get<Engine>();
    }
}
