using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using engine.behave;
using engine.world;
using engine.streets;
using static engine.Logger;
using static builtin.Workarounds;
using static builtin.extensions.JsonObjectNumerics;

namespace nogame.cities;


/**
 * These constants are per lane per vehicle.
 */
internal class DrivingStrokeCarProperties
{
    public float RightLane;
    public Vector2 VLaneOffset;
    public Vector2 VPerfectTarget;
    public Vector2 VPerfectStart;
    
    /**
     * How much shall we slow down before turning into the next segment?
     * Ranges from 1, not at all, to zero, very much.
     */
    public float TurnSlowDown;
}


public class StreetNavigationController : INavigator
{
    private builtin.tools.RandomSource _rnd;

    /*
     * Coordinates of the navigation relative to the cluster.
     */
    private Vector2 _v2Pos;

    /**
     * The target speed.
     */
    private float _speed = 30f * 3.6f;
    
    /**
     * The effective current speed.
     */
    private float _effectiveSpeed;

    /**
     * Meters above recommended riding level.
     */
    private float _height = 0f;

    private ClusterDesc _clusterDesc;
    private StreetPoint _startPoint;
    private Stroke _currentStroke;
    private Stroke _nextStroke;
    
    /**
     * The point we are heading to right now.
     */
    private StreetPoint _targetPoint;
    
    /**
     * The point we will go to after we reach targetPoint.
     */
    private StreetPoint _thenPoint;

    /**
     * Contains the last direction of the character, the length
     * represents the meter per second.
     */
    private Vector3 _v2LastSpeed;

    private Vector2 _vu2LastDirection;
    
    private RandomPathEnumerator _enumPath;

    private DrivingStrokeProperties _nsp;
    private DrivingStrokeCarProperties _ncp;
    

    public void NavigatorBehave(float difftime)
    {
        /*
         + The time to spend when going that expected way.
         */ 
        float tospend = difftime;

        /*
         * Iterate over movement until we used all the difftime.
         */
        while (tospend > 0.000001f)
        {

            /*
             * Be sure to have a destination point and compute the vector
             * to it and its length to have the unit vector to it.
             */
            Vector2 vu2Dest = Vector2.Zero;
            float dist = 0.0f;
            while (true)
            {
                /*
                 * Ask the next point iterator for the next point.
                 */
                if (null == _targetPoint)
                {
                    if (null == _thenPoint)
                    {
                        _enumPath.MoveNext();
                        (_currentStroke, _targetPoint) = _enumPath.Current;
                        _enumPath.MoveNext();
                        (_nextStroke, _thenPoint) = _enumPath.Current;

                    }
                    else
                    {
                        _currentStroke = _nextStroke;
                        _targetPoint = _thenPoint;
                        _enumPath.MoveNext();
                        (_nextStroke, _thenPoint) = _enumPath.Current;
                    }

                    /*
                     * Compute the properties per stroke.
                     */
                    {
                        DrivingStrokeProperties nsp = new();
                        
                        nsp.StreetWidth = _currentStroke.StreetWidth();
                        nsp.VStreetTarget = _targetPoint.Pos;
                        nsp.VStreetStart = _startPoint.Pos;
                        nsp.VUStreetDirection = V2Normalize(nsp.VStreetTarget - nsp.VStreetStart);
                        
                        _nsp = nsp;

                    }
                    
                    /*
                     * Compute the properties for this car per stroke.
                     */
                    {
                        DrivingStrokeCarProperties ncp = new();
                        
                        /*
                         * we slow down before the junctions depending on the angle between
                         * the strokes.
                         *
                         * We use the dot product aka cosine between the vectors.
                         */
                        ncp.TurnSlowDown =
                            (-Vector2.Dot(_currentStroke.Unit, _nextStroke.Unit) / 2f + 0.5f);

                        /*
                         * Compute a proper offset to emulate a bit
                         * of traffic on right-hand side.
                         *
                         * tx is the target we move to, ux is the
                         * unit vector of the direction.
                         */
                        /*
                         * Offset the target one unit towards the start point
                         * and one unit right-hand-side of start to target.
                         *
                         * Figure out the street lane
                         *
                         * Yes, this navigation is lacking proper curves.
                         */
                        if (_nsp.StreetWidth >= 16f)
                        {
                            /*
                             * On larger streets, select lane according to speed.
                             */
                            if (_speed >= (65f / 3.6f))
                            {
                                /*
                                 * fast? Drive on the Left hand side.
                                 */
                                ncp.RightLane = _nsp.StreetWidth / 2f - 5f;
                            }
                            else
                            {
                                /*
                                 * Slow? Drive on the right hand side.
                                 */
                                ncp.RightLane = _nsp.StreetWidth / 2f - 2f;
                            }
                        }
                        else
                        {
                            /*
                             * On small streets, just drive on the right.
                             */
                            ncp.RightLane = _nsp.StreetWidth / 2f - 2f;
                        }
                
                        ncp.VLaneOffset = ncp.RightLane * new Vector2(-_nsp.VUStreetDirection.Y, _nsp.VUStreetDirection.X);

                        /*
                         * This is where we actually are heading to.
                         */
                        ncp.VPerfectTarget = _nsp.VStreetTarget
                                             /*
                                              * Not quite to the center of the junction
                                              */
                                             - _nsp.VUStreetDirection * _nsp.StreetWidth / 2f
                                             /*
                                              * And up to the right lane.
                                              */
                                             + ncp.VLaneOffset;
                        
                        ncp.VPerfectStart = _nsp.VStreetStart + ncp.VLaneOffset;

                        _ncp = ncp;
                    }
                }
                else
                {
                    if (null == _thenPoint)
                    {
                        _enumPath.MoveNext();
                        (_nextStroke, _thenPoint) = _enumPath.Current;
                    }
                }
                
                
                /*
                 * Now, as a function of (nsp, ncp, current vehicle position), compute the next movement
                 */
                
                /*
                 * Now compute the actual target for the current iteration.
                 */
                Vector2 v2CurrentTarget = _ncp.VPerfectTarget;
                var v2PerfectDirection = _ncp.VPerfectTarget - _ncp.VPerfectStart;
                var v2PerfectDirectionLength = v2PerfectDirection.Length();
                var vu2PerfectDirection = v2PerfectDirection / v2PerfectDirectionLength;
                
                Vector2 v2PerfectMe = default;
                Vector2 v2MeFromStart = _v2Pos - _ncp.VPerfectStart;
                float lMeFromStart = v2MeFromStart.Length();
                
                /*
                 * If I am more than 10cm away from the start point, look from deviations
                 * from the perfect route by projecting my location on the perfect line
                 * between source and target.
                 */
                if (lMeFromStart > 0.1f)
                {
                    float lPerfectMeScale = 
                        V2Dot(v2MeFromStart, v2PerfectDirection)
                        / v2PerfectDirectionLength;

                    /*
                     * If we already did overshoot beyond target point, pick the next street point.
                     */
                    if (lPerfectMeScale >= v2PerfectDirectionLength)
                    {
                        dist = 0;
                        break;
                    }
                    
                    /*
                     * This is where I should be on the street.
                     */
                    v2PerfectMe = _ncp.VPerfectStart + vu2PerfectDirection * lPerfectMeScale;

                    /*
                     * This is how I'm off.
                     */
                    Vector2 v2Off = (v2PerfectMe - _v2Pos);
                    float offLength = v2Off.Length();
                    
                    /*
                     * So, if I'm further off than 1m, drive back to the street, heading a bit
                     * into the proper direction.
                     *
                     * Otherwise just target the "proper" target.
                     */
                    if (offLength > 1f)
                    {
                        v2CurrentTarget = v2PerfectMe + vu2PerfectDirection/2f;
                    }
                    else
                    {
                        v2CurrentTarget = _ncp.VPerfectTarget;
                    }
                }

                /*
                 * Compute the direction of the current path. Again, this might be back to
                 * street or on the street.
                 */
                var v2Dest = v2CurrentTarget - _v2Pos;
                vu2Dest = V2Normalize(v2Dest);

                /*
                 * Plus, we will need the distance to the final target.
                 * Squared distance is fine.
                 */
                var v2PerfectDelta = _ncp.VPerfectTarget - _v2Pos;
                float perfectDist2 = v2PerfectDelta.LengthSquared();

                {
                    /*
                     * Derive the current direction.
                     * If I have more than 5cm to ride to the actual computed target, which is not necessarily
                     * the next street node, use this
                     * computed direction as current direction and use the current speed
                     * as speed.
                     */
                    var v2ActualDelta = v2CurrentTarget - _v2Pos;
                    float actualDist2 = v2ActualDelta.LengthSquared();
                    if (actualDist2 > 0.0025f)
                    {
                        _vu2LastDirection = vu2Dest;
                    }
                    else
                    {
                        /*
                         * Otherwise, we would continue iterating to find the next street point.
                         */
                    }
                }

                {
                    /*
                     * Now check, how far I am from the perfect target, which is the street node I
                     * want to reach.
                     *
                     * If it's more than 5cm, tell the algorithm that I will still have dist to go.
                     * Dist is the distance between the computed target and the current position.
                     * And leave the loop, we figured out a new target.
                     */
                    if (perfectDist2 > 0.0025f)
                    {
                        /*
                         * Yes, there's still a way to go.
                         * However, return only what we can apply in the given direction.
                         * If the current target is not the street node, because we just
                         * use intermediate target e.g. after a collision, return that one.
                         * We would eventually use a route that directs us to the perfect target.
                         */

                        const float breakDist = 81f;
                        if (perfectDist2 < breakDist)
                        {
                            _effectiveSpeed =
                                // a constant minimal speed
                                _speed / 5f 
                                // depending, on how much it shall slow down, a part depending on the distance to the node
                                + (1f-_ncp.TurnSlowDown) * perfectDist2 / breakDist * _speed * 4f / 5f
                                // and, the standard
                                + (_ncp.TurnSlowDown) * _speed
                                ;
                        }
                        else
                        {
                            _effectiveSpeed = _effectiveSpeed * 0.99f + _speed * 0.01f;
                        }

                        dist = v2Dest.Length();
                        
                        _v2LastSpeed = new Vector3(vu2Dest.X * _effectiveSpeed, 0f, vu2Dest.Y * _effectiveSpeed);
                        break;
                    }
                }

                /*
                 * No, we do not have a proper way to go, look for the next street point.
                 */

                _startPoint = _targetPoint;
                _targetPoint = null;
            }

            /*
             * move towards the target with the given speed
             */
            var gonow = tospend * _effectiveSpeed;
            var spendNow = tospend;
            /*
             * Did we reach the end of the stroke? Then load the next.
             */
            if (gonow > dist)
            {
                gonow = dist;
                spendNow = gonow / _effectiveSpeed;
                tospend -= spendNow;
                _startPoint = _targetPoint;
                _targetPoint = null;
                //_v2Pos += vu2Dest * gonow;
                
                continue;
            }

            tospend -= spendNow;
            _v2Pos += vu2Dest * gonow;
        }
    }


    public float Height
    {
        get => _height;
        set => _height = value; 
    }


    public float Speed
    {
        get => _speed;
        set => _speed = value;
    }

    
    public void NavigatorGetTransformation(
        out Vector3 position,
        out Quaternion orientation)
    {
        var vYAxis = new Vector3(0f, 1f, 0f);
        var v3Forward = new Vector3(_vu2LastDirection.X, 0f, _vu2LastDirection.Y);
        Matrix4x4 rot = Matrix4x4.CreateWorld(new Vector3(0f, 0f, 0f), v3Forward, vYAxis);
        orientation = Quaternion.CreateFromRotationMatrix(rot);
        position =new Vector3(
            _v2Pos.X + _clusterDesc.Pos.X,
            _clusterDesc.AverageHeight + _height,
            _v2Pos.Y + _clusterDesc.Pos.Z);
    }


    /**
     * Reality has changed, update the current state from the given numbers.
     */
    public void NavigatorSetTransformation(Vector3 vPos3, Quaternion qRotation)
    {
        vPos3 -= _clusterDesc.Pos;
        _v2Pos = new Vector2(vPos3.X, vPos3.Z);
        _v2LastSpeed = Vector3.Transform(new Vector3(0f, -1f, 0f), qRotation);
        _vu2LastDirection = new Vector2( _v2LastSpeed.X, _v2LastSpeed.Z);
    }

    
    public void SetupFrom(JsonElement je)
    {
        //_qPrevRotation = ToQuaternion(jo["sno"]["prevRotation"]);
    }

    
    public void SaveTo(ref JsonObject jo)
    {
        JsonObject joNav = new JsonObject();
        joNav.Add("speed", _speed );
        joNav.Add("height", _height );
        joNav.Add("v2Pos", From(_v2Pos) );
        jo.Add("nav", joNav);
    }
    

    public StreetNavigationController(
        ClusterDesc clusterDesc0,
        StreetPoint startPoint0,
        int seed = 0
    )
    {
        _rnd = new builtin.tools.RandomSource($"{clusterDesc0.Name}+{startPoint0.Pos}+{seed}");
        _clusterDesc = clusterDesc0;
        _startPoint = startPoint0;
        _v2Pos = _startPoint.Pos;
        _targetPoint = null;
        _effectiveSpeed = _speed;

        _enumPath = new RandomPathEnumerator(_rnd, null, _startPoint);
        
        _vu2LastDirection = new Vector2(1f, 0f);
        _v2LastSpeed = new Vector3(1f, 0f, 0f);

        _speed = 2.7f * 15f;

        // TXWTODO: Offload this.
        NavigatorBehave(1f / 60f);
    }
}
