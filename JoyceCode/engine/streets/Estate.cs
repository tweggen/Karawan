using System;
using System.Numerics;
using System.Collections.Generic;

namespace engine.streets
{
    public class Estate
    {
        private List<Vector3> _points;
        private List<Building> _buildings;
        private bool _haveCenter = false;
        private Vector3 _center;
        private float _area;
        private Vector3 _min;
        private Vector3 _max;
        private Vector3 _extent;
        private bool _isCW;
        private List<Vector2>? _poly;

        public List<Vector3> GetPoints()
        {
            return _points;
        }


        public List<Building> GetBuildings()
        {
            return _buildings;
        }


        public void AddPoints(in List<Vector3> points)
        {
            foreach(var point in points)
            {
                _points.Add(point);
            }
        }


        public void AddPoint(Vector3 point)
        {
            _points.Add(point);
        }


        /**
         * Return a projection of this polygon on the z plane.
         */
        public List<Vector2> GetPoly()
        {
            if (null != _poly)
            {
                return _poly;
            }
            _poly = new List<Vector2>();
            foreach(var point in _points)
            {
                _poly.Add(new Vector2(point.X, point.Z));
            }
            _isCW = geom.PolyTools.isCW(_poly);
            _area = geom.PolyTools.getArea(_poly) * (_isCW ? 1.: -1);
            return _poly;
        }


        public void AddBuilding(Building building)
        {
            _buildings.Add(building);
        }


        public Vector3 GetCenter()
        {
            if (!_haveCenter)
            {
                _min = new Vector3(1000000000f, 1000000000f, 1000000000f );
                _max = new Vector3(-1000000000f, -1000000000f, -1000000000f );
                _center = new Vector3(0f, 0f, 0f );
                foreach(var p in _points )
                {
                    _min.X = Math.Min(_min.X, p.X);
                    _min.Y = Math.Min(_min.Y, p.Y);
                    _min.Z = Math.Min(_min.Z, p.Z);
                    _max.X = Math.Max(_max.X, p.X);
                    _max.Y = Math.Max(_max.Y, p.Y);
                    _max.Z = Math.Max(_max.Z, p.Z);
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
                _extent = _max;
                _extent -= _min;
                _haveCenter = true;
            }
            return _center;
        }


        public Vector3 GetMaxExtent()
        {
            if (!_haveCenter)
            {
                GetCenter();
            }
            return _extent;
        }


        public Vector3 getMin()
        {
            if (_haveCenter)
            {
                GetCenter();
            }
            return _min;
        }


        public Vector3 GetMax()
        {
            if (!_haveCenter)
            {
                GetCenter();
            }
            return _max;
        }


        public float GetArea()
        {
            if (null == _poly)
            {
                GetPoly();
            }
            return _area;
        }


        public bool IsInside(Vector3 p)
        {
            var poly = GetPoly();
            return geom.PolyTools.isInside(poly, new geom.Point(p.x, p.z));
        }


        public Estate()
        {
            _points = new List<Vector3>();
            _buildings = new List<Building>();
        }
    }
}
