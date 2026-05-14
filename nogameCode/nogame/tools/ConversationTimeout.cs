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

    private readonly Engine _engine;
    private readonly string _scriptName;
    private Action _onEnded;
    private CancellationTokenSource _cts;
    private bool _disposed;

    public ConversationTimeout(Engine engine, string scriptName, Action onEnded)
    {
        _engine = engine;
        _scriptName = scriptName;
        _onEnded = onEnded;

        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(NodeReachedEvent.EVENT_TYPE, _onNodeReached);
        sm.Subscribe(ScriptEndedEvent.EVENT_TYPE, _onScriptEnded);
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

        var sm = I.Get<SubscriptionManager>();
        sm.Unsubscribe(NodeReachedEvent.EVENT_TYPE, _onNodeReached);
        sm.Unsubscribe(ScriptEndedEvent.EVENT_TYPE, _onScriptEnded);
    }
}
