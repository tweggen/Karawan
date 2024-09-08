using System.Collections.Generic;
using System.Numerics;

namespace builtin.modules.satnav.desc;

public class NavJunction
{
    public Vector3 Position;
    public List<NavLane> StartingLanes;
    public List<NavLane> EndingLanes;
}