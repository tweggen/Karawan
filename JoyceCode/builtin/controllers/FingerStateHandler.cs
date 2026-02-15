using System;
using System.Collections.Generic;
using engine.news;
using static engine.Logger;

namespace builtin.controllers;

public class FingerStateHandler
{
    private object _lo = new();
    
    private SortedDictionary<uint, IFingerState> _mapFingerStates = new();


    private Func<Event, IFingerState> _fingerStateFactory;


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

        if (fingerState != null)
        {
            fingerState.HandleReleased(ev);
            ev.IsHandled = true;
        }
    }
    
    
    public void OnFingerMotion(Event ev)
    {
        IFingerState? fingerState = null;
        
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