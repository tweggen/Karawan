using System;
using System.Numerics;
using System.Collections.Generic;
using engine.behave;
using engine.world;
using engine.streets;
using static engine.Logger;
using static builtin.Workarounds;

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
}


public class StreetNavigationController : INavigator
{
    private builtin.tools.RandomSource _rnd;

    /*
     * Coordinates are relative to cluster.
     */
    private Vector2 _vPos2;

    /**
     * Meters per second.
     */
    private float _speed = 30f * 3.6f;

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
     * Contains the last directeion of the character, the length
     * represents the meter per second.
     */
    private Vector3 _lastSpeed;

    /**
     * Unit vector of the last direction.
     */
    private Vector2 _lastDirection;


    private RandomPathEnumerator _enumPath;

    private DrivingStrokeProperties _nsp;
    private DrivingStrokeCarProperties _ncp;
    

    private void _loadStartPoint()
    {
        _vPos2 = new Vector2(_startPoint.Pos.X, _startPoint.Pos.Y);
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
        while (togo > 0.000001f)
        {

            /*
             * Be sure to have a destination point and compute the vector
             * to it and its length to have the unit vector to it.
             */
            Vector2 vuDest = Vector2.Zero;
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
                Vector2 vCurrentTarget = _ncp.VPerfectTarget;
                var vPerfectDirection = _ncp.VPerfectTarget - _ncp.VPerfectStart;
                var vPerfectDirectionLength = vPerfectDirection.Length();
                var vuPerfectDirection = vPerfectDirection / vPerfectDirectionLength;
                
                Vector2 vPerfectMe = default;
                Vector2 vMeFromStart = _vPos2 - _ncp.VPerfectStart;
                float vMeFromStartLength = vMeFromStart.Length();
                if (vMeFromStartLength > 0.1f)
                {
                    float vPerfectMeScale = 
                        V2Dot(vMeFromStart, vPerfectDirection)
                        / vPerfectDirectionLength;

                    /*
                     * If we already did overshoot, pick the next street point.
                     */
                    if (vPerfectMeScale >= vPerfectDirectionLength)
                    {
                        dist = 0;
                        break;
                    }
                    vPerfectMe = _ncp.VPerfectStart + vuPerfectDirection * vPerfectMeScale;

                    Vector2 vOff = (vPerfectMe - _vPos2);
                    float offLength = vOff.Length();
                    if (offLength > 1f)
                    {
                        vCurrentTarget = vPerfectMe + vuPerfectDirection/2f;
                    }
                    else
                    {
                        vCurrentTarget = _ncp.VPerfectTarget;
                    }
                }

                var vDest = vCurrentTarget - _vPos2;        
                vuDest = V2Normalize(vDest);

                {
                    /*
                     * Derive the current direction from the computed actual target.
                     */
                    var vActualDelta = vCurrentTarget - _vPos2;
                    float actualDist2 = vActualDelta.LengthSquared();
                    if (actualDist2 > 0.0025f)
                    {
                        _lastDirection = vuDest;
                        _lastSpeed = new Vector3(vuDest.X * _speed, 0f, vuDest.Y * _speed);
                    }
                }

                {
                    /*
                     * Now check, how far it is from me to the perfect target
                     */
                    var vPerfectDelta = _ncp.VPerfectTarget - _vPos2;
                    float perfectDist2 = vPerfectDelta.LengthSquared();
                    if (perfectDist2 > 0.0025f)
                    {
                        /*
                         * Yes, there's still a way to go.
                         * However, return only what we can apply in the given direction.
                         */
                        dist = vDest.Length();
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
            var gonow = togo;

            /*
             * Did we reach the end of the stroke? Then load the next.
             */
            if (gonow > dist)
            {
                gonow = dist;
                togo -= gonow;
                _startPoint = _targetPoint;
                _targetPoint = null;
                
                continue;
            }

            togo -= gonow;
            _vPos2 += vuDest * gonow;
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
        var vForward = _lastSpeed;
        Matrix4x4 rot = Matrix4x4.CreateWorld(new Vector3(0f, 0f, 0f), vForward, vYAxis);
        orientation = Quaternion.CreateFromRotationMatrix(rot);
        position =new Vector3(
            _vPos2.X + _clusterDesc.Pos.X,
            _clusterDesc.AverageHeight + _height,
            _vPos2.Y + _clusterDesc.Pos.Z);
    }


    /**
     * Reality has changed, update the current state from the given numbers.
     */
    public void NavigatorSetTransformation(Vector3 vPos3, Quaternion qRotation)
    {
        vPos3 -= _clusterDesc.Pos;
        _vPos2 = new Vector2(vPos3.X, vPos3.Z);
        _lastSpeed = Vector3.Transform(new Vector3(0f, -1f, 0f), qRotation);
        _lastDirection = new Vector2( _lastSpeed.X, _lastSpeed.Z);
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
        _targetPoint = null;

        _enumPath = new RandomPathEnumerator(_rnd, null, _startPoint);
        
        _lastDirection = new Vector2(1f, 0f);
        _lastSpeed = new Vector3(1f, 0f, 0f);

        _speed = 2.7f * 15f;

        _loadStartPoint();

        // TXWTODO: Offload this.
        NavigatorBehave(1f / 60f);
    }
}
