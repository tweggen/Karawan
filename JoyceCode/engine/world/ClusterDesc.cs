using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using engine.streets;

namespace engine.world
{
    public class ClusterDesc
    {
        /**
         * To protect me, especially generating streets
         */
        private object _lo = new();

        public string Id;
        public bool Merged;

        private Vector3 _pos;
        public Vector3 Pos
        {
            get => _pos;
            set => _setPos(value);
        }

        public Vector2 Pos2
        {
            get => new Vector2(Pos.X, Pos.Z);
        }

        private float _size = 100;
        public float Size
        {
            get => _size;
            set => _setSize(value);
        }
        
        public string Name = "Unnamed";

        public float AverageHeight = 0f;

        private ClusterDesc[] _arrCloseCities;
        private int _nClosest;
        private int _maxClosest = 5;
        private string _strKey;
        private engine.RandomSource _rnd;
        private engine.geom.AABB _aabb;

        /** 
         * Each cluster has a stroke store associated that descirbes the 
         * street graph.
         */
        private streets.StrokeStore _strokeStore;

        /**
         * Street generator will be initialized on demand.
         */
        private streets.Generator _streetGenerator;

        /**
         * In addition, each cluster has a lot generator associated.
         */
        private streets.QuarterGenerator _quarterGenerator;

        /**
         * This is the store for all quarters we generated.
         */
        private streets.QuarterStore _quarterStore;


        private void _setSize(float size)
        {
            _size = size;
            _aabb = new geom.AABB(_pos, size);

        }
        
        private void _setPos(Vector3 pos)
        {
            _pos = pos;
            _aabb = new geom.AABB(pos, _size);

        }
        
        public override string ToString()
        {
            return $"{{ 'id': {_strKey}; 'name': {Name}; 'pos': {Pos}; 'size': {Size}; }}";
        }

        public string GetKey()
        {
            return _strKey;
        }


        public bool IsInside(in Vector3 p)
        {
            if (
                p.X >= (Pos.X - Size / 2f) && p.X <= (Pos.X + Size / 2f)
                && p.Z >= (Pos.Z - Size / 2f) && p.Z <= (Pos.Z + Size / 2f)
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void GetAABB(out engine.geom.AABB aabb)
        {
            aabb = _aabb;
        }
        

        public streets.Quarter GuessQuarter(in Vector2 p)
        {
            _triggerStreets();
            /*
             * Fast wrong implementation: Find the closest center point.
             */
            var pc = p;
            pc.X -= Pos.X;
            pc.Y -= Pos.Y;
            float minDist = 9999999999f;
            streets.Quarter minQuarter = null;
            foreach(var quarter in _quarterStore.GetQuarters() )
            {
                var cp = quarter.GetCenterPoint();
                float dist = Vector2.Distance( cp, pc );
                if (dist < minDist)
                {
                    minDist = dist;
                    minQuarter = quarter;
                }
            }
            return minQuarter;
        }


        public ClusterDesc[] GetClosest()
        {
            return _arrCloseCities;
        }


        public int GetNClosest()
        {
            return _nClosest;
        }


        public void AddClosest(in ClusterDesc other)
        {
            lock(_lo)
            { 
                if (other == this) return;

                float distance = (float)Vector3.Distance(other.Pos, this.Pos);

                // Special case first.
                if (0 == _nClosest)
                {
                    _arrCloseCities[0] = other;
                    _nClosest = 1;
                    return;
                }

                // Now insert, whereever required
                int idx = 0;
                while (idx < _nClosest)
                {
                    ClusterDesc cl = _arrCloseCities[idx];
                    // Also ignore this if already known.
                    if (cl == other)
                    {
                        idx++;
                        return;
                    }

                    float clDist = (float)Vector3.Distance(cl.Pos, this.Pos);

                    if (distance < clDist)
                    {
                        // Smaller distance? Then insert myself here.
                        int idx2 = idx + 1;
                        int max = _nClosest + 1;
                        if (max > _maxClosest) max = _maxClosest;
                        while (idx2 < max)
                        {
                            _arrCloseCities[idx2] = _arrCloseCities[idx2 - 1];
                            ++idx2;
                        }
                        _arrCloseCities[idx] = other;
                        _nClosest = max;
                        // Inserted.
                        return;
                    }
                    idx++;
                }
            }
        }


        /**
         * Using the information about the next cities, create seed points for 
         * the map based on the interconnecting stations.
         */
        private void _addHighwayTriggers()
        {
            // TXWTODO: Do it acually
            /*
             * Variant one: From each of the sides.
             */
#if false
            {
                var newA = new StreetPoint();
                newA.setPos( -size/3., -size/3. );
                var newB = new StreetPoint();
                var stroke = Stroke.createByAngleFrom( newA, newB, Math.PI*0.25, 30., true, 1.5 );
                _streetGenerator.addStartingStroke(stroke);
            }
            {
                var newA = new StreetPoint();
                newA.setPos( size/3., -size/3. );
                var newB = new StreetPoint();
                var stroke = Stroke.createByAngleFrom( newA, newB, 3.*Math.PI*0.25, 30., true, 1.5 );
                _streetGenerator.addStartingStroke(stroke);
            }
            {
                var newA = new StreetPoint();
                newA.setPos( -size/3., size/3. );
                var newB = new StreetPoint();
                var stroke = Stroke.createByAngleFrom( newA, newB, -Math.PI*0.25, 30., true, 1.5 );
                _streetGenerator.addStartingStroke(stroke);
            }
            {
                var newA = new StreetPoint();
                newA.setPos( size/3., size/3. );
                var newB = new StreetPoint();
                var stroke = Stroke.createByAngleFrom( newA, newB, -3.0*Math.PI*0.25, 30., true, 1.5 );
                _streetGenerator.addStartingStroke(stroke);
            }
#endif
            /*
             * Variant two: n random points
             */
            var nSeeds = (_rnd.Get8()>>5)+1;
            for( int i=0; i<nSeeds; ++i ) {
                engine.streets.StreetPoint newA = new engine.streets.StreetPoint();
                float x = _rnd.Get8()*((2f*Size)/3f)/256f-Size/3f;
                float y = _rnd.Get8()*((2f*Size)/3f)/256f-Size/3f;
                newA.SetPos( x, y );
                float dir = _rnd.Get8()*(float)Math.PI/128f;
                var newB = new engine.streets.StreetPoint();
                var stroke = engine.streets.Stroke.CreateByAngleFrom( newA, newB, dir, 30f, true, 1.5f );
                _streetGenerator.AddStartingStroke(stroke);
            }
#if true
            /*
             * Plus the four corners.
             */
            {
                var newA = new StreetPoint();
                newA.SetPos( -Size/3f, -Size/3f );
                var newB = new StreetPoint();
                var stroke = Stroke.CreateByAngleFrom( newA, newB, (float)Math.PI*0.25f, 30f, true, 1.5f );
                _streetGenerator.AddStartingStroke(stroke);
            }
            {
                var newA = new StreetPoint();
                newA.SetPos( Size/3f, -Size/3f );
                var newB = new StreetPoint();
                var stroke = Stroke.CreateByAngleFrom( newA, newB, 3f*(float)Math.PI*0.25f, 30f, true, 1.5f );
                _streetGenerator.AddStartingStroke(stroke);
            }
            {
                var newA = new StreetPoint();
                newA.SetPos( -Size/3f, Size/3f );
                var newB = new StreetPoint();
                var stroke = Stroke.CreateByAngleFrom( newA, newB, -(float)Math.PI*0.25f, 30f, true, 1.5f );
                _streetGenerator.AddStartingStroke(stroke);
            }
            {
                var newA = new StreetPoint();
                newA.SetPos( Size/3f, Size/3f );
                var newB = new StreetPoint();
                var stroke = Stroke.CreateByAngleFrom( newA, newB, -3.0f*(float)Math.PI*0.25f, 30f, true, 1.5f );
                _streetGenerator.AddStartingStroke(stroke);
            }
#endif
        }


        private void _triggerStreets()
        {
            lock (_lo)
            {
                if (null == _streetGenerator)
                {
                    _strokeStore = new streets.StrokeStore();
                    _streetGenerator = new streets.Generator();
                    _quarterStore = new streets.QuarterStore();
                    _quarterGenerator = new streets.QuarterGenerator();

                    _streetGenerator.Reset("streets-" + _strKey, _strokeStore);
                    _streetGenerator.SetBounds(-Size / 2f, -Size / 2f, Size / 2f, Size / 2f);
                    _addHighwayTriggers();
                    _streetGenerator.Generate();

                    /*
                     * Unfortunately, we also need to generate the sections at this point.
                     */
                    foreach (var sp in _strokeStore.GetStreetPoints())
                    {
                        sp.GetSectionArray();
                    }

                    _quarterGenerator.Reset("quarters-" + _strKey, _quarterStore, _strokeStore);
                    _quarterGenerator.Generate();
                }
            }
        }


    public streets.Generator StreetGenerator()
    {
        _triggerStreets();
        return _streetGenerator;
    }


    public streets.QuarterGenerator QuarterGenerator()
    {
        _triggerStreets();
        return _quarterGenerator;
    }


    public streets.StrokeStore StrokeStore() 
    {
        _triggerStreets();
        return _strokeStore;
    }


    public streets.QuarterStore QuarterStore() 
    {
        _triggerStreets();
        return _quarterStore;
    }

    
    public ClusterDesc(string strKey)
    {
        _strKey = strKey;
        _rnd = new engine.RandomSource(_strKey);
        _arrCloseCities = new ClusterDesc[_maxClosest];
        _nClosest = 0;
        Merged = false;
        // Street generator will be initialized on demand.
        _streetGenerator = null;
    }
    }
}
