using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExpectEngine;
using static engine.Logger;

namespace engine.testing;

/// <summary>
/// Engine module that drives test execution.
/// Activates when GlobalSettings "test.script" is non-empty
/// or when the environment variable JOYCE_TEST_SCRIPT is set.
/// Loads a JSON test script, runs it against the engine's event stream,
/// and reports results via exit code.
/// </summary>
public sealed class TestDriverModule : AModule
{
    private JoyceTestEventSource _eventSource;
    private Task _testTask;

    public override IEnumerable<IModuleDependency> ModuleDepends()
        => new List<IModuleDependency>();

    protected override void OnModuleActivate()
    {
        string scriptPath = _resolveScriptPath();
        if (string.IsNullOrEmpty(scriptPath))
        {
            return;
        }

        Trace($"TestDriverModule: loading test script '{scriptPath}'");

        _eventSource = new JoyceTestEventSource();
        var eventSink = new JoyceTestEventSink();

        _testTask = Task.Run(async () =>
        {
            try
            {
                // Small delay to let the engine initialize
                await Task.Delay(500);

                using var stream = engine.Assets.Open(scriptPath);
                if (stream == null)
                {
                    Error($"TestDriverModule: could not open test script '{scriptPath}'");
                    _reportResult(new TestResult(
                        TestOutcome.Error, $"Script not found: {scriptPath}", -1,
                        TimeSpan.Zero, new List<string>()));
                    return;
                }

                var script = ScriptRunner.LoadFromStream(stream);

                using var session = new TestSession(_eventSource, eventSink);
                session.Log($"Running test: {script.ScriptName}");

                var result = await script.RunAsync(session);
                _reportResult(result);
            }
            catch (Exception ex)
            {
                Error($"TestDriverModule: unhandled exception: {ex}");
                _reportResult(new TestResult(
                    TestOutcome.Error, ex.ToString(), -1,
                    TimeSpan.Zero, new List<string>()));
            }
        });
    }

    protected override void OnModuleDeactivate()
    {
        _eventSource?.Dispose();
    }

    private string _resolveScriptPath()
    {
        string path = GlobalSettings.Get("test.script");
        if (!string.IsNullOrEmpty(path)) return path;

        path = Environment.GetEnvironmentVariable("JOYCE_TEST_SCRIPT");
        return path ?? "";
    }

    private void _reportResult(TestResult result)
    {
        foreach (var line in result.Log)
        {
            Trace($"TEST: {line}");
        }

        string outcomeStr = result.Outcome.ToString().ToUpperInvariant();
        Trace($"TEST RESULT: {outcomeStr} - {result.Message}");
        Trace($"TEST: Elapsed {result.Elapsed}, step {result.StepIndex}");

        Console.WriteLine($"[TEST] {outcomeStr}: {result.Message}");
        Console.WriteLine($"[TEST] Elapsed: {result.Elapsed}");

        _engine.QueueMainThreadAction(() =>
        {
            _engine.Exit();
        });

        Task.Delay(2000).ContinueWith(_ =>
        {
            Environment.Exit(result.ExitCode);
        });
    }
}
