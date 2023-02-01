using System.Numerics;
using System.Collections.Generic;

namespace engine.streets
{
    public class Building
    {
        private List<Vector3> _points;
        private bool _haveCenter = false;
        private Vector3 _center;
        private float _height = 1f;

        public List<Vector3> GetPoints()
        {
            return _points;
        }


        public void AddPoints(in List<Vector3> points)
        {
            foreach(var point in points) 
            {
                _points.Add(point);
            }
        }


        public void AddPoint(in Vector3 point)
        {
            _points.Add(point);
        }

        public void SetHeight( float height )
        {
            _height = height;
        }


        public float GetHeight()
        {
            return _height;
        }


        public Vector3 getCenter()
        {
            if( !_haveCenter ) {
                _haveCenter = true;
                _center = new Vector3( 0f, 0f, 0f );
                foreach( var p in _points ) {
                    _center.X += p.X;
                    _center.Y += p.Y;
                    _center.Z += p.Z;
                }
                if( _points.Count > 0 ) {
                    _center.X = _center.X / _points.Count;
                    _center.Y = _center.Y / _points.Count;
                    _center.Z = _center.Z / _points.Count;
                }
            }
            return _center;
        }


        public Building()
        {
            _points = new List<Vector3>();
        }
    }
}
