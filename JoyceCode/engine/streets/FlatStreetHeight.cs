namespace engine.streets;


/**
 * The city as it has always been: one height everywhere.
 *
 * This is not a placeholder or a fallback, it is the shipping behaviour written down.
 * ClusterBaseElevationOperator flattens the cluster rectangle to its average, so the
 * ground under every junction really is that average, and returning it is exact rather
 * than approximate.
 *
 * Reads the cluster's average on each call rather than capturing it, because the
 * elevation operator computes it and a source may well be constructed first.
 */
public sealed class FlatStreetHeight : IStreetHeightSource
{
    private readonly world.ClusterDesc _clusterDesc;


    public float GroundHeightAt(StreetPoint sp)
    {
        return _clusterDesc.AverageHeight;
    }


    public FlatStreetHeight(world.ClusterDesc clusterDesc)
    {
        _clusterDesc = clusterDesc;
    }
}


/**
 * A height field given as a function, for tests: it lets a slope be described without
 * terrain, an elevation cache or a booted engine.
 *
 * Caches per junction id, which is not an optimisation but the contract from
 * IStreetHeightSource - a test may hand in a function it is not worth trusting to be
 * consistent, and this makes it so.
 */
public sealed class FuncStreetHeight : IStreetHeightSource
{
    private readonly System.Func<float, float, float> _fHeight;
    private readonly System.Collections.Generic.Dictionary<int, float> _cache = new();
    private readonly object _lo = new();


    public float GroundHeightAt(StreetPoint sp)
    {
        lock (_lo)
        {
            if (_cache.TryGetValue(sp.Id, out float cached))
            {
                return cached;
            }

            float h = _fHeight(sp.Pos.X, sp.Pos.Y);
            _cache[sp.Id] = h;

            return h;
        }
    }


    /**
     * @param fHeight
     *     Ground height from a junction's PLAN position, in cluster-relative
     *     coordinates - the same space StreetPoint.Pos is in.
     */
    public FuncStreetHeight(System.Func<float, float, float> fHeight)
    {
        _fHeight = fHeight;
    }
}
