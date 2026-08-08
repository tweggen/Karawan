using System;
using System.Collections.Generic;
using System.Numerics;
using engine.news;
using static engine.Logger;

namespace builtin.controllers;

public class FingerStateHandler
{
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
        if (Single.IsFinite(p.X) && Single.IsFinite(p.Y))
        {
            return true;
        }

        Warning($"Ignoring finger {ev.Data2} event {ev.Type}: non-finite position {p}.");
        return false;
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