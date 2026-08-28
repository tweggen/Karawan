using System.Collections.Generic;
using System.Numerics;

namespace engine.streets;


/**
 * Junctions stand on the terrain.
 *
 * Only meaningful once ClusterBaseElevationOperator has been told to stop flattening
 * the cluster rectangle (joyce.DisableClusterFlattening); against a flattened city it
 * would return the average anyway, more slowly.
 *
 * The cache is the contract, not a speed-up. IStreetHeightSource promises one height
 * per junction no matter who asks, and a junction is asked about from every fragment
 * its strokes reach into - the stroke surface is emitted in the fragment holding its A
 * end, which for a stroke crossing a fragment boundary is not the fragment holding B.
 * Resampling per caller would be a second chance to get a different answer, and two
 * answers at one junction is a hole in the road.
 */
public sealed class TerrainStreetHeight : IStreetHeightSource
{
    private readonly world.ClusterDesc _clusterDesc;

    private readonly Dictionary<int, float> _cache = new();
    private readonly object _lo = new();


    public bool IsFlat => false;


    public float GroundHeightAt(StreetPoint sp)
    {
        lock (_lo)
        {
            if (_cache.TryGetValue(sp.Id, out float cached))
            {
                return cached;
            }
        }

        /*
         * StreetPoint.Pos is cluster relative; the elevation cache is not.
         *
         * Sampled outside the lock: this can force a neighbouring fragment's elevation
         * to be computed, and holding a lock across that would put this cluster's
         * street geometry behind terrain generation. Two threads racing on the same
         * junction both compute, and the first to store wins - which is safe precisely
         * because the sample is a pure function of the position.
         */
        Vector3 v3World = new(
            _clusterDesc.Pos.X + sp.Pos.X,
            0f,
            _clusterDesc.Pos.Z + sp.Pos.Y);

        float h = I.Get<world.MetaGen>().Loader.GetHeightAt(v3World);

        lock (_lo)
        {
            if (_cache.TryGetValue(sp.Id, out float raced))
            {
                return raced;
            }

            _cache[sp.Id] = h;
        }

        return h;
    }


    public TerrainStreetHeight(world.ClusterDesc clusterDesc)
    {
        _clusterDesc = clusterDesc;
    }
}
