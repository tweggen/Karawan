namespace ExpectEngine;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Interprets JSON test scripts and executes them step-by-step against a TestSession.
/// Scripts are loaded from a Stream (works with both filesystem and Android assets).
/// </summary>
public sealed class ScriptRunner
{
    public string ScriptName { get; private set; }
    public int GlobalTimeoutSeconds { get; private set; } = 120;

    private readonly List<JsonObject> _steps = new();

    public static ScriptRunner LoadFromStream(Stream stream)
    {
        var runner = new ScriptRunner();
        var doc = JsonNode.Parse(stream) as JsonObject;
        if (doc == null)
        {
            throw new InvalidOperationException("Test script root must be a JSON object.");
        }

        runner.ScriptName = doc["name"]?.GetValue<string>() ?? "unnamed";
        if (doc["globalTimeout"] != null)
            runner.GlobalTimeoutSeconds = doc["globalTimeout"].GetValue<int>();

        var steps = doc["steps"]?.AsArray();
        if (steps != null)
        {
            foreach (var step in steps)
            {
                if (step is JsonObject stepObj)
                {
                    runner._steps.Add(stepObj);
                }
            }
        }

        return runner;
    }

    public async Task<TestResult> RunAsync(TestSession session)
    {
        using var globalCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(GlobalTimeoutSeconds));

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
                else
                    session.Log($"  Unknown step type at index {i}");
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
        int timeoutSec = step["timeout"]?.GetValue<int>() ?? 30;
        string comment = step["comment"]?.GetValue<string>() ?? $"step {idx}";

        Func<TestEvent, bool> predicate = evt =>
        {
            if (type != null && evt.Type != type) return false;
            if (code != null && evt.Code != code) return false;
            return true;
        };

        await session.FishForEvent(predicate, TimeSpan.FromSeconds(timeoutSec), comment);
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
        await Task.Delay(ms);
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
