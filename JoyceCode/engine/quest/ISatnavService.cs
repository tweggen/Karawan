namespace engine.quest;

/// <summary>
/// Central service for "followed quest" tracking.
/// At most one active quest is followed at a time; only the followed quest
/// renders its goal marker and satnav route.
/// </summary>
public interface ISatnavService
{
    /// <summary>The quest ID currently being followed, or null if none.</summary>
    string FollowedQuestId { get; }

    /// <summary>Returns true if the given entity is the currently followed quest.</summary>
    bool IsFollowed(DefaultEcs.Entity questEntity);

    /// <summary>
    /// Follow the quest with the given ID.
    /// Fires QuestUnfollowedEvent for the previous quest (if any) and
    /// QuestFollowedEvent for the new quest.
    /// </summary>
    void FollowQuest(string questId);

    /// <summary>
    /// Stop following the current quest.
    /// Fires QuestUnfollowedEvent.
    /// </summary>
    void UnfollowQuest();
}
