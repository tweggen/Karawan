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

    /// <summary>
    /// Signaled once the test session is subscribed to the engine's event
    /// stream. Hosts that generate test events (e.g. the DES simulation
    /// thread in TestRunner) can wait on this instead of sleeping.
    /// </summary>
    public static readonly System.Threading.ManualResetEventSlim SessionReady = new(false);

    /// <summary>
    /// Signaled once a test result has been reported; ExitCode carries it.
    /// Hosts can wait on this instead of a fixed timeout sleep.
    /// </summary>
    public static readonly System.Threading.ManualResetEventSlim Completed = new(false);

    public static int? ExitCode { get; private set; }

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

        /*
         * The session's constructor subscribes to the event source. Doing this
         * synchronously before signalling SessionReady guarantees that no event
         * generated after the signal can be missed.
         */
        var session = new TestSession(_eventSource, eventSink);
        SessionReady.Set();

        _testTask = Task.Run(async () =>
        {
            try
            {
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
            finally
            {
                session.Dispose();
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

        ExitCode = result.ExitCode;
        Completed.Set();

        /*
         * Fallback for hosts that do not wait on Completed: exit after the
         * engine had a moment to wind down.
         */
        Task.Delay(2000).ContinueWith(_ =>
        {
            Environment.Exit(result.ExitCode);
        });
    }
}
