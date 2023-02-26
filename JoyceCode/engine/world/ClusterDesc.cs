using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

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
        public Vector3 Pos;
        public float Size = 100;
        public string Name = "Unnamed";

        public float AverageHeight = 0f;

        private ClusterDesc[] _arrCloseCities;
        private int _nClosest;
        private int _maxClosest = 5;
        private string _strKey;
        private engine.RandomSource _rnd;

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


        public Vector3 GetPos() {
            return Pos;
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


        public streets.Quarter guessQuarter(in Vector2 p)
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


        public ClusterDesc[] getClosest()
        {
            return _arrCloseCities;
        }


        public int getNClosest()
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
            var nSeeds = (_rnd.get8()>>5)+1;
            for( int i=0; i<nSeeds; ++i ) {
                engine.streets.StreetPoint newA = new engine.streets.StreetPoint();
                float x = _rnd.get8()*((2f*Size)/3f)/256f-Size/3f;
                float y = _rnd.get8()*((2f*Size)/3f)/256f-Size/3f;
                newA.SetPos( x, y );
                float dir = _rnd.get8()*(float)Math.PI/128f;
                var newB = new engine.streets.StreetPoint();
                var stroke = engine.streets.Stroke.CreateByAngleFrom( newA, newB, dir, 30f, true, 1.5f );
                _streetGenerator.addStartingStroke(stroke);
            }
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

                    _streetGenerator.reset("streets-" + _strKey, _strokeStore);
                    _streetGenerator.setBounds(-Size / 2f, -Size / 2f, Size / 2f, Size / 2f);
                    _addHighwayTriggers();
                    _streetGenerator.generate();

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


    public streets.Generator streetGenerator()
    {
        _triggerStreets();
        return _streetGenerator;
    }


    public streets.QuarterGenerator quarterGenerator()
    {
        _triggerStreets();
        return _quarterGenerator;
    }


    public streets.StrokeStore strokeStore() 
    {
        _triggerStreets();
        return _strokeStore;
    }


    public streets.QuarterStore quarterStore() 
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
