using System.Collections.Generic;
using engine;
using engine.news;

namespace builtin.controllers;


/**
 * Map the controller / keyboard input to logical game buttons.
 *
 * This can be configured by json.
 */
public class InputMapper : AModule
{
    private SortedDictionary<string, string> _mapButtonToLogical = new ();

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
    };

    private EventQueue _eq;
    
    
    public SortedDictionary<string, string> MapButtonToLogical
    {
        get
        {
            lock (_lo)
            {
                return _mapButtonToLogical;
            }
        }
        set
        {
            lock (_lo)
            {
                _mapButtonToLogical = new SortedDictionary<string, string>(value);;
            }
        }
    }
    

    private SortedDictionary<string, string> _mapLogicalToDescription = new ();
    
    public SortedDictionary<string, string> MapLogicalToDescription
    {
        get
        {
            lock (_lo)
            {
                return _mapLogicalToDescription;
            }
        }
        set
        {
            lock (_lo)
            {
                _mapLogicalToDescription = new SortedDictionary<string, string>(value);
            }
        }
    }


    public Event? ToLogical(Event ev)
    {
        lock (_lo)
        {
            if (_mapButtonToLogical.TryGetValue(ev.ToKey(), out var codeLogical))
            {
                int seperatorPos = codeLogical.IndexOf(':');
                if (-1 != seperatorPos)
                {
                    return new Event(
                        codeLogical.Substring(0,seperatorPos), 
                        codeLogical.Substring(seperatorPos+1, codeLogical.Length-seperatorPos-1)
                        );
                }
            }
        }

        return null;
    }


    public void TranslateEmitLogical(engine.news.Event ev)
    {
        Event? evLogical = ToLogical(ev);
        if (null != evLogical)
        {
            I.Get<EventQueue>().Push(evLogical);
        }
    }

    
    public void EmitPlusTranslation(engine.news.Event ev)
    {
        if (null == _eq)
        {
            return;
        }

        _eq.Push(ev);
        Event? evLogical = ToLogical(ev);
        if (null != evLogical)
        {
            _eq.Push(evLogical);
        }
    }

    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _eq = I.Get<EventQueue>();
        _engine.AddModule(this);
    }
}

