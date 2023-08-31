using System;
using System.Numerics;
using System.Collections.Generic;
using ClipperLib;

namespace engine.streets
{
    public class Estate
    {
        private object _lo = new();
        
        private List<Vector3> _points = new();
        private List<Building> _buildings = new();
        private bool _haveCenter = false;
        private Vector3 _center;
        private float _area;
        private Vector3 _min;
        private Vector3 _max;
        private Vector3 _extent;
        private bool _isCW;
        private List<IntPoint>? _poly;

        public List<Vector3> GetPoints()
        {
            lock (_lo)
            {
                return _points;
            }
        }


        public List<Building> GetBuildings()
        {
            lock (_lo)
            {
                return _buildings;
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


        public void AddPoint(Vector3 point)
        {
            lock (_lo)
            {
                _points.Add(point);
            }
        }


        /**
         * Return a projection of this polygon on the z plane.
         */
        public List<IntPoint> GetIntPoly()
        {
            lock (_lo)
            {
                if (null != _poly)
                {
                    return _poly;
                }

                _poly = new List<IntPoint>();
                foreach (var point in _points)
                {
                    _poly.Add(new IntPoint(point.X * 10, point.Z * 10));
                }

                _isCW = Clipper.Orientation(_poly);
                _area = (float)Clipper.Area(_poly) * (_isCW ? 1f : -1f) / 100f;
                return _poly;
            }
        }


        public void AddBuilding(Building building)
        {
            lock (_lo)
            {
                _buildings.Add(building);
            }
        }


        public Vector3 GetCenter()
        {
            lock (_lo)
            {
                if (!_haveCenter)
                {
                    _min = new Vector3(1000000000f, 1000000000f, 1000000000f);
                    _max = new Vector3(-1000000000f, -1000000000f, -1000000000f);
                    _center = new Vector3(0f, 0f, 0f);
                    foreach (var p in _points)
                    {
                        _min.X = Math.Min(_min.X, p.X);
                        _min.Y = Math.Min(_min.Y, p.Y);
                        _min.Z = Math.Min(_min.Z, p.Z);
                        _max.X = Math.Max(_max.X, p.X);
                        _max.Y = Math.Max(_max.Y, p.Y);
                        _max.Z = Math.Max(_max.Z, p.Z);
                        _center += p;
                    }

                    if (_points.Count > 0)
                    {
                        _center = _center / _points.Count;
                    }

                    _extent = _max;
                    _extent -= _min;
                    _haveCenter = true;
                }

                return _center;
            }
        }


        public Vector3 GetMaxExtent()
        {
            GetCenter();
            return _extent;
        }


        public Vector3 getMin()
        {
            GetCenter();
            return _min;
        }


        public Vector3 GetMax()
        {
            GetCenter();
            return _max;
        }


        public float GetArea()
        {
            GetIntPoly();
            return _area;
        }


        public bool IsInside(in Vector3 p)
        {
            var poly = GetIntPoly();
            return Clipper.PointInPolygon(
                new IntPoint(
                    (int)(p.X * 10.0 + 0.5),
                    (int)(p.Z * 10.0 + 0.5)
                ),
                (List<IntPoint>) _poly
            ) != 0;
        }
    }
}
