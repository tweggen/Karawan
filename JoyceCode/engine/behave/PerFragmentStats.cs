using engine.behave;
using engine.joyce;
using engine.joyce.components;
using FbxSharp;
using static engine.Logger;

namespace engine.behave;

/**
 * Collects per fragment data for a given behavior.
 */
public class PerFragmentStats
{
    /**
     * The number of entities found inside.
     */
    public int NumberEntities = 0;
    
    // List of positions?
    public void Add()
    {
        // Trace($"Incrementing from {NumberEntities}");
        ++NumberEntities;
    }
}