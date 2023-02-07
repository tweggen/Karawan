using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;
using engine;
using engine.world;
using engine.streets;

#if true
namespace nogame.cities
{
    public class StreetNavigationController
    {
        private engine.RandomSource _rnd;

        /*
         * Coordinates are relative to cluster.
         */
        private Vector3 _pos;

        /**
         * Meters per second.
         */
        private float _speed;
    
        private bool _avoidDeadEnds;

    private ClusterDesc _clusterDesc;
    private StreetPoint _startPoint;
    private StreetPoint _prevStart;
    private Stroke _currentStroke;
    private StreetPoint _targetPoint;

    /**
     * Contains the last directeion of the character, the length
     * represents the meter per second.
     */
    private Vector3 _lastDirection;


    private bool _isAB;


    private void _loadNextPoint(StreetPoint previousPoint)
        {
            /*
             * We select a random stroke and use its destination point.
             * If the destination point has one stroke only (which must be
             * the one we origin from), we consider that street point only
             * if dead ends are allowed.
             */
            IList<Stroke> strokes = _startPoint.GetAngleArray();
            if (0 == strokes.Count)
            {
                throw  new InvalidOperationException( $"CubeCharacter.loadNextPoint(): Encountered empty street point." );
            }

            var nTries = 0;
            while (true)
            {
                ++nTries;
                var idx = (int)(_rnd.getFloat() * strokes.Count);
                _currentStroke = strokes[idx];
                if (null == _currentStroke)
                {
                    throw new InvalidOperationException( "CubeCharacter.loadNextPoint(): strokes[{idx}] is null." );
                }
                if (null == _currentStroke.A)
                {
                    throw new InvalidOperationException( "CubeCharacter.loadNextPoint(): _currentStroke.A is null." );
                }
                if (null == _currentStroke.B)
                {
                    throw new InvalidOperationException("CubeCharacter.loadNextPoint(): _currentStroke.B is null.");
                }
                if (_currentStroke.A == _startPoint)
                {
                    _isAB = true;
                    _targetPoint = _currentStroke.B;
                }
                else
                {
                    _isAB = false;
                    _targetPoint = _currentStroke.A;
                }

            /*
             * Is the target point the prevoius point? 
             * That's a valid path of course.
             */
            var targetStrokes = _targetPoint.GetAngleArray();

            /*
             * There's no other path? Then take it.
             */
            if( strokes.Count==1 ) {
                break;
            }

            /*
             * OK, target point is not the previous point amd not the only option.
             */
            if( 1==targetStrokes.Count && _avoidDeadEnds ) {

                if( nTries < strokes.Count ) {
                    _targetPoint = null;
                    continue;
                }
            }

            if( _targetPoint != previousPoint ) {
                break;
            }

        }
    }

    private void _loadStartPoint()
    {
            _pos = new Vector3(_startPoint.Pos.X, 0f, _startPoint.Pos.Y);
    }


    public void NavigatorBehave(float difftime)
    {

        /*
            * The meters to go.
            */
        float togo = _speed * difftime;

        /*
            * Iterate over movement until we used all the difftime.
            */
        while(togo>0.000001f) {

            /*
                * Be sure to have a destination point and compute the vector
                * to it and its length to have the unit vector to it.
                */
            float ux = 0.0f; 
            float uy = 0.0f;
            float dist = 0.0f;
            while(true) {
                if( null==_targetPoint ) {
                    _loadNextPoint(_prevStart);
                    _prevStart = null;
                }
                /*
                    * Compute a proper offset to emulate a bit
                    * of traffic on right-hand side.
                    *
                    * tx is the target we move to, ux is the
                    * unit vector of the direction.
                    */
                var tx = _targetPoint.Pos.X;
                var ty = _targetPoint.Pos.Y;

                var sx = _startPoint.Pos.X;
                var sy = _startPoint.Pos.Y;

                var sux = tx - sx;
                var suy = ty - sy;
                {
                    var sul = (float) Math.Sqrt(sux*sux+suy*suy);
                    if( sul>0.00001f ) {
                        sux = sux / sul;
                        suy = suy / sul;
                    }
                }
                /*
                    * Offset the target one unit towards the start point
                    * and one unit right-hand-side of start to target.
                    */
                tx = tx 
                    - sux /* one unit to start */ 
                    + 2.5f*suy /* two units to the right */;
                ty = ty 
                    - suy /* one unit to the start */
                    - 2.5f*sux /* two units to the right */;

                /*
                    * understand how long we still need to go and set the direction.
                    */
                var dx = tx - _pos.X;
                var dy = ty - _pos.Z;
                dist = (float) Math.Sqrt( dx*dx+dy*dy );
                if( dist>0.05f ) {
                    ux = dx/dist;
                    uy = dy/dist;

                    _lastDirection = new Vector3( ux*_speed, 0f, uy*_speed );
                    
                    break;
                }
                _prevStart = _startPoint;
                _startPoint = _targetPoint;
                _targetPoint = null;
                // No, don't reload the new start point, just iterate to the next one.
                // loadStartPoint();
            }

            /*
                * move towards the target with the given speed
                */
            var gonow = togo;

            /*
                * Did we reach the end of the stroke? Then load the next.
                */
            if( gonow > dist ) {
                gonow = dist;
                togo -= gonow;
                _prevStart = _startPoint;
                _startPoint = _targetPoint;
                _targetPoint = null;
                // loadStartPoint();
                continue;
            }

            togo -= gonow;
            _pos.X += ux * gonow;
            _pos.Z += uy * gonow;
        }

            /*
             * We are on streets, not on terrain, so we use the cluster's average height.
             */
            _pos.Y = _clusterDesc.AverageHeight;
    }


    public Vector3 NavigatorGetWorldPos()
        {
        return new Vector3( _pos.X + _clusterDesc.Pos.X, _pos.Y, _pos.Z + _clusterDesc.Pos.Z );
    }


    public Vector3 NavigatorGetLastDirection()
        {
        return _lastDirection;
    }


    public Vector3 NavigatorGetLinearVelocity()
        {
        return _lastDirection;
    }


    public Quaternion NavigatorGetAngularVelocity()
        {
        return new Quaternion( 0f, 0f, 0f, 1f );
    }


    public Quaternion NavigatorGetOrientation()
        {
        var vYAxis = new Vector3( 0f, 1f, 0f );
#if false
            /*
             * z axis is minus direction
             */
            var vZAxis = _lastDirection;
            vZAxis *= -1f;
        var qOrientation = Quaternion.fromYZBases( vYAxis, vZAxis );
        return qOrientation;
#else
            var vForward = _lastDirection;
            Matrix4x4 rot = Matrix4x4.CreateWorld(new Vector3(0f, 0f, 0f), vForward, vYAxis);
            return Quaternion.CreateFromRotationMatrix(rot);
#endif

        }


    public void NavigatorSetSpeed( float speed )
        {
        _speed = speed;
    }

    public void NavigatorAvoidDeadEnds( bool avoid) {
        _avoidDeadEnds = avoid;
    }


    public StreetNavigationController(
        ClusterDesc clusterDesc0,
        StreetPoint startPoint0
    ) {
        _rnd = new engine.RandomSource(clusterDesc0.Name);
        _clusterDesc = clusterDesc0;
        _startPoint = startPoint0;
        _targetPoint = null;
        _prevStart = null;
        _lastDirection = new Vector3(1f, 0f, 0f);
        _avoidDeadEnds = false;

        _speed = 2.7f * 5f;

        _loadStartPoint();

        NavigatorBehave(1f/60f);
    }    }
}

#endif