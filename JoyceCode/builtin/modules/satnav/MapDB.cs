using System.Collections.Generic;
using SharpNav;

namespace builtin.modules.satnav;



public class MapDB
{
    private object _lo = new();
    
    private SortedDictionary<int, NavMesh> _dictFragmentToNavMesh = new();
    
    public MapDB()
    {
    }
}