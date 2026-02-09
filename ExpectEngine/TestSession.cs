namespace ExpectEngine;

using System.Diagnostics;
using System.Threading.Channels;

/// <summary>
/// Core expect engine. Subscribes to an ITestEventSource and provides
/// async expect/fish/inject operations against the event stream.
/// Uses System.Threading.Channels for lock-free cross-thread communication.
/// </summary>
public sealed class TestSession : IDisposable
{
    private readonly ITestEventSource _source;
    private readonly ITestEventSink _sink;
    private readonly Channel<TestEvent> _channel;
    private readonly IDisposable _subscription;
    private readonly List<string> _log = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private bool _disposed;

    public TestSession(ITestEventSource source, ITestEventSink sink)
    {
        _source = source;
        _sink = sink;
        _channel = Channel.CreateUnbounded<TestEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _subscription = _source.Subscribe(evt =>
        {
            _channel.Writer.TryWrite(evt);
        });
    }

    /// <summary>
    /// Wait for the very next event to match the predicate.
    /// If the next event does not match, the test fails immediately.
    /// </summary>
    public async Task<TestEvent> ExpectEvent(
        Func<TestEvent, bool> predicate, TimeSpan timeout, string description = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var evt = await _channel.Reader.ReadAsync(cts.Token);
            if (predicate(evt))
            {
                Log($"EXPECT matched: {evt} ({description})");
                return evt;
            }

            throw new TestExpectationException(
                $"ExpectEvent failed: got {evt}, expected: {description ?? "predicate"}");
        }
        catch (OperationCanceledException)
        {
            throw new TestTimeoutException(
                $"ExpectEvent timed out after {timeout}: {description ?? "predicate"}");
        }
    }

    /// <summary>
    /// Skip non-matching events, return first match. This is the typical "fish"
    /// pattern from Akka TestKit — keeps consuming events until one matches.
    /// </summary>
    public async Task<TestEvent> FishForEvent(
        Func<TestEvent, bool> predicate, TimeSpan timeout, string description = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var evt = await _channel.Reader.ReadAsync(cts.Token);
                if (predicate(evt))
                {
                    Log($"FISH matched: {evt} ({description})");
                    return evt;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // fall through
        }

        throw new TestTimeoutException(
            $"FishForEvent timed out after {timeout}: {description ?? "predicate"}");
    }

    /// <summary>
    /// Poll a condition function until it returns true or timeout.
    /// </summary>
    public async Task AwaitCondition(
        Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.Token.IsCancellationRequested)
        {
            if (condition())
            {
                Log("AwaitCondition satisfied.");
                return;
            }

            try
            {
                await Task.Delay(interval, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TestTimeoutException($"AwaitCondition timed out after {timeout}.");
    }

    /// <summary>
    /// Assert no matching event arrives within the given time window.
    /// </summary>
    public async Task ExpectNoEvent(
        Func<TestEvent, bool> predicate, TimeSpan window, string description = null)
    {
        using var cts = new CancellationTokenSource(window);
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var evt = await _channel.Reader.ReadAsync(cts.Token);
                if (predicate(evt))
                {
                    throw new TestExpectationException(
                        $"ExpectNoEvent failed: unexpected event {evt} ({description})");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log($"ExpectNoEvent passed: no match within {window} ({description})");
        }
    }

    /// <summary>
    /// Inject an event into the host application.
    /// </summary>
    public Task InjectEvent(TestEvent evt)
    {
        Log($"INJECT: {evt}");
        return _sink.InjectEventAsync(evt);
    }

    public void Log(string message)
    {
        lock (_log)
        {
            _log.Add($"[{_stopwatch.Elapsed:mm\\:ss\\.fff}] {message}");
        }
    }

    public TestResult CreateResult(TestOutcome outcome, string message, int stepIndex)
    {
        List<string> logCopy;
        lock (_log)
        {
            logCopy = new List<string>(_log);
        }

        return new TestResult(outcome, message, stepIndex, _stopwatch.Elapsed, logCopy);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _subscription?.Dispose();
            _channel.Writer.TryComplete();
        }
    }
}
