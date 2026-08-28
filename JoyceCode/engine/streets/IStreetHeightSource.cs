namespace engine.streets;


/**
 * Where the ground is, under a junction.
 *
 * Streets have always been built at one height for the whole city, because
 * ClusterBaseElevationOperator irons the terrain flat underneath them first. This is
 * the seam that lets that stop being true: everything that emits street geometry asks
 * here instead of reading ClusterDesc.AverageHeight, and a source that returns the
 * average reproduces the flat city exactly.
 *
 * Two properties are load bearing, and both are why this is per JUNCTION rather than
 * per vertex or per stroke end:
 *
 * - **One junction, one height.** A junction is a single node in the stroke graph, and
 *   every stroke meeting there reads its height from here. That is what makes a
 *   non-planar network consistent by construction - no stroke can disagree with itself,
 *   and two streets meeting at a junction meet at exactly one height. Sample the same
 *   spot twice and get two answers, and the road splits open along the seam.
 * - **Fragment independence.** The same junction is asked about from every fragment its
 *   strokes reach into, so the answer may not depend on which fragment is asking.
 *
 * Implementations must therefore be deterministic in the junction, and are expected to
 * cache rather than resample.
 */
public interface IStreetHeightSource
{
    /**
     * Height of the ground surface under this junction, in world space.
     *
     * Deck elevation is NOT included: callers add StreetLevels.ElevationOf on top, so
     * that "which deck" and "how high is the ground here" stay the separate quantities
     * they are.
     */
    float GroundHeightAt(StreetPoint sp);
}


/**
 * Which source a city gets, as one named decision.
 *
 * Separate from ClusterDesc so that the choice can be exercised without writing to a
 * process global: setting the flag for real would leak into any test running beside
 * it, and a city that picked TerrainStreetHeight in a harness with no elevation cache
 * would fail somewhere unrelated.
 */
public static class StreetHeightSources
{
    /**
     * Set to "true" to leave the terrain under a city alone and let the streets follow
     * it. Read by ClusterBaseElevationOperator, which does the not-flattening, and here,
     * which does the following - the two halves of one decision, so they share the name
     * rather than each spelling it out.
     */
    public const string DisableClusterFlatteningSetting = "joyce.DisableClusterFlattening";


    public static IStreetHeightSource For(world.ClusterDesc clusterDesc, bool followTerrain)
    {
        if (!followTerrain)
        {
            return new FlatStreetHeight(clusterDesc);
        }

        /*
         * Raw terrain would give a three-dimensional city with whatever gradients the
         * noise happened to produce, so the sample is always relaxed before use. The
         * two are separable and separately tested, but there is no case for shipping
         * the unrelaxed one.
         */
        return new RelaxedStreetHeight(
            clusterDesc, new TerrainStreetHeight(clusterDesc), new GradePolicy());
    }


    public static IStreetHeightSource For(world.ClusterDesc clusterDesc)
    {
        return For(
            clusterDesc,
            GlobalSettings.Get(DisableClusterFlatteningSetting) == "true");
    }
}
