using engine.joyce;

namespace engine.behave;

/**
 * Store SpawnStatus Information per fragment.
 * Keeps a two dimensional array of SpawnStatus records
 * This class is not thread safe by definition!!
 */
abstract public class AHeatMap
{
    protected float[,] _arrayDensity;


    protected abstract float _computeDensity(in Index3 idxFragment);
    
    
    public float GetDensity(in Index3 idxFragment)
    {
        float density = _arrayDensity[idxFragment.I, idxFragment.K];
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