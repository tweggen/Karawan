using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ExpectEngine;
using Xunit;

namespace JoyceCode.Tests.expect;

/// <summary>
/// KAR-433: a script that checks nothing must not pass, and expect steps can
/// count and match codes.
/// </summary>
public class ScriptRunnerTests
{
    private sealed class ListSource : ITestEventSource, ITestEventSink
    {
        private readonly List<TestEvent> _events;
        public ListSource(params TestEvent[] events) => _events = new List<TestEvent>(events);

        public IDisposable Subscribe(Action<TestEvent> onEvent)
        {
            foreach (var e in _events) onEvent(e);
            return new Unsub();
        }

        public Task InjectEventAsync(TestEvent testEvent) => Task.CompletedTask;

        private sealed class Unsub : IDisposable { public void Dispose() { } }
    }

    private static ScriptRunner _load(string json) =>
        ScriptRunner.LoadFromStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static async Task<TestOutcome> _run(string json, params TestEvent[] events)
    {
        var source = new ListSource(events);
        using var session = new TestSession(source, source);
        return (await _load(json).RunAsync(session)).Outcome;
    }


    [Theory]
    [InlineData("""{ "name": "x", "expected": [ { "code": "npc_created" } ] }""", "expected")]
    [InlineData("""{ "name": "x", "title": "t", "steps": [ { "expect": { "type": "a" } } ] }""", "title")]
    [InlineData("""{ "name": "x" }""", "no steps")]
    [InlineData("""{ "name": "x", "steps": [] }""", "no steps")]
    [InlineData("""{ "name": "x", "steps": [ { "sleep": 10 }, { "action": "quit", "result": "pass" } ] }""", "no expect step")]
    [InlineData("""{ "name": "x", "steps": [ { "step": "verify", "action": "prose" } ] }""", "unknown key")]
    [InlineData("""{ "name": "x", "steps": [ { "comment": "c" }, { "expect": { "type": "a" } } ] }""", "exactly one")]
    [InlineData("""{ "name": "x", "steps": [ { "sleep": 1, "expect": { "type": "a" } } ] }""", "exactly one")]
    [InlineData("""{ "name": "x", "steps": [ { "expect": { "type": "a", "count": 2 } } ] }""", "unknown expect key")]
    [InlineData("""{ "name": "x", "steps": [ { "expect": { "type": "a" }, "count": 0 } ] }""", "at least 1")]
    public void ScriptsThatCheckNothing_DoNotLoad(string json, string reason)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _load(json));
        Assert.Contains(reason, ex.Message);
    }


    [Fact]
    public async Task Count_WaitsForThatManyEvents()
    {
        const string script = """
        { "name": "x", "steps": [ { "expect": { "type": "a" }, "count": 3, "timeout": 1 } ] }
        """;
        Assert.Equal(TestOutcome.Pass, await _run(script,
            new TestEvent("a", "1"), new TestEvent("b"), new TestEvent("a", "2"), new TestEvent("a", "3")));
        Assert.Equal(TestOutcome.Timeout, await _run(script,
            new TestEvent("a", "1"), new TestEvent("a", "2")));
    }


    [Fact]
    public async Task CodeMatches_IsARegexOverTheCode()
    {
        const string script = """
        { "name": "x", "steps": [ { "expect": { "type": "interrupt_fired", "codeMatches": "^\\d+:Nest:" }, "timeout": 1 } ] }
        """;
        Assert.Equal(TestOutcome.Pass, await _run(script,
            new TestEvent("interrupt_fired", "4:Replace:x"), new TestEvent("interrupt_fired", "7:Nest:demand_protection")));
        Assert.Equal(TestOutcome.Timeout, await _run(script,
            new TestEvent("interrupt_fired", "4:Replace:x"), new TestEvent("interrupt_fired")));
    }


    [Fact]
    public async Task ExpectSteps_ConsumeEventsInOrder()
    {
        const string script = """
        { "name": "x", "steps": [
          { "expect": { "type": "day_summary", "code": "30" }, "timeout": 1 },
          { "expect": { "type": "escalation" }, "timeout": 1 } ] }
        """;
        Assert.Equal(TestOutcome.Pass, await _run(script,
            new TestEvent("day_summary", "30"), new TestEvent("escalation")));
        Assert.Equal(TestOutcome.Timeout, await _run(script,
            new TestEvent("escalation"), new TestEvent("day_summary", "30")));
    }
}
