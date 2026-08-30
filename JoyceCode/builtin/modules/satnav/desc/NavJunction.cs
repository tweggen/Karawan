using System.Collections.Generic;
using System.Numerics;
using engine.world;

namespace builtin.modules.satnav.desc;

public class NavJunction
{
    /**
     * Where something navigating through this junction is, in world space: the ground
     * under it plus ClusterNavigationHeight, which is the VEHICLE hover reference and
     * not where anybody's feet go.
     */
    public Vector3 Position;

    /**
     * Height of the ground this junction stands on - the terrain under it in a flat
     * city, the relaxed street height of the junction it was traced from in one that
     * follows its terrain.
     *
     * This is the field NPCs walk by. A junction position is the only place in the world
     * where "how high is the road here" has an exact answer rather than a sample near
     * one, and lanes measure with Vector3.Distance and split with Vector3.Lerp, so a
     * height at each junction already interpolates linearly along a lane at no per-NPC
     * cost. What was missing was somewhere to put it that did not already have an offset
     * baked in: Position carries the vehicle clearance, and a walker wants
     * ClusterStreetHeight plus QuarterSidewalkOffset instead. Subtracting one constant
     * to add another is how the two silently drift apart, so the ground is stored and
     * each consumer adds its own.
     */
    public float GroundHeight;

    public List<NavLane> StartingLanes;
    public List<NavLane> EndingLanes;


    /**
     * Where a vehicle navigating this junction sits.
     */
    public static float NavigationHeightOf(float groundHeight)
        => groundHeight + MetaGen.ClusterNavigationHeight;


    /**
     * Where a walker's feet go: the street surface, plus the kerb.
     */
    public static float WalkingHeightOf(float groundHeight)
        => groundHeight + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset;


    public float WalkingHeight => WalkingHeightOf(GroundHeight);


    /**
     * @param v3Plan
     *     Where the junction is. Y is replaced by the navigation height.
     * @param groundHeight
     *     Height of the ground under it.
     */
    public static NavJunction At(Vector3 v3Plan, float groundHeight) => new()
    {
        Position = v3Plan with { Y = NavigationHeightOf(groundHeight) },
        GroundHeight = groundHeight,
        StartingLanes = new(),
        EndingLanes = new()
    };


    /**
     * A junction part way along the straight line between two others, as
     * GenerateNavMapOperator makes when it splits a lane longer than MaxLaneLength.
     *
     * The GROUND is interpolated and the position follows from it, rather than the
     * position being interpolated and the ground left behind - which is the shape that
     * would leave a split lane's intermediate junctions reporting a ground height of
     * zero while looking perfectly right, and would drop every NPC walking over one to
     * 2.15 m above sea level.
     */
    public static NavJunction Between(NavJunction njA, NavJunction njB, float t) => At(
        Vector3.Lerp(njA.Position, njB.Position, t),
        float.Lerp(njA.GroundHeight, njB.GroundHeight, t));


    /**
     * As At, for a junction synthesised at a point that is already ON a lane and
     * therefore already carries the navigation height.
     */
    public static NavJunction AtNavigationHeight(Vector3 v3Nav) => new()
    {
        Position = v3Nav,
        GroundHeight = v3Nav.Y - MetaGen.ClusterNavigationHeight,
        StartingLanes = new(),
        EndingLanes = new()
    };
}
