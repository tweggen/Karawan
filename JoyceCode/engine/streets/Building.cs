using System;
using System.Numerics;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.streets;

public class Building
{
    private object _lo = new();

    public required world.ClusterDesc ClusterDesc;

    private List<ShopFront> _shopfronts = new();
    
    private List<Vector3> _points = new();
    private bool _haveCenter = false;
    private Vector3 _center;
    private float _height = 1f;


    public List<ShopFront> GetShopFronts()
    {
        lock (_lo)
        {
            return _shopfronts;
        }
    }


    public void AddShopFront(ShopFront shopFront)
    {
        lock (_lo)
        {
            _shopfronts.Add(shopFront);
        }
    }
    

    public List<Vector3> GetPoints()
    {
        lock (_lo)
        {
            return _points;
        }
    }


    /**
     * Add this building's points. We expect the points to be in the right order. 
     */
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


    public Vector3 GetCenter()
    {
        lock (_lo)
        {
            if (!_haveCenter)
            {
                _haveCenter = true;
                _center = Vector3.Zero;
                foreach (var p in _points)
                {
                    _center += p;
                }

                if (_points.Count > 0)
                {
                    _center /= _points.Count;
                }
            }

            return _center;
        }
    }
}