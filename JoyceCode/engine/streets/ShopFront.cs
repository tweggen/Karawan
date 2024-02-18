using System.Collections.Generic;
using System.Numerics;

namespace engine.streets;

public class ShopFront
{
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