namespace ExpectEngine;

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

/// <summary>
/// Interprets JSON test scripts and executes them step-by-step against a TestSession.
/// Scripts are loaded from a Stream (works with both filesystem and Android assets).
/// </summary>
public sealed class ScriptRunner
{
    public string ScriptName { get; private set; }
    public int GlobalTimeoutSeconds { get; private set; } = 120;

    /*
     * Multiplier for all wall-clock timeouts in the script. Lets an
     * orchestrator stretch timeouts when many tests share a saturated
     * machine (JOYCE_TEST_TIMEOUT_SCALE, e.g. "2.0").
     */
    private static readonly float _timeoutScale = _readScale("JOYCE_TEST_TIMEOUT_SCALE");

    /*
     * Multiplier for "sleep" steps. Scripts written against real-time hosts
     * sleep to let background work progress; when the event producer runs at
     * CPU speed (headless DES runs) these sleeps can be shortened
     * (JOYCE_TEST_SLEEP_SCALE, e.g. "0.1").
     */
    private static readonly float _sleepScale = _readScale("JOYCE_TEST_SLEEP_SCALE");

    private static float _readScale(string envVar)
    {
        string s = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(s)
            && float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float scale)
            && scale > 0f)
        {
            return scale;
        }
        return 1f;
    }

    private readonly List<JsonObject> _steps = new();

    /*
     * The top-level keys a script may carry. Anything else is an error: a
     * misspelt or invented block (49 TALE scripts once declared their checks
     * in an "expected" block nobody read, and passed with no steps).
     */
    private static readonly HashSet<string> _knownKeys = new()
    {
        "name", "description", "globalTimeout", "steps",
        // Documentation only.
        "phase", "category", "priority", "preconditions", "expectedOutcome"
    };

    private static readonly string[] _stepKinds = { "expect", "inject", "sleep", "action" };

    private static readonly HashSet<string> _knownExpectKeys = new() { "type", "code", "codeMatches" };

    private static readonly HashSet<string> _knownStepKeys = new()
    {
        "expect", "inject", "sleep", "action", "comment", "timeout", "count", "result"
    };


    /// <summary>
    /// Parse and validate a script. Throws InvalidOperationException when the
    /// script has an unknown top-level key, no steps, a step that is none of
    /// expect/inject/sleep/action, or no expect step at all: a script that
    /// checks nothing must not pass.
    /// </summary>
    public static ScriptRunner LoadFromStream(Stream stream)
    {
        var runner = new ScriptRunner();
        var doc = JsonNode.Parse(stream) as JsonObject;
        if (doc == null)
        {
            throw new InvalidOperationException("Test script root must be a JSON object.");
        }

        foreach (var (key, _) in doc)
        {
            if (!_knownKeys.Contains(key))
                throw new InvalidOperationException(
                    $"Test script has unknown top-level key \"{key}\"; known: {string.Join(", ", _knownKeys)}.");
        }

        runner.ScriptName = doc["name"]?.GetValue<string>() ?? "unnamed";
        if (doc["globalTimeout"] != null)
            runner.GlobalTimeoutSeconds = doc["globalTimeout"].GetValue<int>();

        var steps = doc["steps"]?.AsArray();
        if (steps == null || steps.Count == 0)
            throw new InvalidOperationException($"Test script \"{runner.ScriptName}\" has no steps.");

        bool hasExpect = false;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] is not JsonObject stepObj)
                throw new InvalidOperationException($"Step {i} is not a JSON object.");

            foreach (var (key, _) in stepObj)
            {
                if (!_knownStepKeys.Contains(key))
                    throw new InvalidOperationException($"Step {i}: unknown key \"{key}\".");
            }

            var kinds = _stepKinds.Where(stepObj.ContainsKey).ToList();
            if (kinds.Count != 1)
                throw new InvalidOperationException(
                    $"Step {i} must have exactly one of {string.Join("/", _stepKinds)}, has {kinds.Count}.");

            if (kinds[0] == "expect")
            {
                hasExpect = true;
                if (stepObj["expect"] is not JsonObject expectObj)
                    throw new InvalidOperationException($"Step {i}: expect must be an object.");
                foreach (var (key, _) in expectObj)
                {
                    if (!_knownExpectKeys.Contains(key))
                        throw new InvalidOperationException($"Step {i}: unknown expect key \"{key}\".");
                }
                if (stepObj["count"] != null && stepObj["count"].GetValue<int>() < 1)
                    throw new InvalidOperationException($"Step {i}: count must be at least 1.");
            }

            runner._steps.Add(stepObj);
        }

        if (!hasExpect)
            throw new InvalidOperationException($"Test script \"{runner.ScriptName}\" has no expect step.");

        return runner;
    }

    public async Task<TestResult> RunAsync(TestSession session)
    {
        using var globalCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(GlobalTimeoutSeconds * _timeoutScale));

        for (int i = 0; i < _steps.Count; i++)
        {
            if (globalCts.Token.IsCancellationRequested)
            {
                return session.CreateResult(TestOutcome.Timeout,
                    $"Global timeout at step {i}", i);
            }

            var step = _steps[i];
            string comment = step["comment"]?.GetValue<string>() ?? $"step {i}";
            session.Log($"Step {i}: {comment}");

            try
            {
                if (step.ContainsKey("expect"))
                    await _executeExpect(session, step, i);
                else if (step.ContainsKey("inject"))
                    await _executeInject(session, step);
                else if (step.ContainsKey("sleep"))
                    await _executeSleep(step);
                else if (step.ContainsKey("action"))
                    return _executeAction(session, step, i);
            }
            catch (TestTimeoutException ex)
            {
                return session.CreateResult(TestOutcome.Timeout, ex.Message, i);
            }
            catch (TestExpectationException ex)
            {
                return session.CreateResult(TestOutcome.Fail, ex.Message, i);
            }
            catch (Exception ex)
            {
                return session.CreateResult(TestOutcome.Error, ex.ToString(), i);
            }
        }

        return session.CreateResult(TestOutcome.Pass, "All steps completed", _steps.Count);
    }

    private async Task _executeExpect(TestSession session, JsonObject step, int idx)
    {
        var expectObj = step["expect"].AsObject();
        string type = expectObj["type"]?.GetValue<string>();
        string code = expectObj["code"]?.GetValue<string>();
        string codeMatches = expectObj["codeMatches"]?.GetValue<string>();
        var codeRegex = codeMatches != null ? new Regex(codeMatches, RegexOptions.CultureInvariant) : null;
        int count = step["count"]?.GetValue<int>() ?? 1;
        int timeoutSec = step["timeout"]?.GetValue<int>() ?? 30;
        string comment = step["comment"]?.GetValue<string>() ?? $"step {idx}";

        Func<TestEvent, bool> predicate = evt =>
        {
            if (type != null && evt.Type != type) return false;
            if (code != null && evt.Code != code) return false;
            if (codeRegex != null && (evt.Code == null || !codeRegex.IsMatch(evt.Code))) return false;
            return true;
        };

        /*
         * "count": n waits for n matching events, all within the one timeout.
         */
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSec * _timeoutScale);
        for (int n = 1; n <= count; n++)
        {
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero) left = TimeSpan.FromMilliseconds(1);
            await session.FishForEvent(predicate, left,
                count > 1 ? $"{comment} ({n}/{count})" : comment);
        }
    }

    private async Task _executeInject(TestSession session, JsonObject step)
    {
        var injectObj = step["inject"].AsObject();
        string type = injectObj["type"]?.GetValue<string>() ?? "";
        string code = injectObj["code"]?.GetValue<string>();
        await session.InjectEvent(new TestEvent(type, code));
    }

    private async Task _executeSleep(JsonObject step)
    {
        int ms = step["sleep"].GetValue<int>();
        await Task.Delay((int)(ms * _sleepScale));
    }

    private TestResult _executeAction(TestSession session, JsonObject step, int idx)
    {
        string action = step["action"].GetValue<string>();
        if (action == "quit")
        {
            string resultStr = step["result"]?.GetValue<string>() ?? "pass";
            var outcome = resultStr == "pass" ? TestOutcome.Pass : TestOutcome.Fail;
            return session.CreateResult(outcome, $"Quit action: {resultStr}", idx);
        }

        return session.CreateResult(TestOutcome.Error, $"Unknown action: {action}", idx);
    }
}
