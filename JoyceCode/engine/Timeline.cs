using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static engine.Logger;

namespace engine;

public class Timeline : IDisposable
{
    private object _lo = new();
    private SortedDictionary<string, DateTime> _mapTimeline = new();
    private SortedDictionary<DateTime, Action> _mapActions = new();
    private System.Timers.Timer _timer = new();
    

    private void _checkExpiredActions(DateTime now)
    {
        List<Action> listActions = new();
        List<DateTime> listDelete = new();

        DateTime newFirst = DateTime.MaxValue;
        lock (_lo)
        {
            /*
             * Naturally, we need to iterate from the beginning
             * to the first thing that is larger.
             */
            foreach (var kvp in _mapActions)
            {
                if (kvp.Key > now)
                {
                    newFirst = kvp.Key;
                    break;
                }

                listActions.Add(kvp.Value);
                listDelete.Add(kvp.Key);
            }
            

            foreach (var date in listDelete)
            {
                _mapActions.Remove(date);
            }
            
            if (newFirst != DateTime.MaxValue)
            {
                _checkNewTimerNoLock(now, newFirst);
            }
        }

        foreach (var action in listActions)
        {
            /*
             * Async trigger off task.
             */
            Task.Run(action);
        }
    }


    /**
     * Check, if a timer has been set up to trigger at the given point.
     */
    private void _checkNewTimerNoLock(DateTime now, DateTime tsTarget)
    {
        bool needReprogram = false;
        TimeSpan offset = tsTarget - now;

        if (_timer.Enabled == false)
        {
            needReprogram = true;
        }
        else
        {
            double triggersInMs = _timer.Interval;

            double toleranceInMs = 1f;

            if (offset.TotalMilliseconds > triggersInMs)
            {
                /*
                 * Don't need to reprogram, will trigger earlier
                 * anyway.
                 */
                needReprogram = false;
            }
            else
            {
                needReprogram = true;
            }
        }

        if (needReprogram)
        {
            _timer.Enabled = false;
            _timer.Interval = offset.TotalMilliseconds;
            _timer.Start();
        }
    }


    private void _onTimerTriggered(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _checkExpiredActions(DateTime.Now);
    }
    
    
    public void SetMarker(string key, DateTime dateTime)
    {
        lock (_lo)
        {
            _mapTimeline[key] = dateTime;
        }
    }

    
    public void RunAt(string key, TimeSpan offset, Action action)
    {
        DateTime tsTarget;
        DateTime now = DateTime.Now;
        lock (_lo)
        {
            if (!_mapTimeline.TryGetValue(key, out tsTarget))
            {
                ErrorThrow($"Cannot find key \"{key}\".", (m) => new ArgumentException(m));
                return;
            }

            tsTarget += offset;
            _mapActions[tsTarget] = action;
        }

        _checkExpiredActions(now);
    }


    public void Dispose()
    {
        _timer.Enabled = false;
        _timer.Dispose();
    }
    
    
    public Timeline()
    {
        _timer = new();
        _timer.AutoReset = false;
        _timer.Elapsed += _onTimerTriggered;
    }
}