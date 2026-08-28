using System.Collections.Generic;

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
        var strokes = store.GetStrokes();

        var heights = new Dictionary<int, float>();
        foreach (var sp in store.GetStreetPoints())
        {
            heights[sp.Id] = _base.GroundHeightAt(sp);
        }

        GradeRelaxer.Relax(strokes, heights, _policy);

        lock (_lo)
        {
            _heights ??= heights;
            return _heights;
        }
    }


    public RelaxedStreetHeight(
        world.ClusterDesc clusterDesc, IStreetHeightSource baseSource, GradePolicy policy)
    {
        _clusterDesc = clusterDesc;
        _base = baseSource;
        _policy = policy;
    }
}
