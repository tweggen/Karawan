using System.Collections.Generic;
using static engine.Logger;

namespace engine.streets;


/**
 * A height source with the unbuildable gradients taken out of it.
 *
 * Wraps another source - in the game, TerrainStreetHeight - and runs GradeRelaxer over
 * the whole cluster's stroke graph the first time anybody asks. Whole graph at once,
 * because relaxing one junction moves its neighbours: there is no per-junction answer to
 * give until the network has settled.
 *
 * Flat in, flat out. Every stroke of a flat network is already within any grade limit,
 * so no correction is ever computed and the result is the underlying source unchanged -
 * which is what keeps this safe to have in the chain.
 */
public sealed class RelaxedStreetHeight : IStreetHeightSource
{
    private static readonly engine.Dc _dc = engine.Dc.StreetGen;

    private readonly world.ClusterDesc _clusterDesc;
    private readonly IStreetHeightSource _base;
    private readonly GradePolicy _policy;

    private readonly object _lo = new();
    private Dictionary<int, float> _heights;


    /**
     * What is being relaxed. Exposed so that a test can check the chain a city is
     * actually wired with, rather than only that the outermost layer is right.
     */
    internal IStreetHeightSource Base => _base;


    /**
     * Relaxation only ever removes gradients, so it cannot make a non-flat network flat
     * nor a flat one otherwise. The answer is whatever is underneath.
     */
    public bool IsFlat => _base.IsFlat;


    public float GroundHeightAt(StreetPoint sp)
    {
        Dictionary<int, float> heights = _ensureRelaxed();

        /*
         * A junction the relaxation never saw - one built after the fact, or outside
         * this cluster's store - still needs an answer, and the unrelaxed one is a
         * better answer than none.
         */
        return heights.TryGetValue(sp.Id, out float h) ? h : _base.GroundHeightAt(sp);
    }


    private Dictionary<int, float> _ensureRelaxed()
    {
        lock (_lo)
        {
            if (null != _heights)
            {
                return _heights;
            }
        }

        /*
         * Sampled and relaxed outside the lock. StrokeStore() can trigger street
         * generation, and the base source can pull in a neighbouring fragment's
         * elevation; holding a lock across either would put unrelated work behind this
         * cluster. Two threads racing both compute the same answer - the relaxation is
         * deterministic in the graph and the starting heights - and the first to store
         * wins.
         */
        var store = _clusterDesc.StrokeStore();

        var heights = TableFor(
            _clusterDesc, store.GetStrokes(), store.GetStreetPoints(), _base, _policy);

        lock (_lo)
        {
            _heights ??= heights;
            return _heights;
        }
    }


    /**
     * Sample the base source at every junction, relax the result, and say so if it did
     * not settle.
     *
     * The whole of what this class does, as a function of things a caller can hand it,
     * so that "the budget running out is REPORTED" is a property a test can drive rather
     * than one that needs ClusterStorage, MetaGen and the event queue in the container.
     * The two lines left above it are the store lookup and the memoisation.
     *
     * ⚠️ This exists because for as long as the pass had existed, nobody read what
     * GradeRelaxer.Relax returned. It has always handed back its sweep count "useful to
     * a caller that wants to complain about it"; there was no such caller, and since
     * joyce.DisableClusterFlattening became the default every city in the game was
     * running an unconverged relaxation with nothing anywhere saying so. A pass that
     * silently gives up is the Trace-in-a-catch shape this project keeps being bitten by.
     *
     * The exhaustion is a log line and nothing else - deliberately, rather than also a
     * property on this class. Where a fact is observable is where a test can hold it, and
     * a mirror field that only this class could read would be state nothing drives. It is
     * a Warning, not a Trace, because a debug category decides how much DETAIL to keep
     * and never whether a problem is reported.
     */
    internal static Dictionary<int, float> TableFor(
        world.ClusterDesc clusterDesc, IList<Stroke> strokes,
        IList<StreetPoint> streetPoints, IStreetHeightSource baseSource,
        GradePolicy policy)
    {
        var heights = new Dictionary<int, float>();
        foreach (var sp in streetPoints)
        {
            heights[sp.Id] = baseSource.GroundHeightAt(sp);
        }

        int sweeps = GradeRelaxer.Relax(strokes, heights, policy);

        /*
         * The city is named, because the answer to "which one?" is what decides whether
         * this is a ruleset that has outgrown the budget or one pathological site.
         */
        if (sweeps >= policy.MaxSweeps)
        {
            Warning(_dc,
                $"Cluster {clusterDesc.IdString} ({clusterDesc.Size:F0} m, "
                + $"{strokes.Count} strokes) used all {policy.MaxSweeps} relaxation "
                + "sweeps without settling, so some of its streets are steeper than "
                + "GradePolicy allows. The city is still built; its grades are not "
                + "guaranteed.");
        }

        return heights;
    }


    public RelaxedStreetHeight(
        world.ClusterDesc clusterDesc, IStreetHeightSource baseSource, GradePolicy policy)
    {
        _clusterDesc = clusterDesc;
        _base = baseSource;
        _policy = policy;
    }
}
