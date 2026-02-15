namespace engine.quest;


/// <summary>
/// Emitted when a quest has been triggered and its entity is set up.
/// Code contains the questId.
/// </summary>
public class QuestTriggeredEvent : engine.news.Event
{
    public const string EVENT_TYPE = "quest.triggered";

    public QuestTriggeredEvent(string questId)
        : base(EVENT_TYPE, questId)
    {
    }
}


/// <summary>
/// Emitted when a quest is deactivated and disposed.
/// Code contains the questId.
/// </summary>
public class QuestDeactivatedEvent : engine.news.Event
{
    public const string EVENT_TYPE = "quest.deactivated";

    public string Title { get; init; }
    public bool IsSuccess { get; init; }

    public QuestDeactivatedEvent(string questId, string title = "", bool isSuccess = true)
        : base(EVENT_TYPE, questId)
    {
        Title = title;
        IsSuccess = isSuccess;
    }
}
