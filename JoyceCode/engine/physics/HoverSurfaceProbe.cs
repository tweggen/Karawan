using System;
using System.Numerics;

namespace engine.physics;


/**
 * Where a hover vehicle should aim to fly, given what is actually underneath it.
 *
 * The controller has always aimed at ONE height: the terrain under the ship plus
 * MetaGen.ClusterNavigationHeight. That is the right answer over open ground, where the
 * terrain is all there is - it carries no collider at all, so the hover loop IS its
 * collision - and the wrong one everywhere a city has been built, because what the ship
 * drives on there is a STREET, and a street is not the terrain.
 *
 * The two disagree by construction and the disagreement is signed. Street heights are
 * relaxed to buildable gradients, so a road crossing a hillside is cut into it at one
 * end and stands on FILL at the other; wherever the fill exceeds the metre between the
 * two constants below, the road surface is above the height the loop is aiming at and
 * the loop commands a descent into the road for as long as the ship drives along it.
 * Proportional descent and a slippery ship made that survivable. It did not make it
 * right: the ship was still being told to fly below the surface it was standing on.
 *
 * So ask the physics world instead. A ray straight down from the ship reports the
 * surface it is over, exactly the way the walking player already finds the floor.
 *
 * The ray sees only things with COLLIDERS, and the terrain has none, so outside a city -
 * and inside one, over anything the city did not build on - it reports nothing at all,
 * and the terrain height is the answer there. Where it does find a built surface, that
 * surface wins, also BELOW the terrain beside it: a road in a cutting is driven on, not
 * flown over (VehicleTargetHeight). The two used to be combined with a maximum, which
 * floated the ship above the road in every cutting - at 43.8 % of ground-street points
 * in the terrain city - until the owner decided otherwise (2026-10-31, KAR-472).
 */
public static class HoverSurfaceProbe
{
    /**
     * How far above a built surface the ship should hover, in metres.
     *
     * DERIVED, and it has to be, because the whole change is gated on a flat city coming
     * out bit for bit unchanged. In a flat city every drivable surface - the fragment
     * floor plane, the quarter floors, a deck collider on a level stroke - has its top
     * face at AverageHeight + CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE, while the loop's
     * existing target is AverageHeight + ClusterNavigationHeight. The ship hovers the
     * DIFFERENCE of the two above the surface, one metre, and so:
     *
     *     surface + SurfaceClearance
     *   = AverageHeight + CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE
     *       + ClusterNavigationHeight - CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE
     *   = AverageHeight + ClusterNavigationHeight
     *
     * which is the terrain-derived target exactly, so taking the surface changes nothing
     * whatsoever. Any other clearance - ClusterNavigationHeight itself, most temptingly -
     * moves the entire default city vertically.
     */
    public static float SurfaceClearance
        => world.MetaGen.ClusterNavigationHeight - world.MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;


    /**
     * The layers a body can be in and still not be something to hover over.
     *
     * Everything here MOVES THROUGH the world rather than being part of it: the player on
     * foot, NPCs, their vehicles, the things either of them shoots or swings, coins, quest
     * markers.
     */
    public const CollisionProperties.Layers MovingLayers =
        CollisionProperties.Layers.Player
        | CollisionProperties.Layers.PlayerWeapon
        | CollisionProperties.Layers.Npc
        | CollisionProperties.Layers.NpcWeapon
        | CollisionProperties.Layers.Collectable
        | CollisionProperties.Layers.QuestMarker;


    /**
     * Is a body with this solid mask a surface the ship should hold its height above?
     *
     * Deliberately NOT a plain intersection test with MovingLayers, which would reject
     * most of the city: a house declares no mask at all and therefore keeps the default
     * Layers.All, which intersects everything. The question is whether the body is ONLY
     * one of the moving kinds. A mask with any other bit in it is part of the world -
     * terrain, a quarter floor, a house, a wall - and a mask with none at all is not
     * solid to anything and is not a surface either.
     *
     * The reason for excluding the moving kinds is worth stating, because "hover over
     * whatever the ray finds" is the obvious implementation and it is a trap. The climb
     * side of the servo keeps FULL authority by design - being slow to rise is being
     * inside a hillside - so a pedestrian walking under a parked ship would raise its
     * target by their own height and launch it. Vehicles and NPCs are the solver's
     * business: they generate contacts, and a contact is how a hover ship should learn
     * about something that can walk away.
     */
    public static bool IsHoverSurface(CollisionProperties.Layers solidLayerMask)
        => 0 != (solidLayerMask & ~MovingLayers);


    /*
     * ================================ shared by every hovering body ===================
     *
     * The player's ship, a crashed NPC car and a knocked-down citizen all need to know
     * what is built under them (KAR-471). The ray cast and the rule for which hits count
     * lived in HoverController alone; the other two compared themselves with the terrain
     * and were thrown out of every road cutting.
     */

    /** How far above the body the probe starts: a ray beginning inside a convex shape reports nothing. */
    public const float DefaultProbeAbove = 2f;

    /** How far below the body the probe looks; deep enough to cover the drop from a deck to the street under it. */
    public const float DefaultProbeBelow = 24f;

    /** How far below the terrain-derived height a body must be before it is put back on top. */
    public const float DefaultRescueDepth = 4f;

    /** Vertical speed, in m/s, above which a body counts as falling rather than resting on something. */
    public const float DefaultRestingVerticalSpeed = 2f;


    /**
     * Does a ray hit count as a surface to hover over?
     *
     * A hit on the body itself never does. A body with collision properties counts when it
     * is tangible and not one of the moving kinds (IsHoverSurface). A collider with no
     * properties at all is the city's own fabric - the fragment floor plane, the
     * carriageway and junction prisms are raw statics - and counts when static or
     * kinematic; an unlabelled DYNAMIC body is a loose object, and hovering over loose
     * objects turns a hover vehicle into a pogo stick.
     */
    public static bool IsProbeSurface(CollisionProperties props, bool isStaticOrKinematic, in DefaultEcs.Entity self)
    {
        if (props != null)
        {
            if (props.Entity == self) return false;
            return 0 != (props.Flags & CollisionProperties.CollisionFlags.IsTangible)
                   && IsHoverSurface(props.SolidLayerMask);
        }

        return isStaticOrKinematic;
    }


    /**
     * Top of the nearest built surface under <paramref name="position"/>, or null when
     * there is none - over open country, off the world, or high above everything.
     * API.RayCastSync takes the simulation lock itself: call it from the logical thread and
     * do NOT wrap it in a lock of its own.
     */
    public static float? SurfaceBelow(API physics, in Vector3 position, in DefaultEcs.Entity self,
        float probeAbove = DefaultProbeAbove, float probeBelow = DefaultProbeBelow)
    {
        Vector3 origin = position with { Y = position.Y + probeAbove };
        float closest = Single.MaxValue;
        var me = self;

        physics.RayCastSync(origin, -Vector3.UnitY, probeAbove + probeBelow,
            (BepuPhysics.Collidables.CollidableReference cRef, CollisionProperties props, float t, Vector3 _) =>
            {
                bool isStaticOrKinematic = cRef.Mobility is BepuPhysics.Collidables.CollidableMobility.Static
                    or BepuPhysics.Collidables.CollidableMobility.Kinematic;
                if (IsProbeSurface(props, isStaticOrKinematic, me) && t < closest)
                {
                    closest = t;
                }
            });

        return closest == Single.MaxValue ? null : origin.Y - closest;
    }


    /**
     * The height a hover vehicle - the player's ship (KAR-472), a crashed NPC car
     * (KAR-471) - aims at: the built surface under it plus SurfaceClearance when there is
     * one, the terrain-derived height only where nothing is built.
     *
     * A built surface may LOWER the target: in a road cutting the terrain beside the road
     * is higher than the road, and a vehicle that aimed at the terrain there floated above
     * the road or climbed out of it. It is the height NPC traffic rides at (KAR-470), so
     * every vehicle on a road rides level with the others.
     */
    public static float VehicleTargetHeight(float terrainHoverHeight, float? surfaceHeightBelow)
        => surfaceHeightBelow is { } surface ? surface + SurfaceClearance : terrainHoverHeight;


    /**
     * Must a body be put back on top of the terrain? Only when it has gone through the
     * world: more than <paramref name="rescueDepth"/> below the terrain-derived height,
     * falling, and with nothing built under it. Depth alone would catch a body resting in
     * a cutting, falling alone a body mid-bounce, and a built surface below means the
     * body is above something that will catch it.
     */
    public static bool NeedsRescue(float terrainHeight, float y, float verticalVelocity, float? surfaceHeightBelow,
        float rescueDepth = DefaultRescueDepth, float restingVerticalSpeed = DefaultRestingVerticalSpeed)
        => surfaceHeightBelow is null
           && verticalVelocity < -restingVerticalSpeed
           && terrainHeight - y > rescueDepth;
}
