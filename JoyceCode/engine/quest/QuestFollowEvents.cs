namespace engine.quest;


/// <summary>
/// Emitted when a quest becomes the followed quest (drives satnav / shows marker).
/// Code contains the questId.
/// </summary>
public class QuestFollowedEvent : engine.news.Event
{
    public const string EVENT_TYPE = "quest.followed";

    public QuestFollowedEvent(string questId)
        : base(EVENT_TYPE, questId)
    {
    }
}


/// <summary>
/// Emitted when the followed quest is unfollowed.
/// Code contains the questId of the quest that was unfollowed.
/// </summary>
public class QuestUnfollowedEvent : engine.news.Event
{
    public const string EVENT_TYPE = "quest.unfollowed";

    public QuestUnfollowedEvent(string questId)
        : base(EVENT_TYPE, questId)
    {
    }
}
