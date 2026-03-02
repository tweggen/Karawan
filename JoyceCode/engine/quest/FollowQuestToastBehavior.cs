using System;
using DefaultEcs;
using engine;
using engine.behave;
using engine.news;

namespace engine.quest;

/// <summary>
/// Behavior that combines a timer (for auto-removal) with input handling (to follow a quest).
/// Subscribes to INPUT_BUTTON_PRESSED on attach and unsubscribes on detach.
/// </summary>
public class FollowQuestToastBehavior : ABehavior
{
    public required float Lifetime { get; set; }
    public required string QuestId { get; set; }

    private bool _iAmDoomed = false;

    private void _onInputButton(Event ev)
    {
        if (ev.Code != "<followquest>") return;
        ev.IsHandled = true;
        I.Get<ISatnavService>().FollowQuest(QuestId);
        // Remove the toast since its purpose (prompting to follow) is fulfilled
        I.Get<Engine>().AddDoomedEntity(_entity);
    }

    private Entity _entity;

    public override void OnAttach(in Engine engine, in Entity entity)
    {
        _entity = entity;
        I.Get<SubscriptionManager>().Subscribe(Event.INPUT_BUTTON_PRESSED, _onInputButton);
    }

    public override void OnDetach(in Entity entity)
    {
        I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_BUTTON_PRESSED, _onInputButton);
    }

    public override void Behave(in Entity entity, float dt)
    {
        if (!_iAmDoomed)
        {
            Lifetime -= dt;
            if (Lifetime <= 0)
            {
                _iAmDoomed = true;
                I.Get<Engine>().AddDoomedEntity(entity);
            }
        }
    }
}
