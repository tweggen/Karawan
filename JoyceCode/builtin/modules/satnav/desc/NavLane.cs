namespace builtin.modules.satnav.desc;


/**
 * This is a navigable lane.
 * It describes one edge of the navigation graph.
 * It is directed by nature and may contain further conditions that
 * more closely specify the way things may navigate within.
 */
public class NavLane
{
    public NavJunction Start;
    public NavJunction End;

    public float MaxSpeed;
    public float Length;
}