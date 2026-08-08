using System;
using System.Collections.Generic;
using System.Numerics;
using engine.news;
using static engine.Logger;

namespace builtin.controllers;

public class FingerStateHandler
{
    private static readonly engine.Dc _dc = engine.Dc.Input;

    private object _lo = new();

    private SortedDictionary<uint, IFingerState> _mapFingerStates = new();


    private Func<Event, IFingerState> _fingerStateFactory;


    /**
     * Reject a touch position that is not a finite number, at the boundary where platform
     * input enters the engine.
     *
     * This is not defensive decoration. Finger deltas are accumulated by
     * RightStickFingerState into InputController.V2RightTouchMove, and FollowCameraController
     * accumulates those again into its own long-lived _v2MouseOffseting. Neither accumulator
     * has any path back from NaN or Infinity, so ONE bad event does not cause one bad frame -
     * it ends rendering for the rest of the process. Observed on Android as a view that spins
     * wildly, then goes black, with OpenAL logging "Listener orientation out of range" every
     * frame from then on.
     *
     * The known producer is fixed at source (Wuka.GameSurface.OnTouch divided by a
     * not-yet-laid-out View.Width/Height), but the cost of a bad value getting through here
     * is out of all proportion to the cost of checking, and every platform feeds this path.
     */
    private static bool _isUsable(Event ev)
    {
        Vector2 p = ev.PhysicalPosition;
        if (!Single.IsFinite(p.X) || !Single.IsFinite(p.Y))
        {
            Warning($"Ignoring finger {ev.Data2} event {ev.Type}: non-finite position {p}.");
            return false;
        }

        _validateRange(ev, p);
        return true;
    }


    /**
     * THE CONTRACT for a finger event, stated once, checked at the only door every platform's
     * touch has to come through.
     *
     *   PhysicalPosition   0..1 on both axes, fraction of the surface
     *   PhysicalSize       (1,1), because the position is already normalised
     *
     * That is what Wuka.GameSurface.OnTouch produces (it divides by View.Width/Height). Parts of
     * InputController were written against PIXEL coordinates and divide by the "view.size" global
     * instead, so the two conventions coexist in this codebase and a producer sending the wrong
     * one is not obviously wrong at any single line - it just makes everything downstream 3
     * orders of magnitude too big.
     *
     * Hence: violations are a WARNING, visible without switching anything on. That lesson was
     * learned the expensive way - the "Too fast" impulse diagnostics were behind a debug category
     * during an angular runaway and printed nothing, costing a whole test round.
     *
     * The per-event OUT-OF-RANGE warning is rate limited; the per-event in-range line stays behind
     * debug.category.input = true, because at ~100 events/second it would drown the log.
     */
    private const float PositionMax = 1f;
    private const float PositionSlack = 0.05f;
    private const int MaxRangeReports = 20;
    private static int _nRangeReports = 0;

    private static void _validateRange(Event ev, Vector2 p)
    {
        /*
         * Slack on purpose: a finger can be reported slightly outside the surface bounds on some
         * digitizers, and that is not the failure this is looking for. A producer sending pixels
         * is out by a factor of hundreds, not by 0.05.
         */
        bool positionInRange =
            p.X >= -PositionSlack && p.X <= PositionMax + PositionSlack
            && p.Y >= -PositionSlack && p.Y <= PositionMax + PositionSlack;

        bool sizeInRange = ev.PhysicalSize == Vector2.One;

        if (!positionInRange || !sizeInRange)
        {
            if (_nRangeReports < MaxRangeReports)
            {
                ++_nRangeReports;
                Warning(
                    $"Finger {ev.Data2} {ev.Type} OUT OF RANGE: pos={p} expected 0..1 on both axes; "
                    + $"size={ev.PhysicalSize} expected (1,1). "
                    + $"view.size={engine.GlobalSettings.Get("view.size")}. "
                    + $"A position that looks like pixels means the producer is not normalising - "
                    + $"everything downstream is then scaled by the surface size. "
                    + $"Report {_nRangeReports} of {MaxRangeReports}.");
            }

            return;
        }

        Trace(_dc, $"{ev.Type} finger={ev.Data2} dev={ev.Data1} pos={p} (expected 0..1) "
                   + $"size={ev.PhysicalSize} (expected (1,1)) "
                   + $"view.size={engine.GlobalSettings.Get("view.size")}");
    }


    public void OnFingerReleased(Event ev)
    {
        IFingerState? fingerState = null;
        
        lock (_lo)
        {
            if (_mapFingerStates.TryGetValue(ev.Data2, out fingerState))
            {
                Trace($"Removing finger {ev.Data2} from map.");
                /*
                 * We better have an old one.
                 */
                _mapFingerStates.Remove(ev.Data2);
            }
        }

        /*
         * Note the removal above is unconditional: a finger that has been lifted must never
         * stay in the map, whatever its coordinates say. Only the handler call is skipped,
         * because that is what feeds the position into the accumulators.
         */
        if (fingerState != null && _isUsable(ev))
        {
            fingerState.HandleReleased(ev);
            ev.IsHandled = true;
        }
    }


    public void OnFingerMotion(Event ev)
    {
        IFingerState? fingerState = null;

        if (!_isUsable(ev)) return;

        lock (_lo)
        {
            if (_mapFingerStates.TryGetValue(ev.Data2, out fingerState))
            {
            }
        }

        if (fingerState != null)
        {
            fingerState.HandleMotion(ev);
            ev.IsHandled = true;
        }
    }


    public void OnFingerPressed(Event ev, Func<Event, IFingerState> localFingerStateFactory = null)
    {
        if (!_isUsable(ev)) return;

        IFingerState? oldFingerState = null;
        IFingerState? iFingerState;
        
        lock (_lo)
        {
            if (_mapFingerStates.TryGetValue(ev.Data2, out oldFingerState))
            {
                Trace($"OnFingerPressed: finger {ev.Data2} already pressed. Terminating old state. {oldFingerState}.");
                /*
                 * This should not happen. Terminate the old one, start a new.
                 */
                _mapFingerStates.Remove(ev.Data2);
            }
        }

        if (null != oldFingerState)
        {
            var releaseEv = new Event(Event.INPUT_FINGER_RELEASED, ev.Code)
            {
                PhysicalPosition = ev.PhysicalPosition,
                PhysicalSize = ev.PhysicalSize,
                LogicalPosition = ev.LogicalPosition,
                Data1 = ev.Data1,
                Data2 = ev.Data2,
            };
            oldFingerState.HandleReleased(releaseEv);
        }

        if (null != localFingerStateFactory)
        {
            iFingerState = localFingerStateFactory(ev);
        }
        else
        {
            iFingerState = _fingerStateFactory(ev);
        }

        if (null != iFingerState)
        {
            var evKey = ev.Data2;
            
            lock (_lo)
            {
                Trace($"OnFingerPressed: adding finger {evKey} to map.");
                _mapFingerStates[evKey] = iFingerState;
            }
            
            iFingerState.HandlePressed(ev);
            
            
            /*
             * If the event was not handled, do not track it furthermore.
             */
            if (!ev.IsHandled)
            {
                Warning($"Removing finger {evKey} from map because it was not handled.");
                lock (_lo)
                {
                    _mapFingerStates.Remove(evKey);
                }
            }
        }
    }


    public FingerStateHandler(Func<Event, IFingerState> fingerStateFactory = null)
    {
        _fingerStateFactory = fingerStateFactory;
    }
}