using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using engine.news;
using static engine.Logger;

namespace engine.news;


public class InputEventPipeline : engine.AModule
{
    private PriorityMap<IInputPart> _dictParts = new();
 
   
    void _onInputEvent(engine.news.Event ev)
    {
        /*
         * Now run distribution of key events in a dedicated task.
         * TXWTODO: This may become out of order, until we have a proper global event queue.
         */
        if (ev.IsHandled)
        {
            Trace($"Event {ev} not dispatched at all, already handled from the beginning.");
            return;
        }
        
        /*
         * We need to propagate the event through all of the parts z order.
         */
        IEnumerable<IInputPart> listParts = _dictParts.GetEnumerable();

        
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
        _dictParts.Remove(part);
    }


    public void AddInputPart(float zOrder, IInputPart part0)
    {
        _dictParts.Add(zOrder, part0);
    }


    public float GetFrontZ()
    {
        return _dictParts.FrontPrio();
    }
    
    
    public override void ModuleDeactivate()
    {
        I.Get<SubscriptionManager>().Unsubscribe("input.", _onInputEvent);
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }

    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
        I.Get<SubscriptionManager>().Subscribe("input.", _onInputEvent);
    }
}