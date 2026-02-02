using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static engine.Logger;

namespace engine.narration;


/// <summary>
/// Runs a single narration script, tracking current node, flow index,
/// visit counts, and resolving goto targets (simple, conditional, random, sequential).
/// </summary>
public class NarrationRunner
{
    private static readonly Random _rng = new();

    private NarrationScript _script;
    private NarrationInterpolator _interpolator;
    private NarrationConditionEvaluator _conditionEvaluator;

    private string _currentNodeId;
    private NarrationNode _currentNode;
    private int _flowIndex;

    /// <summary>
    /// Accumulated speaker/animation from Speaker statements preceding a Text statement.
    /// </summary>
    private string _currentSpeaker = "";
    private string _currentAnimation = "";

    /// <summary>
    /// Per-node visit count, used for sequential goto.
    /// </summary>
    private Dictionary<string, int> _visitCounts = new();

    public bool IsFinished { get; private set; }

    public string CurrentNodeId => _currentNodeId;
    public NarrationNode CurrentNode => _currentNode;
    public NarrationScript Script => _script;


    public NarrationRunner(
        NarrationScript script,
        NarrationInterpolator interpolator,
        NarrationConditionEvaluator conditionEvaluator)
    {
        _script = script ?? throw new ArgumentNullException(nameof(script));
        _interpolator = interpolator ?? throw new ArgumentNullException(nameof(interpolator));
        _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
    }


    /// <summary>
    /// Start the script at its start node.
    /// </summary>
    public async Task<NodeResult> Start()
    {
        IsFinished = false;
        _visitCounts.Clear();
        _currentSpeaker = "";
        _currentAnimation = "";
        return await _enterNode(_script.StartNodeId);
    }


    /// <summary>
    /// Advance to the next flow step. Use when there are no choices
    /// (user acknowledged text, or following a goto).
    /// </summary>
    public async Task<NodeResult> Advance()
    {
        if (IsFinished || _currentNode == null)
        {
            return null;
        }

        _flowIndex++;
        return await _advanceFlow();
    }


    /// <summary>
    /// Choose one of the current flow step's choices and advance.
    /// </summary>
    public async Task<NodeResult> Choose(int choiceIndex)
    {
        if (IsFinished || _currentNode == null)
        {
            return null;
        }

        // Current step must be a Choices statement
        if (_flowIndex < _currentNode.Flow.Count)
        {
            var stmt = _currentNode.Flow[_flowIndex];
            if (stmt.StatementKind == NarrationStatement.Kind.Choices)
            {
                var visibleChoices = _getVisibleChoices(stmt.Choices);
                if (choiceIndex < 0 || choiceIndex >= visibleChoices.Count)
                {
                    Warning($"NarrationRunner: choice index {choiceIndex} out of range (0..{visibleChoices.Count - 1})");
                    return null;
                }

                var choice = visibleChoices[choiceIndex];
                string nextId = _resolveGoto(choice.Goto);
                if (string.IsNullOrEmpty(nextId))
                {
                    // No goto on choice — advance to next flow step
                    _flowIndex++;
                    return await _advanceFlow();
                }

                return await _enterNode(nextId);
            }
        }

        Warning("NarrationRunner: Choose() called but current step is not a Choices statement.");
        return null;
    }


    /// <summary>
    /// Restore runner state from saved data.
    /// </summary>
    public async Task<NodeResult> RestoreAt(string nodeId, Dictionary<string, int> visitCounts)
    {
        _visitCounts = visitCounts ?? new();
        IsFinished = false;
        return await _enterNode(nodeId);
    }


    /// <summary>
    /// Get save state for serialization.
    /// </summary>
    public (string NodeId, Dictionary<string, int> VisitCounts) GetSaveState()
    {
        return (_currentNodeId, new Dictionary<string, int>(_visitCounts));
    }


    private async Task<NodeResult> _enterNode(string nodeId)
    {
        if (!_script.Nodes.TryGetValue(nodeId, out var node))
        {
            Warning($"NarrationRunner: node '{nodeId}' not found in script '{_script.Name}'");
            IsFinished = true;
            return null;
        }

        // Check condition — if false, follow goto to skip
        if (!string.IsNullOrEmpty(node.Condition) && !_conditionEvaluator.Evaluate(node.Condition))
        {
            string skipTarget = _resolveGoto(node.Goto);
            if (string.IsNullOrEmpty(skipTarget))
            {
                IsFinished = true;
                return null;
            }

            return await _enterNode(skipTarget);
        }

        _currentNodeId = nodeId;
        _currentNode = node;
        _flowIndex = 0;

        // Track visits
        _visitCounts.TryGetValue(nodeId, out int count);
        _visitCounts[nodeId] = count + 1;

        return await _advanceFlow();
    }


    /// <summary>
    /// Walk through flow statements from _flowIndex, auto-advancing
    /// Speaker and Events steps, stopping on Text/Choices (user wait)
    /// or Goto (transition).
    /// </summary>
    private async Task<NodeResult> _advanceFlow()
    {
        while (_flowIndex < _currentNode.Flow.Count)
        {
            var stmt = _currentNode.Flow[_flowIndex];

            // Check statement condition
            if (!string.IsNullOrEmpty(stmt.Condition) && !_conditionEvaluator.Evaluate(stmt.Condition))
            {
                _flowIndex++;
                continue;
            }

            switch (stmt.StatementKind)
            {
                case NarrationStatement.Kind.Speaker:
                    _currentSpeaker = stmt.Speaker;
                    _currentAnimation = stmt.Animation;
                    _flowIndex++;
                    continue;

                case NarrationStatement.Kind.Events:
                    // Return a result with events only so the manager can process them,
                    // then the manager will auto-advance.
                    return new NodeResult
                    {
                        NodeId = _currentNodeId,
                        Text = "",
                        Speaker = _currentSpeaker,
                        Animation = _currentAnimation,
                        Choices = new List<string>(),
                        Events = stmt.Events,
                        HasChoices = false,
                        HasGoto = false,
                        IsAutoAdvance = true,
                    };

                case NarrationStatement.Kind.Text:
                {
                    string rawText = stmt.ResolveText(_rng);
                    string interpolatedText = await _interpolator.Interpolate(rawText);
                    return new NodeResult
                    {
                        NodeId = _currentNodeId,
                        Text = interpolatedText,
                        Speaker = _currentSpeaker,
                        Animation = _currentAnimation,
                        Choices = new List<string>(),
                        Events = new List<NarrationEventDescriptor>(),
                        HasChoices = false,
                        HasGoto = false,
                    };
                }

                case NarrationStatement.Kind.Choices:
                {
                    var visibleChoices = _getVisibleChoices(stmt.Choices);
                    var interpolatedChoices = new List<string>();
                    foreach (var choice in visibleChoices)
                    {
                        interpolatedChoices.Add(await _interpolator.Interpolate(choice.Text));
                    }

                    return new NodeResult
                    {
                        NodeId = _currentNodeId,
                        Text = "",
                        Speaker = _currentSpeaker,
                        Animation = _currentAnimation,
                        Choices = interpolatedChoices,
                        Events = new List<NarrationEventDescriptor>(),
                        HasChoices = visibleChoices.Count > 0,
                        HasGoto = false,
                    };
                }

                case NarrationStatement.Kind.Goto:
                {
                    string nextId = _resolveGoto(stmt.Goto);
                    if (string.IsNullOrEmpty(nextId))
                    {
                        IsFinished = true;
                        return null;
                    }

                    return await _enterNode(nextId);
                }
            }

            _flowIndex++;
        }

        // End of flow with no goto
        IsFinished = true;
        return null;
    }


    private List<NarrationChoice> _getVisibleChoices(List<NarrationChoice> choices)
    {
        var visible = new List<NarrationChoice>();
        foreach (var choice in choices)
        {
            if (string.IsNullOrEmpty(choice.Condition) || _conditionEvaluator.Evaluate(choice.Condition))
            {
                visible.Add(choice);
            }
        }

        return visible;
    }


    private string _resolveGoto(NarrationGoto g)
    {
        switch (g.GotoKind)
        {
            case NarrationGoto.Kind.None:
                return null;

            case NarrationGoto.Kind.Simple:
                return g.Target;

            case NarrationGoto.Kind.Conditional:
                foreach (var (condition, target) in g.Conditionals)
                {
                    if (_conditionEvaluator.Evaluate(condition))
                    {
                        return target;
                    }
                }

                return !string.IsNullOrEmpty(g.ElseTarget) ? g.ElseTarget : null;

            case NarrationGoto.Kind.Random:
                return _resolveRandomGoto(g);

            case NarrationGoto.Kind.Sequential:
                return _resolveSequentialGoto(g);

            default:
                return null;
        }
    }


    private string _resolveRandomGoto(NarrationGoto g)
    {
        if (g.RandomEntries.Count == 0) return null;

        float totalWeight = 0;
        foreach (var (weight, _) in g.RandomEntries)
        {
            totalWeight += weight;
        }

        float roll = (float)(_rng.NextDouble() * totalWeight);
        float cumulative = 0;
        foreach (var (weight, target) in g.RandomEntries)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return target;
            }
        }

        // Fallback to last entry
        return g.RandomEntries[^1].Target;
    }


    private string _resolveSequentialGoto(NarrationGoto g)
    {
        if (g.Sequence.Count == 0) return null;

        // Use visit count of the current node to determine sequence index
        _visitCounts.TryGetValue(_currentNodeId, out int visits);
        int index = visits - 1; // visits already incremented in _enterNode

        if (index < g.Sequence.Count)
        {
            return g.Sequence[index];
        }

        // Overflow handling
        switch (g.Overflow)
        {
            case "cycle":
                return g.Sequence[index % g.Sequence.Count];
            case "clamp":
                return g.Sequence[^1];
            case "random":
                return g.Sequence[_rng.Next(g.Sequence.Count)];
            default:
                return g.Sequence[index % g.Sequence.Count];
        }
    }


    /// <summary>
    /// Result of a flow step within a narration node.
    /// </summary>
    public class NodeResult
    {
        public string NodeId { get; set; }
        public string Text { get; set; }
        public string Speaker { get; set; }
        public string Animation { get; set; }
        public List<string> Choices { get; set; }
        public List<NarrationEventDescriptor> Events { get; set; }
        public bool HasChoices { get; set; }
        public bool HasGoto { get; set; }

        /// <summary>
        /// When true, this step should be auto-advanced by the manager
        /// (no user interaction required).
        /// </summary>
        public bool IsAutoAdvance { get; set; }
    }
}
