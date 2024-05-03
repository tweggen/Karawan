using System.Collections.Generic;

namespace engine.world;

public class Tags
{
    private SortedSet<string>? _tags = null;

    public bool Contains(in string s)
    {
        lock (this)
        {
            if (_tags != null)
            {
                return _tags.Contains(s);
            }
            else
            {
                return false;
            }
        }
    }


    public void Set(in string s)
    {
        lock (this)
        {
            if (_tags == null)
            {
                _tags = new();
            }

            _tags.Add(s);
        }
    }
    
}