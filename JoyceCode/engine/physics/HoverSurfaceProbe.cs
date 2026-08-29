using System;

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
 * IT IS AN ADDITION, NOT A REPLACEMENT, and that is the part to get right. The ray sees
 * only things with COLLIDERS, and the terrain has none, so outside a city - and inside
 * one, over anything the city did not build on - it reports nothing at all. The two
 * answers are therefore combined with a MAXIMUM: the terrain height is a floor the ship
 * never flies below, and a built surface can only ever raise the target above it.
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
     * which is the terrain-derived target exactly, so the maximum below changes nothing
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


    /**
     * The height the hover loop should fly at.
     *
     * @param terrainHoverHeight
     *     What the loop has always used: the terrain sample under the ship plus
     *     ClusterNavigationHeight.
     * @param surfaceHeightBelow
     *     Top of the nearest built surface under the ship, or null when the probe found
     *     nothing - off the edge of the world, over open country, or high above
     *     everything. Falling back to the terrain height there is not a degraded mode,
     *     it is the answer: outside a city the terrain is the only thing the ship can
     *     be over, and that is precisely the case the existing height was built for.
     */
    public static float HoverTargetHeight(float terrainHoverHeight, float? surfaceHeightBelow)
    {
        if (surfaceHeightBelow is null)
        {
            return terrainHoverHeight;
        }

        return Single.Max(terrainHoverHeight, surfaceHeightBelow.Value + SurfaceClearance);
    }
}
