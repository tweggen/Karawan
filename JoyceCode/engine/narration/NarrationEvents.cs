namespace engine.narration;


/// <summary>
/// Emitted when a narration script starts.
/// </summary>
public class ScriptStartedEvent : engine.news.Event
{
    public const string EVENT_TYPE = "narration.script.started";

    public string ScriptName { get; set; }
    public string Mode { get; set; }
    public string InstanceId { get; set; }

    public ScriptStartedEvent(string scriptName, string mode, string instanceId)
        : base(EVENT_TYPE, scriptName)
    {
        ScriptName = scriptName;
        Mode = mode;
        InstanceId = instanceId;
    }
}


/// <summary>
/// Emitted when the runner enters a new node.
/// </summary>
public class NodeReachedEvent : engine.news.Event
{
    public const string EVENT_TYPE = "narration.node.reached";

    public string ScriptName { get; set; }
    public string NodeId { get; set; }
    public string InterpolatedText { get; set; }
    public string Speaker { get; set; }
    public string Animation { get; set; }
    public System.Collections.Generic.List<string> InterpolatedChoices { get; set; }

    public NodeReachedEvent(
        string scriptName, string nodeId, string interpolatedText,
        string speaker, string animation,
        System.Collections.Generic.List<string> interpolatedChoices)
        : base(EVENT_TYPE, nodeId)
    {
        ScriptName = scriptName;
        NodeId = nodeId;
        InterpolatedText = interpolatedText;
        Speaker = speaker;
        Animation = animation;
        InterpolatedChoices = interpolatedChoices;
    }
}


/// <summary>
/// Emitted when a narration script ends.
/// </summary>
public class ScriptEndedEvent : engine.news.Event
{
    public const string EVENT_TYPE = "narration.script.ended";

    public string ScriptName { get; set; }

    public ScriptEndedEvent(string scriptName)
        : base(EVENT_TYPE, scriptName)
    {
        ScriptName = scriptName;
    }
}


/// <summary>
/// Emitted when the speaking character changes.
/// </summary>
public class SpeakerChangedEvent : engine.news.Event
{
    public const string EVENT_TYPE = "narration.speaker.changed";

    public string Person { get; set; }
    public string Animation { get; set; }

    public SpeakerChangedEvent(string person, string animation)
        : base(EVENT_TYPE, person)
    {
        Person = person;
        Animation = animation;
    }
}


/// <summary>
/// Emitted when the narration state machine changes state.
/// Replaces the previous CurrentStateEvent.
/// </summary>
public class NarrationStateEvent : engine.news.Event
{
    public const string EVENT_TYPE = "narration.state.changed";

    public bool MayConverse { get; set; }
    public bool ShallBeInteractive { get; set; }

    public NarrationStateEvent(bool mayConverse, bool shallBeInteractive)
        : base(EVENT_TYPE, "")
    {
        MayConverse = mayConverse;
        ShallBeInteractive = shallBeInteractive;
    }
}
