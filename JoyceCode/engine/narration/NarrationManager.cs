using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using engine.news;
using static engine.Logger;

namespace engine.narration;


/// <summary>
/// Central narration manager module. Loads scripts and triggers from the Mix DOM
/// at /narration, manages the state machine, and owns the active NarrationRunner.
///
/// State machine (same as legacy Narration):
///   Idle -> Conversation, Narration, ScriptedScene
///   Conversation -> Conversation, Narration, ScriptedScene, Idle
///   Narration -> Narration, ScriptedScene
///   ScriptedScene -> ScriptedScene
/// </summary>
public class NarrationManager : AModule
{
    public enum State
    {
        Idle,
        Conversation,
        Narration,
        ScriptedScene
    }

    private State _currentState = State.Idle;

    private Dictionary<string, NarrationScript> _scripts = new();
    private Dictionary<string, NarrationTrigger> _triggers = new();

    private NarrationRunner _activeRunner;
    private string _activeScriptName = "";
    private string _activeInstanceId = "";

    private readonly NarrationInterpolator _interpolator = new();
    private readonly NarrationConditionEvaluator _conditionEvaluator = new();

    /// <summary>
    /// Event handlers for narration event descriptors (e.g., "quest.trigger").
    /// </summary>
    private readonly Dictionary<string, Func<NarrationEventDescriptor, Task>> _eventHandlers = new();

    private string _startupScript = "";

    private bool _mayConverse = true;
    private bool _shallBeInteractive = true;

    public State CurrentState => _currentState;
    public bool MayConverse => _mayConverse;
    public bool ShallBeInteractive => _shallBeInteractive;
    public NarrationRunner ActiveRunner => _activeRunner;
    public NarrationInterpolator Interpolator => _interpolator;
    public NarrationConditionEvaluator ConditionEvaluator => _conditionEvaluator;


    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<InputEventPipeline>(),
        new SharedModule<Saver>()
    };


    /// <summary>
    /// Register a function for string interpolation in narration text.
    /// </summary>
    public void RegisterFunction(string name, Func<string[], string> fn)
    {
        _interpolator.RegisterFunction(name, fn);
        _conditionEvaluator.RegisterFunction(name, fn);
    }


    /// <summary>
    /// Register an async function for string interpolation.
    /// </summary>
    public void RegisterAsyncFunction(string name, Func<string[], Task<string>> fn)
    {
        _interpolator.RegisterAsyncFunction(name, fn);
    }


    /// <summary>
    /// Register a handler for narration event descriptors.
    /// When a node is entered that has events with matching type, the handler is called.
    /// </summary>
    public void RegisterEventHandler(string eventType, Func<NarrationEventDescriptor, Task> handler)
    {
        _eventHandlers[eventType] = handler;
    }


    /// <summary>
    /// Trigger a specific script by name.
    /// </summary>
    public async Task<NarrationRunner.NodeResult> TriggerScript(string scriptName, string mode, string instanceId)
    {
        if (!_scripts.TryGetValue(scriptName, out var script))
        {
            Warning($"NarrationManager: script '{scriptName}' not found.");
            return null;
        }

        State newState = _parseMode(mode);
        if (!_mayTransition(newState))
        {
            Trace($"NarrationManager: cannot transition from {_currentState} to {newState}.");
            return null;
        }

        _toState(newState);
        _activeScriptName = scriptName;
        _activeInstanceId = instanceId;
        _activeRunner = new NarrationRunner(script, _interpolator, _conditionEvaluator);

        // Emit script started event
        _pushEvent(new ScriptStartedEvent(scriptName, mode, instanceId));

        var result = await _activeRunner.Start();
        result = await _processAndAutoAdvance(result);
        return result;
    }


    /// <summary>
    /// Advance the active runner (no choices, follow goto).
    /// </summary>
    public async Task<NarrationRunner.NodeResult> Advance()
    {
        if (_activeRunner == null || _activeRunner.IsFinished)
        {
            _endScript();
            return null;
        }

        var result = await _activeRunner.Advance();
        result = await _processAndAutoAdvance(result);
        return result;
    }


    /// <summary>
    /// Choose a specific option and advance.
    /// </summary>
    public async Task<NarrationRunner.NodeResult> Choose(int choiceIndex)
    {
        if (_activeRunner == null || _activeRunner.IsFinished)
        {
            _endScript();
            return null;
        }

        var result = await _activeRunner.Choose(choiceIndex);
        result = await _processAndAutoAdvance(result);
        return result;
    }


    /// <summary>
    /// Trigger a script via a story advancement tag.
    /// Looks up the trigger at "story.advanceTo.{tag}".
    /// </summary>
    public async Task<NarrationRunner.NodeResult> AdvanceToTag(string tag)
    {
        string triggerPath = $"story.advanceTo.{tag}";
        if (_triggers.TryGetValue(triggerPath, out var trigger))
        {
            return await TriggerScript(trigger.ScriptName, trigger.Mode, "");
        }

        Warning($"NarrationManager: no trigger found for tag '{tag}'.");
        return null;
    }


    /// <summary>
    /// Check if the narration system is currently idle.
    /// </summary>
    public bool IsIdle() => _currentState == State.Idle;


    protected override void OnModuleActivate()
    {
        I.Get<casette.Loader>().WhenLoaded("/narration", _onNarrationLoaded);

        M<Saver>().OnBeforeSaveGame += _onBeforeSaveGame;
        M<Saver>().OnAfterLoadGame += _onAfterLoadGame;
    }


    protected override void OnModuleDeactivate()
    {
        M<Saver>().OnBeforeSaveGame -= _onBeforeSaveGame;
        M<Saver>().OnAfterLoadGame -= _onAfterLoadGame;
    }


    private void _onNarrationLoaded(string path, JsonNode jn)
    {
        if (jn is not JsonObject root) return;

        // Load bindings
        if (root.TryGetPropertyValue("bindings", out var bindingsNode) && bindingsNode is JsonObject bindingsObj)
        {
            foreach (var kvp in bindingsObj)
            {
                if (kvp.Value is JsonValue jv && jv.GetValueKind() == System.Text.Json.JsonValueKind.String)
                {
                    _interpolator.RegisterBinding(kvp.Key, jv.GetValue<string>());
                }
            }
        }

        // Load scripts
        if (root.TryGetPropertyValue("scripts", out var scriptsNode) && scriptsNode is JsonObject scriptsObj)
        {
            var newScripts = new Dictionary<string, NarrationScript>();
            foreach (var kvp in scriptsObj)
            {
                newScripts[kvp.Key] = NarrationScript.FromJson(kvp.Key, kvp.Value);
            }

            lock (_lo)
            {
                _scripts = newScripts;
            }
        }

        // Load triggers
        if (root.TryGetPropertyValue("triggers", out var triggersNode) && triggersNode is JsonObject triggersObj)
        {
            var newTriggers = new Dictionary<string, NarrationTrigger>();
            foreach (var kvp in triggersObj)
            {
                newTriggers[kvp.Key] = NarrationTrigger.FromJson(kvp.Key, kvp.Value);
            }

            lock (_lo)
            {
                _triggers = newTriggers;
            }

            // Subscribe to trigger events
            foreach (var trigger in newTriggers.Values)
            {
                var capturedTrigger = trigger;
                Subscribe(trigger.EventPath, ev =>
                {
                    if (!ev.IsHandled)
                    {
                        ev.IsHandled = true;
                        _engine.QueueMainThreadAction(async () =>
                        {
                            await TriggerScript(capturedTrigger.ScriptName, capturedTrigger.Mode, ev.Code);
                        });
                    }
                });
            }
        }

        // Load startup script name
        if (root.TryGetPropertyValue("startup", out var startupNode)
            && startupNode is JsonValue sv
            && sv.GetValueKind() == System.Text.Json.JsonValueKind.String)
        {
            _startupScript = sv.GetValue<string>();
        }

        Trace($"NarrationManager: loaded {_scripts.Count} scripts, {_triggers.Count} triggers, startup='{_startupScript}'.");
    }


    private async Task _processNodeResult(NarrationRunner.NodeResult result)
    {
        // Emit speaker changed event
        if (!string.IsNullOrEmpty(result.Speaker))
        {
            _pushEvent(new SpeakerChangedEvent(result.Speaker, result.Animation));
        }

        // Process node event descriptors
        foreach (var eventDesc in result.Events)
        {
            if (_eventHandlers.TryGetValue(eventDesc.Type, out var handler))
            {
                try
                {
                    await handler(eventDesc);
                }
                catch (Exception e)
                {
                    Warning($"NarrationManager: event handler for '{eventDesc.Type}' threw: {e.Message}");
                }
            }
            else
            {
                // Emit as a generic engine event
                _pushEvent(new Event($"narration.event.{eventDesc.Type}", _activeScriptName));
            }
        }

        // Emit node reached event (only for steps that have text or choices)
        if (!result.IsAutoAdvance)
        {
            _pushEvent(new NodeReachedEvent(
                _activeScriptName, result.NodeId, result.Text,
                result.Speaker, result.Animation, result.Choices));
        }
    }


    /// <summary>
    /// Process a node result, auto-advancing through event/speaker steps
    /// until we reach a step that requires user interaction or the script ends.
    /// </summary>
    private async Task<NarrationRunner.NodeResult> _processAndAutoAdvance(NarrationRunner.NodeResult result)
    {
        while (result != null)
        {
            await _processNodeResult(result);

            if (!result.IsAutoAdvance)
            {
                return result;
            }

            // Auto-advance: get the next step
            result = await _activeRunner.Advance();
        }

        _endScript();
        return null;
    }


    private void _endScript()
    {
        if (_activeRunner != null)
        {
            _pushEvent(new ScriptEndedEvent(_activeScriptName));
            _activeRunner = null;
            _activeScriptName = "";
            _activeInstanceId = "";
            _toState(State.Idle);
        }
    }


    private void _pushEvent(Event ev)
    {
        try
        {
            I.Get<EventQueue>().Push(ev);
        }
        catch (Exception)
        {
            // EventQueue may not be available during init
        }
    }


    private bool _mayTransition(State newState)
    {
        switch (_currentState)
        {
            case State.Idle:
                return true;
            case State.Conversation:
                return true; // Conversations are interruptible
            case State.Narration:
                return newState is State.Narration or State.ScriptedScene;
            case State.ScriptedScene:
                return newState == State.ScriptedScene;
            default:
                return false;
        }
    }


    private void _toState(State newState)
    {
        if (newState == _currentState) return;

        _currentState = newState;

        _mayConverse = newState is State.Idle or State.Conversation;
        _shallBeInteractive = newState is not State.ScriptedScene;

        _pushEvent(new NarrationStateEvent(_mayConverse, _shallBeInteractive));
    }


    private static State _parseMode(string mode)
    {
        return mode?.ToLowerInvariant() switch
        {
            "conversation" => State.Conversation,
            "narration" => State.Narration,
            "scriptedscene" => State.ScriptedScene,
            _ => State.Conversation
        };
    }


    private void _onBeforeSaveGame(object sender, object _)
    {
        // Save state is handled by the game-level module that reads from ActiveRunner
    }


    private void _onAfterLoadGame(object sender, object objGameState)
    {
        if (!string.IsNullOrEmpty(_startupScript))
        {
            // Delay startup narration to let the game world settle after load.
            Task.Delay(5000).ContinueWith(t =>
            {
                _engine.QueueMainThreadAction(async () =>
                {
                    if (_currentState == State.Idle)
                    {
                        await TriggerScript(_startupScript, "narration", "");
                    }
                });
            });
        }
    }
}
