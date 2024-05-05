using System.Collections.Generic;
using System.Numerics;

namespace engine.streets;


/**
 * Shopfronts on buildings are the equivalent of available retail
 * space to rent in reality. They are populated only by tagging them with
 * a specific shop.
 *
 * The house generator will read the tags in turn.
 */
public class ShopFront
{
    public engine.world.Tags Tags { get; } = new();
    
    private object _lo = new();
    private List<Vector3> _points = new();
    
    public List<Vector3> GetPoints()
    {
        lock (_lo)
        {
            return _points;
        }
    }


    public void AddPoints(in List<Vector3> points)
    {
        lock (_lo)
        {
            foreach (var point in points)
            {
                _points.Add(point);
            }
        }
    }


    public void AddPoint(in Vector3 point)
    {
        lock (_lo)
        {
            _points.Add(point);
        }
    }
}