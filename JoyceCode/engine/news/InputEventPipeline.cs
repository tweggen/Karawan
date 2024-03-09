using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using engine.news;
using static engine.Logger;

namespace engine.news;


public class InputEventPipeline : engine.AModule
{
    private SortedDictionary<float, IInputPart> _dictParts = new();
 
    
    void _onInputEvent(engine.news.Event ev)
    {
        /*
         * We need to propagate the event through all of the parts z order.
         */
        IEnumerable<IInputPart> listParts;
        lock (_lo)
        {
            listParts = new List<IInputPart>(_dictParts.Values);
        }

        /*
         * Now run distribution of key events in a dedicated task.
         * TXWTODO: This may become out of order, until we have a proper global event queue.
         */
        if (ev.IsHandled)
        {
            Trace($"Event {ev} not dispatched at all, already handled from the beginning.");
            return;
        }


        
        foreach (var part in listParts)
        {
            try
            {
                part.InputPartOnInputEvent(ev);
            }
            catch (Exception e)
            {
                Error($"Exception handling key event by part {part}: {e}.");
            }

            if (ev.IsHandled)
            {
                //Trace($"Key event {ev} was handled by part {part}.");
                break;
            }
        }
    }


    public void RemoveInputPart(IInputPart part)
    {
        lock (_lo)
        {
            foreach (KeyValuePair<float, IInputPart> kvp in _dictParts)
            {
                if (kvp.Value == part)
                {
                    _dictParts.Remove(kvp.Key);
                    return;
                }
            }
        }
        ErrorThrow<ArgumentException>($"Warning: input part not found.");
    }


    public void AddInputPart(float zOrder, IInputPart part0)
    {
        lock (_lo)
        {
            if (_dictParts.ContainsKey(-zOrder))
            {
                ErrorThrow<ArgumentException>($"Handler for zOrder {zOrder} already exists: {part0}");
                return;
            }
            /*
             * We use the negative sign because we want to iterate in reverse order.
             */
            _dictParts.Add(-zOrder, part0);
        }
    }


    public float GetFrontZ()
    {
        lock (_lo)
        {
            foreach (KeyValuePair<float, IInputPart> kvp in _dictParts)
            {
                return -kvp.Key;
            }

            return Single.MinValue;
        }
    }
    
    
    public override void ModuleDeactivate()
    {
        I.Get<SubscriptionManager>().Unsubscribe("input.", _onInputEvent);
        base.ModuleDeactivate();
    }

    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        I.Get<SubscriptionManager>().Subscribe("input.", _onInputEvent);
    }
}