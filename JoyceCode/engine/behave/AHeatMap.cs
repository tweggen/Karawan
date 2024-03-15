using System.Diagnostics;
using engine.joyce;

namespace engine.behave;

/**
 * Store SpawnStatus Information per fragment.
 * Keeps a two dimensional array of SpawnStatus records
 * This class is not thread safe by definition!!
 */
public abstract class AHeatMap
{
    protected float[,] _arrayDensity;
    protected readonly int _si = (int)(world.MetaGen.MaxWidth / world.MetaGen.FragmentSize);
    protected readonly int _sk = (int)(world.MetaGen.MaxHeight / world.MetaGen.FragmentSize);


    protected abstract float _computeDensity(in Index3 idxFragment);
    
    
    public float GetDensity(in Index3 idxFragment)
    {
        float density = _arrayDensity[_si/2+idxFragment.I, _sk/2+idxFragment.K];
        if (density >= 0f)
        {
             return density;
        }

        return _computeDensity(idxFragment);
    }


    protected AHeatMap()
    {
        _arrayDensity = new float[_si + 1, _sk + 1];
        /*
         * First clear the array. Then iterate through all clusters,
         * summing up the cluster partitions to a total of 10000 == 100%.
         */
        // _arraySpawnStatus.Fill<SpawnStatus>(emptySpawnStatus); not possible for 2d arrays
        
        for (int i = 0; i < _si; ++i)
        {
            for (int k = 0; k < _sk; ++k)
            {
                _arrayDensity[i, k] = -1f;
            }
        }

    }
}