using Android.Systems;
using Java.Lang;
using Java.Util.Functions;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace engine.world
{
    public class ClusterDesc
    {
        public bool merged;
        public Vector3 pos;
        public float size = 100;
        public string name = "Unnamed";

        public float averageHeight = 0f;

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


        public Vector3 getPos() {
                return pos;
        }


        public string toString()
        {
            return $"{{ 'id': {_strKey}; 'name': {name}; 'pos': {pos}; 'size': {size}; }}";
        }

        public string getKey()
        {
            return _strKey;
        }


        public bool isInside(Vector3 p)
        {
            if (
            p.X >= (pos.X - size / 2f) && p.X <= (pos.X + size / 2f)
                && p.Z >= (pos.Z - size / 2f) && p.z <= (pos.Z + size / 2f)
            )
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        public streets.Quarter guessQuarter(Vector2 p)
        {
            /*
             * Fast wrong implementation: Find the closest center point.
             */
            var pc = p;
            pc.X -= pos.X;
            pc.Y -= pos.Y;
            float minDist = 9999999999f;
            streets.Quarter minQuarter = null;
            foreach(var quarter in _quarterStore.getQuarters() )
            {
                var cp = quarter.getCenterPoint();
                var dist = cp.distTo(pc);
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


        public function addClosest(other:ClusterDesc )
            {
            if (other == this) return;

            var distance:Float = Math.sqrt(
                (other.x - this.x) * (other.x - this.x)
                + (other.z - this.z) * (other.z - this.z));

            // Special case first.
            if (0 == _nClosest)
            {
                _arrCloseCities[0] = other;
                _nClosest = 1;
                return;
            }

            // Now insert, whereever required
            var idx:Int = 0;
            while (idx < _nClosest)
            {
                var cl:ClusterDesc = _arrCloseCities[idx];
                // Also ignore this if already known.
                if (cl == other)
                {
                    idx++;
                    return;
                }

                var clDist:Float = Math.sqrt(
                    (cl.x - this.x) * (cl.x - this.x)
                    + (cl.z - this.z) * (cl.z - this.z));

                if (distance < clDist)
                {
                    // Smaller distance? Then insert myself here.
                    var idx2:Int = idx + 1;
                    var max:Int = _nClosest + 1;
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


        /**
         * Using the information about the next cities, create seed points for 
         * the map based on the interconnecting stations.
         */
        public void _addHighwayTriggers()
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
            for( i in 0...nSeeds ) {
                var newA = new StreetPoint();
                var x = _rnd.get8()*((2.*size)/3.)/256.-size/3.;
                var y = _rnd.get8()*((2.*size)/3.)/256.-size/3.;
                newA.setPos( x, y );
                var dir = _rnd.get8()*Math.PI/128.;
                var newB = new StreetPoint();
                var stroke = Stroke.createByAngleFrom( newA, newB, dir, 30., true, 1.5 );
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


        private void triggerStreets()
        {
            if( null==_streetGenerator ) {
                _strokeStore = new streets.StrokeStore();
                _streetGenerator = new streets.Generator();
                _quarterStore = new streets.QuarterStore();
                _quarterGenerator = new streets.QuarterGenerator();

                _streetGenerator.reset( "streets-"+_strKey, _strokeStore );
                _streetGenerator.setBounds( -size/2f, -size/2f, size/2f, size/2f );
                _addHighwayTriggers();
                _streetGenerator.generate();

                /*
                 * Unfortunately, we also need to generate the sections at this point.
                 */
                 foreach( var sp in _strokeStore.getStreetPoints()) {
                    sp.getSectionArray();
                }

                _quarterGenerator.reset( "quarters-"+_strKey, _quarterStore, _strokeStore );
                _quarterGenerator.generate();
            }
        }


    public streets.Generator streetGenerator()
    {
        triggerStreets();
        return _streetGenerator;
    }


    public streets.QuarterGenerator quarterGenerator()
    {
        triggerStreets();
        return _quarterGenerator;
    }


    public streets.StrokeStore strokeStore() 
    {
        triggerStreets();
        return _strokeStore;
    }


    public streets.QuarterGenerator quarterStore() 
    {
        triggerStreets();
        return _quarterStore;
    }


    public ClusterDesc(string strKey)
    {
        _strKey = strKey;
        _rnd = new engine.RandomSource(_strKey);
        _arrCloseCities = new ClusterDesc[_maxClosest];
        _nClosest = 0;
        merged = false;
        // Street generator will be initialized on demand.
        _streetGenerator = null;
    }
    }
}
