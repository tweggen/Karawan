using System;
using engine.gongzuo;
using engine.news;
using static engine.Logger;

namespace engine.gongzuo;

/**
 * Add all bindings to the lua language.
 */
public class JoyceBindings
{
    private object _lo = new();
    
    /**
     * send an event.
     */
    public void ev(string type)
    {
        try
        {
            I.Get<EventQueue>().Push(new Event(type, null));
        }
        catch (Exception e)
        {
            Error($"Exception executing push event call: {e}");
            return;
        }
    }


    /**
     * send an event.
     */
    public void ev(string type, string code)
    {
        try
        {
            I.Get<EventQueue>().Push(new Event(type, code));
        }
        catch (Exception e)
        {
            Error($"Exception executing push event call: {e}");
            return;
        }
    }
}