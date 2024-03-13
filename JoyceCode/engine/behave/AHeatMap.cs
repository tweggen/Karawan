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
        float density = _arrayDensity[_si+idxFragment.I, _si+idxFragment.K];
        if (density >= 0f)
        {
            return density;
        }

        return _computeDensity(idxFragment);
    }


    protected AHeatMap()
    {
        _arrayDensity =
            new float[
                (int)(world.MetaGen.MaxWidth / world.MetaGen.FragmentSize) + 1,
                (int)(world.MetaGen.MaxHeight / world.MetaGen.FragmentSize) + 1];
    }
}