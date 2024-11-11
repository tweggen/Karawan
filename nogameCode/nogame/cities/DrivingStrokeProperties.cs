using System.Numerics;

namespace nogame.cities;

public struct DrivingLanesArray
{
    /**
     * The maximal amount of driving lanes per directions.
     * Keep this to a power of two.
     */
    public const int MaxLanes = 1<<3;

    private DrivingLaneProperties?[] _laneProperties;
    
    public DrivingLaneProperties? this[int idx]
    {
        get => _laneProperties[(idx) & (MaxLanes - 1)];
        set { _laneProperties[(idx) & (MaxLanes - 1)] = value; }
    }


    public DrivingLanesArray()
    {
        _laneProperties = new DrivingLaneProperties?[MaxLanes * 2];
    }
}

/**
 * These numbers are constant per lane.
 */
public class DrivingStrokeProperties
{
    public Vector2 VStreetTarget;
    public Vector2 VStreetStart;
    public Vector2 VUStreetDirection;
    public float StreetWidth;

    public DrivingLanesArray Lanes;
}


