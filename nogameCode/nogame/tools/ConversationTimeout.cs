using System;
using System.Threading;
using System.Threading.Tasks;
using engine;
using engine.narration;
using engine.news;
using nogame.modules.story;

namespace nogame.tools;

/// <summary>
/// Per-NPC watcher that ends a conversation when the player stops engaging:
///   - after a "terminal" node (no choices presented), if the player doesn't
///     advance within TimeoutSeconds — the NPC moves on rather than freezing;
///   - when the owner explicitly calls CancelNow() (e.g. player walked away).
///
/// Lives only for the duration of one conversation. Subscribes to the
/// narration event stream, filters by ScriptName so other narrations don't
/// trip it, and invokes <c>onEnded</c> exactly once when the conversation
/// terminates (timeout, ScriptEnded, or explicit cancel). Always Dispose()
/// when discarding to release the subscriptions.
/// </summary>
internal sealed class ConversationTimeout : IDisposable
{
    public float TimeoutSeconds { get; set; } = 3.0f;

    /** How long to wait for the conversation to start before giving up on it (KAR-493). */
    public const int StartTimeoutMs = 2000;

    private readonly Engine _engine;
    private readonly string _scriptName;
    private Action _onEnded;
    private CancellationTokenSource _cts;
    private bool _disposed;

    /*
     * KAR-493: a ScriptEnded counts only once this conversation has started. Talking to an
     * NPC again ends the running conversation first, and that end - same script name -
     * arrives after the new watcher exists; it used to end the new conversation at once.
     */
    private bool _started;
    private CancellationTokenSource _startCts;

    public ConversationTimeout(Engine engine, string scriptName, Action onEnded)
    {
        _engine = engine;
        _scriptName = scriptName;
        _onEnded = onEnded;

        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(NodeReachedEvent.EVENT_TYPE, _onNodeReached);
        sm.Subscribe(ScriptEndedEvent.EVENT_TYPE, _onScriptEnded);
        sm.Subscribe(ScriptStartedEvent.EVENT_TYPE, _onScriptStarted);
        _scheduleStartTimeout();
    }


    private void _onScriptStarted(Event ev)
    {
        if (_disposed) return;
        if (ev is not ScriptStartedEvent sev || sev.ScriptName != _scriptName) return;
        _started = true;
        _cancelStartTimeout();
    }


    /*
     * If the conversation never starts (the narration refused it), end the watch, so its
     * owner resumes instead of waiting forever.
     */
    private void _scheduleStartTimeout()
    {
        var cts = new CancellationTokenSource();
        _startCts = cts;
        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(StartTimeoutMs, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            _engine.QueueMainThreadAction(() =>
            {
                if (token.IsCancellationRequested || _disposed || _started) return;
                _fireEnded();
            });
        });
    }


    private void _cancelStartTimeout()
    {
        var cts = _startCts;
        _startCts = null;
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }


    /// <summary>
    /// End the conversation now from outside (e.g. player walked out of range).
    /// CancelConversation pushes a ScriptEndedEvent which then routes through
    /// _onScriptEnded → onEnded.
    /// </summary>
    public void CancelNow()
    {
        if (_disposed) return;
        I.Get<Narration>().CancelConversation();
    }


    private void _onNodeReached(Event ev)
    {
        if (_disposed) return;
        if (ev is not NodeReachedEvent nev) return;
        if (nev.ScriptName != _scriptName) return;

        bool hasChoices = (nev.InterpolatedChoices?.Count ?? 0) > 0;
        _cancelTimer();
        if (!hasChoices) _scheduleTimer();
    }


    private void _onScriptEnded(Event ev)
    {
        if (_disposed) return;
        if (ev is ScriptEndedEvent sev && sev.ScriptName != _scriptName) return;
        if (!_started) return;   // the end of the conversation this one replaced
        _fireEnded();
    }


    private void _scheduleTimer()
    {
        _cancelTimer();
        if (TimeoutSeconds <= 0f) return;

        var cts = new CancellationTokenSource();
        _cts = cts;
        var token = cts.Token;
        int delayMs = (int)(TimeoutSeconds * 1000f);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            _engine.QueueMainThreadAction(() =>
            {
                if (token.IsCancellationRequested) return;
                if (_disposed) return;
                I.Get<Narration>().CancelConversation();
            });
        });
    }


    private void _cancelTimer()
    {
        var cts = _cts;
        _cts = null;
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }


    private void _fireEnded()
    {
        var cb = _onEnded;
        _onEnded = null;
        Dispose();
        cb?.Invoke();
    }


    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancelTimer();
        _cancelStartTimeout();

        var sm = I.Get<SubscriptionManager>();
        sm.Unsubscribe(NodeReachedEvent.EVENT_TYPE, _onNodeReached);
        sm.Unsubscribe(ScriptEndedEvent.EVENT_TYPE, _onScriptEnded);
        sm.Unsubscribe(ScriptStartedEvent.EVENT_TYPE, _onScriptStarted);
    }
}
