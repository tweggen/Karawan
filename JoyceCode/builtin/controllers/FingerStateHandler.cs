using System;
using System.Collections.Generic;
using engine.news;

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
    
    
    public void OnFingerPressed(Event ev)
    {        
        IFingerState? oldFingerState = null;
        IFingerState? iFingerState;
        
        lock (_lo)
        {
            if (_mapFingerStates.TryGetValue(ev.Data2, out oldFingerState))
            {
                /*
                 * This should not happen. Terminate the old one, start a new.
                 */
                _mapFingerStates.Remove(ev.Data2);
            }
        }

        if (null != oldFingerState)
        {
            oldFingerState.HandleReleased(ev);
        }
        
        iFingerState = _fingerStateFactory(ev);

        if (null != iFingerState)
        {
            var evKey = ev.Data2;
            
            lock (_lo)
            {
                _mapFingerStates[evKey] = iFingerState;
            }
            
            iFingerState.HandlePressed(ev);
            
            
            /*
             * If the event was not handled, do not track it furthermore.
             */
            if (!ev.IsHandled)
            {
                lock (_lo)
                {
                    _mapFingerStates.Remove(evKey);
                }
            }
        }
    }


    public FingerStateHandler(Func<Event, IFingerState> fingerStateFactory)
    {
        _fingerStateFactory = fingerStateFactory;
    }
}