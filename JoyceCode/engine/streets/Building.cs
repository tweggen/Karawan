using System.Numerics;
using System.Collections.Generic;

namespace engine.streets;

public class Building
{
    private object _lo = new();

    public required engine.world.ClusterDesc ClusterDesc; 
    
    private List<Vector3> _points = new();
    private bool _haveCenter = false;
    private Vector3 _center;
    private float _height = 1f;


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

    public void SetHeight(float height)
    {
        lock (_lo)
        {
            _height = height;
        }
    }


    public float GetHeight()
    {
        lock (_lo)
        {
            return _height;
        }
    }


    public Vector3 getCenter()
    {
        lock (_lo)
        {
            if (!_haveCenter)
            {
                _haveCenter = true;
                _center = new Vector3(0f, 0f, 0f);
                foreach (var p in _points)
                {
                    _center.X += p.X;
                    _center.Y += p.Y;
                    _center.Z += p.Z;
                }

                if (_points.Count > 0)
                {
                    _center.X = _center.X / _points.Count;
                    _center.Y = _center.Y / _points.Count;
                    _center.Z = _center.Z / _points.Count;
                }
            }
        }

        return _center;
    }
}