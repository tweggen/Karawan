namespace engine.quest.components;

/**
 * Represents a single active quest.
 */
[IsPersistable]
public struct Quest
{
    public IQuest ActiveQuest { get; set; }
}