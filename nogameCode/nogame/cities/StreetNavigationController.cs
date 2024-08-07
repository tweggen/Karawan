﻿using System;
using System.Numerics;
using System.Collections.Generic;
using engine;
using engine.behave;
using engine.world;
using engine.streets;
using static engine.Logger;
using static builtin.Workarounds;

namespace nogame.cities;

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
    private Vector3 _lastSpeed;

    /**
     * Unit vector of the last direction.
     */
    private Vector2 _lastDirection;


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
            ErrorThrow("Encountered empty street point.", m => new InvalidOperationException(m));
        }

        var nTries = 0;
        int tookIndex = -1;
        while (true)
        {
            ++nTries;
            var idx = (int)(_rnd.GetFloat() * strokes.Count);
            tookIndex = idx;
            _currentStroke = strokes[idx];
            if (null == _currentStroke)
            {
                throw new InvalidOperationException("CubeCharacter.loadNextPoint(): strokes[{idx}] is null.");
            }

            if (null == _currentStroke.A)
            {
                throw new InvalidOperationException("CubeCharacter.loadNextPoint(): _currentStroke.A is null.");
            }

            if (null == _currentStroke.B)
            {
                throw new InvalidOperationException("CubeCharacter.loadNextPoint(): _currentStroke.B is null.");
            }

            if (_currentStroke.A == _startPoint)
            {
                //_isAB = true;
                _targetPoint = _currentStroke.B;
            }
            else
            {
                //isAB = false;
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
            if (strokes.Count == 1)
            {
                break;
            }

            /*
             * OK, target point is not the previous point amd not the only option.
             */
            if (1 == targetStrokes.Count && _avoidDeadEnds)
            {

                if (nTries < strokes.Count)
                {
                    _targetPoint = null;
                    continue;
                }
            }

            if (_targetPoint != previousPoint)
            {
                break;
            }

        }
        // Trace($"took index {tookIndex}");
    }


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
                if (null == _targetPoint)
                {
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
                Vector2 vStreetTarget = _targetPoint.Pos;
                Vector2 vStreetStart = _startPoint.Pos;
                Vector2 vuStreetDirection = V2Normalize(vStreetTarget - vStreetStart);

                /*
                 * Offset the target one unit towards the start point
                 * and one unit right-hand-side of start to target.
                 * 
                 * Figure out the street lane
                 *
                 * Yes, this navigation is lacking proper curves.
                 */
                float streetWidth = _currentStroke.StreetWidth();
                float rightLane;

                if (streetWidth >= 16f)
                {
                    /*
                     * On larger streets, select lane according to speed.
                     */
                    if (_speed >= (65f / 3.6f))
                    {
                        /*
                         * fast? Drive on the Left hand side.
                         */
                        rightLane = streetWidth / 2f - 5f;
                    }
                    else
                    {
                        /*
                         * Slow? Drive on the right hand side.
                         */
                        rightLane = streetWidth / 2f - 2f;
                    }
                }
                else
                {
                    /*
                     * On small streets, just drive on the right.
                     */
                    rightLane = streetWidth / 2f - 2f;
                }
                
                var vLaneOffset = rightLane * new Vector2(-vuStreetDirection.Y, vuStreetDirection.X); 
                
                /*
                 * This is where we actually are heading to.
                 */
                var vPerfectTarget = vStreetTarget
                              /*
                               * Not quite to the center of the junction
                               */
                              - vuStreetDirection * streetWidth / 2f
                              /*
                               * And up to the right lane.
                               */
                              + vLaneOffset;
                var vPerfectStart = vStreetStart + vLaneOffset;

                /*
                 * Now compute the actual target for the current iteration.
                 */
                Vector2 vCurrentTarget = vPerfectTarget;
                var vPerfectDirection = vPerfectTarget - vPerfectStart;
                var vPerfectDirectionLength = vPerfectDirection.Length();
                var vuPerfectDirection = vPerfectDirection / vPerfectDirectionLength;
                
                Vector2 vPerfectMe = default;
                Vector2 vMeFromStart = _vPos2 - vPerfectStart;
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
                    vPerfectMe = vPerfectStart + vuPerfectDirection * vPerfectMeScale;

                    Vector2 vOff = (vPerfectMe - _vPos2);
                    float offLength = vOff.Length();
                    if (offLength > 1f)
                    {
                        vCurrentTarget = vPerfectMe + vuPerfectDirection/2f;
                    }
                    else
                    {
                        vCurrentTarget = vPerfectTarget;
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
                    var vPerfectDelta = vPerfectTarget - _vPos2;
                    float perfectDist2 = vPerfectDelta.LengthSquared();
                    if (perfectDist2 > 0.0025f)
                    {
                        /*
                         * Yes, there's still a way to go.
                         * However, return only what we can apply in the given direction.
                         */
                        dist = vDest.Length();
                        //dist = Single.Sqrt(perfectDist2);
                        break;
                    }
                }

                /*
                 * No, we do not have a proper way to go, look for the next street point.
                 */

                _prevStart = _startPoint;
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
                _prevStart = _startPoint;
                _startPoint = _targetPoint;
                _targetPoint = null;
                // loadStartPoint();
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


    public bool AvoidDeadEnds
    {
        get => _avoidDeadEnds;
        set => _avoidDeadEnds = value;
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
        _prevStart = null;
        _lastDirection = new Vector2(1f, 0f);
        _lastSpeed = new Vector3(1f, 0f, 0f);
        _avoidDeadEnds = false;

        _speed = 2.7f * 15f;

        _loadStartPoint();

        // TXWTODO: Offload this.
        NavigatorBehave(1f / 60f);
    }
}
