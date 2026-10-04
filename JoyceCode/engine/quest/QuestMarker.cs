using System.Numerics;

namespace engine.quest;


/**
 * The geometry of the cube that marks a quest goal.
 *
 * Two numbers used to live apart and disagree. The mesh was scaled to
 * (SensitiveRadius, 3, SensitiveRadius) about the goal's own position, so its visible
 * bottom was one and a half metres BELOW the position the quest had chosen, and no quest
 * knew that: every one of them chose a position for the marker's middle while thinking
 * about where its bottom would land.
 *
 * In the shipped flat city that read as correct by coincidence.
 * ClusterBaseElevationOperator writes the ground at the city average plus 1.5 - a constant
 * unrelated to CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE, which is 2.0 - so a marker at
 * terrain + ClusterNavigationHeight put its bottom exactly one metre over the road and
 * looked deliberate. A city that keeps its terrain has no such bias, and the cube's lower
 * half went under the road: measured over every junction of the four baseline cities,
 * bottom minus pavement was -0.64 to -0.67 m at the median, -1.29 to -2.03 at p05, -9.77
 * at the worst, and below the pavement at 88 to 93 % of junctions.
 *
 * So the cube RESTS on the position it is given, and the position is a surface height.
 * Both halves are needed and neither is sufficient: resting the cube on a position that is
 * still the terrain plus the vehicle hover clearance leaves it 8.3 m under the pavement at
 * that same worst junction, and a surface height under a cube that straddles it still
 * buries half of it.
 */
public static class QuestMarker
{
    /**
     * How tall the marker cube is drawn, in metres.
     *
     * One copy, because the offset that rests it on the ground is half of it: two literals
     * is how a marker comes to straddle its own anchor again.
     */
    public const float Height = 3f;


    /**
     * The mesh scale for a goal of the given sensitive radius.
     */
    public static Vector3 ScaleFor(float sensitiveRadius)
        => new(sensitiveRadius, Height, sensitiveRadius);


    /**
     * Where the mesh sits relative to the goal, so that the cube stands ON the goal rather
     * than around it.
     *
     * engine.joyce.mesh.Tools.CreateCubeMesh is centred on its own origin, so lifting it by
     * half its height puts its bottom face exactly on the anchor. The marker also spins,
     * and the spin is about Y and applied by TransformApi as scale, then rotation, then
     * translation - so it turns about its own centre and the bottom face does not move.
     */
    public static Vector3 RestOffset => new(0f, Height / 2f, 0f);


    /**
     * Where the bottom face of the marker ends up, given the goal's own height.
     *
     * Written out rather than left implicit because it is the whole claim: the visible
     * bottom is at the anchor, and the anchor is a surface.
     */
    public static float BottomOf(float anchorY) => anchorY + RestOffset.Y - Height / 2f;
}
