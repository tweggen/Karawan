namespace builtin.modules.satnav.desc;

public class NavCursor
{
    // Sentinel returned by TryCreateCursor when no cursor can be produced.
    // Must be a real instance — callers do `cursor.IsNil()` without a null check.
    public static readonly NavCursor Nil = new NavCursor();

    private bool _isNil = true;
    public NavCluster Cluster;
    public NavLane Lane;
    public NavJunction Junction;

    public bool IsNil()
    {
        return _isNil;
    }

    private NavCursor()
    {
        // Nil sentinel only — _isNil keeps its field default of true.
    }

    public NavCursor(NavCluster nc)
    {
        Cluster = nc;
        _isNil = false;
    }
}