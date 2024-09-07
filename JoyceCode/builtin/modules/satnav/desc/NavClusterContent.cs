using System.Collections.Generic;

namespace builtin.modules.satnav.desc;


/**
 * This represents the actual navigable graph of a cluster.
 */
public class NavClusterContent
{
    public List<NavJunction> Junctions = new();
    public List<NavLane> Lanes = new();
}