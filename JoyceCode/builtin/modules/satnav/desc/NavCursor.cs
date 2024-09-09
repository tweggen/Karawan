namespace builtin.modules.satnav.desc;

public class NavCursor
{
    public static NavCursor Nil;
    
    private bool _isNil = true;
    public NavCluster Cluster;
    public NavLane Lane;
    public NavJunction Junction; 

    public bool IsNil()
    {
        return _isNil;
    }

    public NavCursor(NavCluster nc)
    {
        Cluster = nc;
        _isNil = false;
    }
}