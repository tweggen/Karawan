using System.Collections.Generic;
using builtin.modules.satnav.desc;

namespace builtin.modules.satnav;

public class LocalPathfinder
{
    private HashSet<NavJunction> _hashVisitedJunction = new();

    private void _addOptions(NavJunction njSource, NavJunction njTarget)
    {
        foreach (var nl in njSource.StartingLanes)
        {
            var nj = nl.End;
            if (_hashVisitedJunction.Contains(nj))
            {
                continue;
            }
        }
    }
    
}