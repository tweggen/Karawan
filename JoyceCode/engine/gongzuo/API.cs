using System.Collections.Generic;
using System.Collections.Immutable;

namespace engine.gongzuo;

public class API
{
    private SortedDictionary<string, object> _mapTemplateBindings = new();

    public void AddDefaultBinding(string key, object bindings)
    {
        _mapTemplateBindings[key] = bindings;
    }


    public void RemoveDefaultBinding(string key, object bindings)
    {
        _mapTemplateBindings.Remove(key);
    }
    
    
    public IEnumerable<object> GetDefaultBindings()
    {
        return _mapTemplateBindings.Values.ToImmutableList();
    }
    
    
    public SortedDictionary<string, object> GetDefaultBindingMap()
    {
        return _mapTemplateBindings;
    }
}


