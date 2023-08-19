using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using engine.news;
using static engine.Logger;

namespace engine;


public class InputEventPipeline
{
    private object _lo = new();
    private Engine _engine;


    void _onInputEvent(engine.news.Event ev)
    {
        /*
         * We need to propagate the event through all of the parts z order.
         */
        IEnumerable<IPart> listParts = _engine.GetParts();

        /*
         * Now run distribution of key events in a dedicated task.
         * TXWTODO: This may become out of order, until we have a proper global event queue.
         */
        if (ev.IsHandled)
        {
            Trace($"Event {ev} not dispatched at all, already handled from the beginning.");
            return;
        }


        // TXWTODO: That's the wrong part order, isn't it?
        foreach (var part in listParts)
        {
            try
            {
                part.PartOnInputEvent(ev);
            }
            catch (Exception e)
            {
                Error($"Exception handling key event by part {part}: {e}.");
            }

            if (ev.IsHandled)
            {
                Trace($"Key event {ev} was handled by part {part}.");
                break;
            }
        }
    }
    
    
    public void ModuleDeactivate()
    {
        Implementations.Get<SubscriptionManager>().Unsubscribe("input.", _onInputEvent);
    }

    
    public void ModuleActivate(engine.Engine engine0)
    {
        _engine = engine0;
        Implementations.Get<SubscriptionManager>().Subscribe("input.", _onInputEvent);
    }
}