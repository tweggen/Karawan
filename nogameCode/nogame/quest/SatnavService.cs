using engine;
using engine.news;
using engine.quest;
using engine.quest.components;
using static engine.Logger;

namespace nogame.quest;

/// <summary>
/// Central singleton service for the "followed quest" concept.
/// At most one active quest is followed at a time; only the followed quest
/// renders its goal marker and satnav route.
/// </summary>
public class SatnavService : ISatnavService
{
    private Engine _engine;
    private object _lo = new();

    private string _followedQuestId = null;
    private DefaultEcs.Entity _followedQuestEntity = default;

    public string FollowedQuestId
    {
        get { lock (_lo) { return _followedQuestId; } }
    }


    public bool IsFollowed(DefaultEcs.Entity questEntity)
    {
        lock (_lo)
        {
            return _followedQuestEntity.IsAlive && _followedQuestEntity == questEntity;
        }
    }


    public void FollowQuest(string questId)
    {
        if (questId == null)
        {
            UnfollowQuest();
            return;
        }

        DefaultEcs.Entity eQuest = default;
        foreach (var e in _engine.GetEcsWorld().GetEntities().With<QuestInfo>().AsEnumerable())
        {
            if (e.Get<QuestInfo>().QuestId == questId)
            {
                eQuest = e;
                break;
            }
        }

        if (!eQuest.IsAlive)
        {
            Warning($"SatnavService.FollowQuest: quest '{questId}' not found.");
            return;
        }

        string prevId;
        lock (_lo)
        {
            prevId = _followedQuestId;
            _followedQuestId = questId;
            _followedQuestEntity = eQuest;
        }

        _persistFollowedQuest(questId);

        if (prevId != null && prevId != questId)
        {
            I.Get<EventQueue>().Push(new QuestUnfollowedEvent(prevId));
        }

        I.Get<EventQueue>().Push(new QuestFollowedEvent(questId));

        Trace($"SatnavService: now following '{questId}'.");
    }


    public void UnfollowQuest()
    {
        string prevId;
        lock (_lo)
        {
            prevId = _followedQuestId;
            _followedQuestId = null;
            _followedQuestEntity = default;
        }

        _persistFollowedQuest(null);

        if (prevId != null)
        {
            I.Get<EventQueue>().Push(new QuestUnfollowedEvent(prevId));
        }

        Trace("SatnavService: unfollowed all quests.");
    }


    private void _persistFollowedQuest(string questId)
    {
        try
        {
            I.Get<nogame.modules.AutoSave>().GameState.FollowedQuestId = questId;
        }
        catch
        {
            // GameState may not be available early in startup — safe to ignore.
        }
    }


    private void _handleQuestTriggered(Event ev)
    {
        string currentlyFollowed;
        lock (_lo)
        {
            currentlyFollowed = _followedQuestId;
        }

        if (currentlyFollowed == null)
        {
            // Auto-follow the first triggered quest.
            _engine.QueueMainThreadAction(() => FollowQuest(ev.Code));
        }
    }


    private void _handleQuestDeactivated(Event ev)
    {
        string currentlyFollowed;
        lock (_lo)
        {
            currentlyFollowed = _followedQuestId;
        }

        if (ev.Code != currentlyFollowed) return;

        // The followed quest completed — clear follow state.
        lock (_lo)
        {
            _followedQuestId = null;
            _followedQuestEntity = default;
        }

        _persistFollowedQuest(null);

        // Auto-follow the next active quest, if any.
        _engine.QueueMainThreadAction(() =>
        {
            DefaultEcs.Entity nextQuest = default;
            foreach (var e in _engine.GetEcsWorld().GetEntities().With<QuestInfo>().AsEnumerable())
            {
                ref var qi = ref e.Get<QuestInfo>();
                if (qi.IsActive && qi.QuestId != ev.Code)
                {
                    nextQuest = e;
                    break;
                }
            }

            if (nextQuest.IsAlive)
            {
                FollowQuest(nextQuest.Get<QuestInfo>().QuestId);
            }
        });
    }


    private void _onBeforeSaveGame(object sender, object args)
    {
        _persistFollowedQuest(_followedQuestId);
    }


    private void _onAfterLoadGame(object sender, object args)
    {
        if (args is GameState gs && gs.FollowedQuestId != null)
        {
            _engine.QueueMainThreadAction(() => FollowQuest(gs.FollowedQuestId));
        }
    }


    public SatnavService()
    {
        _engine = I.Get<Engine>();
        I.Get<SubscriptionManager>().Subscribe(QuestTriggeredEvent.EVENT_TYPE, _handleQuestTriggered);
        I.Get<SubscriptionManager>().Subscribe(QuestDeactivatedEvent.EVENT_TYPE, _handleQuestDeactivated);
        I.Get<Saver>().OnBeforeSaveGame += _onBeforeSaveGame;
        I.Get<Saver>().OnAfterLoadGame += _onAfterLoadGame;
    }
}
