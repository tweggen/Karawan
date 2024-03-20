using System;
using System.Collections.Generic;
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
    public int ToKill = 0;


    public List<SpawnInfo>? PossibleVictims;

    public void ClearPerFrame()
    {
        NumberEntities = 0;
    }
        
    
    public void Add()
    {
        ++NumberEntities;
    }

    
    public List<SpawnInfo> NeedVictims()
    {
        if (null == PossibleVictims)
        {
            PossibleVictims = new();
        }

        return PossibleVictims;
    }
}