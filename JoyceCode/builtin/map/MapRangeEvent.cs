using engine.news;

namespace builtin.map;

public class MapRangeEvent : Event
{
    public required engine.geom.AABB AABB;
    
    public MapRangeEvent(string code) : base(MAP_RANGE_EVENT, code)
    {
    }
}