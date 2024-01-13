using System;

namespace builtin.map;

/**
 * Represents part of the total map.
 */
public class MapFragment
{
    public engine.Engine Engine { get; private set; }
    
    private int _id;
    public int NumericalId
    {
        get => _id; 
    }
    public DateTime LoadedAt { get; private set; }
    public engine.geom.AABB AABB;

    
    public MapFragment(engine.Engine engine0)
    {
        Engine = engine0;
        _id = Engine.GetNextId();
    }
}