using System.Numerics;

namespace nogame.characters.citizen;

/**
 * Published by CitizenCollisionRouter when an NPC's body is contacted by
 * the player's walking body (not a weapon, not a vehicle).
 *
 * Direction is the world-space contact normal at the impact point. The
 * receiving navigator should typically zero the Y component, normalize,
 * and use it as a brief lateral offset so the NPC steps out of the way.
 */
public class BumpEvent : engine.news.Event
{
    public Vector3 Direction;

    public BumpEvent(string type, Vector3 direction) : base(type, "")
    {
        Direction = direction;
    }
}
