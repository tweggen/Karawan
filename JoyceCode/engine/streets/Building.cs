using System;
using System.Numerics;
using System.Collections.Generic;
using static engine.Logger;
using engine.world;

namespace engine.streets;

public class Building
{
    private object _lo = new();

    public required world.ClusterDesc ClusterDesc;

    public Tags Tags { get; } = new();

    private List<ShopFront> _shopfronts = new();
    
    private List<Vector3> _points = new();
    private bool _haveCenter = false;
    private Vector3 _center;
    private float _height = 1f;


    private bool _hasFooting;
    private float _footingLo, _footingHi;


    /**
     * The lowest and highest the block floor gets over this building's own footprint, once
     * somebody has worked it out.
     *
     * Cached here rather than recomputed, the way the block's floor is cached on its
     * Quarter: it is the EXACT range of a piecewise linear surface over a polygon
     * (generation.BlockFloor.TryBoundsOver), which is not free, and everything on the
     * building asks for it - the base, the height, and each shopfront's storey twice over.
     * The footprint and the block's height source are both fixed once the block is traced,
     * so there is nothing here to invalidate.
     */
    internal bool TryGetFooting(out float lo, out float hi)
    {
        lock (_lo)
        {
            lo = _footingLo;
            hi = _footingHi;
            return _hasFooting;
        }
    }


    internal void SetFooting(float lo, float hi)
    {
        lock (_lo)
        {
            _footingLo = lo;
            _footingHi = hi;
            _hasFooting = true;
        }
    }


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