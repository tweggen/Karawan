using System.Collections.Generic;
using engine;

namespace builtin.controllers;


/**
 * Map the controller / keyboard input to logical game buttons.
 *
 * This can be configured by json.
 */
public class InputMapper : AModule
{
    private SortedDictionary<string, string> _mapButtonToLogical = new ();

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
                _mapButtonToLogical = value;
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
                _mapLogicalToDescription = value;
            }
        }
    }

    
    
    public override void ModuleDeactivate()
    {
        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate()
    {
        base.ModuleActivate();
    }
}