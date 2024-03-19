using System;
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
    
    /*
     * The actual spawn status for this fragment, if we have it.
     */
    public SpawnStatus? SpawnStatus = null;
    
    /**
     * The number of characters to kill inside this fragment. 
     */
    
    /**
     * The number of characters to spawn inside this fragment
     */
    
    public void Add()
    {
        ++NumberEntities;
    }
}