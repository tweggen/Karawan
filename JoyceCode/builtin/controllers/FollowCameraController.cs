using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using engine;
using engine.joyce.components;
using engine.news;
using engine.physics;
using static engine.Logger;

namespace builtin.controllers;

public class FollowCameraController : AController, IInputPart
{
    private object _lo = new();

    DefaultEcs.Entity _eTarget;
    public DefaultEcs.Entity Target
    {
        get => _eTarget;
        set => _eTarget = value;
    }
    
    DefaultEcs.Entity _eCarrot;
    public DefaultEcs.Entity Carrot
    {
        get => _eCarrot;
        set => _eCarrot = value;
    }   

    private Vector3 _vPreviousCameraPosition;
    private Vector3 _vPreviousCameraOffset;
    private Quaternion _qLastPerfectCameraRotation = Quaternion.Identity;
    private Vector2 _vMouseOffseting;
    private Vector2 _vStickOffset;
    private Vector2 _vMouseMove;
    float _lastMouseMove = 0f;
    private bool _firstFrame = true;
    private bool _isInputEnabled = true;
    private bool _mouseOffsetsCamera = false;

    private float _previousZoomDistance = 33f;
    
    public static string EventTypeRequestMode = "builtin.controllers.followCameraController.RequestMode";

    public float MY_Z_ORDER { get; set; } = 21f;
    

    public float ORIENTATION_SLERP_AMOUNT { get; set; } = 0.07f;
    public float CAMERA_BACK_TO_ORIENTATION_SLERP_AMOUNT { get; set; } = 0.01f;
    public float ZOOM_SLERP_AMOUNT { get; set; } = 0.2f;
    public float ZOOM_MIN_DISTANCE { get; set; } = 5f;
    public float ZOOM_MAX_DISTANCE { get; set; } = 133f;
    public float ZOOM_STEP_FRACTION { get; set; } = 60f;
    public float MOUSE_RELATIVE_AMOUNT { get; set; } = 0.03f;
    public float MOUSE_RETURN_SLERP { get; set; } = 0.98f;
    public float MOUSE_INACTIVE_BEFORE_RETURN_TIMEOUT { get; set; } = 1.6f;
    public float FOLLOW_AFTER_MOVING_FOR { get; set; } = 0.4f;
    public float ADJUST_AFTER_STOPPING_FOR { get; set; } = 0.5f;
    public float CONSIDER_ORIENTATION_WHILE_DRIVING { get; set; } = 0.9f;

    public float STICK_VERTICAL_SENSITIVITY { get; set; } = 80f;
    public float STICK_HORIZONTAL_SENSITIVITY { get; set; } = 180f;

    public float YAngleDefault { get; set; } = 2f;
    
    public float CameraRadius { get; set; } = 0.5f;
    public float CameraMass { get; set; } = 0.5f;

    public float CameraDistance { get; set; } = 0.25f;
    public float CameraMinDistance { get; set; } = 2.0f;

    public string CameraPhysicsName { get; set; } = "CameraPhysics";

    private BodyReference _prefCameraBall;
    private BodyReference _prefPlayer;

    private float _dtMoving = 0f;
    private float _dtStopped = 0f;

    /**
     * The angles that additionally are applied because of mouse movement
     */
    private Vector2 _vMouseAnglesOffseting;
    
    private static Vector3 _vuUp = new(0f, 1f, 0f);
    private Quaternion _qPreviousCameraFront = Quaternion.Identity;

    private enum CameraAngle
    {
        MiddleBetweenOrientationAndMoving,
        Orientation,
        PreviousFront,
        SlerpBackToOrientation
    };

    private CameraAngle _cameraAngle;
    private CameraAngle _previousCameraAngle;


    private void _buildPhysics()
    {
        lock (_engine.Simulation)
        {
            Vector3 posShip = _prefPlayer.Pose.Position;
            Quaternion rotShip = _prefPlayer.Pose.Orientation;

            var shape = new TypedIndex()
            {
                Packed = (uint)engine.physics.actions.CreateSphereShape.Execute(
                    _engine.PLog, _engine.Simulation, CameraMass, out var pbody
                )
            };  
            var inertia = pbody.ComputeInertia(CameraMass);
            var po = new engine.physics.Object(_engine, _eTarget, inertia, shape,
                posShip, rotShip).AddContactListener();
            _prefCameraBall = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
            _eTarget.Set(new engine.physics.components.Body(po, _prefCameraBall));
        }
    }


    private void _destroyPhysics()
    {
        _eTarget.Remove<engine.physics.components.Body>();
    }


    /**
     * We maintain an internal zoom state ranging from zero to one.
     */
    private float _zoomState = 0.15f;

    private Vector3 _vCarrotVelocity;

    private float _zoomDistance()
    {
        if (_isInputEnabled)
        {
            /*
             * Compute a distance from the zoom state.
             * TXWTODO: Remove the magic numbers.
             */
            float zoomFactor = _zoomState * _zoomState;
            float zoomDistance = ZOOM_MIN_DISTANCE + zoomFactor * (ZOOM_MAX_DISTANCE-ZOOM_MIN_DISTANCE);
            zoomDistance = (1f - ZOOM_SLERP_AMOUNT) * _previousZoomDistance + zoomDistance * ZOOM_SLERP_AMOUNT;
            return zoomDistance;
        }
        else
        {
            return _previousZoomDistance;
        }
    }


    /**
     * Read the carrot's velocity.
     *
     * Entities with physics may have exact velocity attached. However, for optimization, physics
     * only are maintained for objects close to the player. For camera targets without valid physics,
     * we might want to consult different sources of information.
     */
    private void _readEntityVelocity(out Vector3 vVelocity)
    {
        bool haveVelocity = false;
        vVelocity = Vector3.Zero;
        lock (_engine.Simulation)
        {
            if (_prefPlayer.Exists)
            {
                if (_prefPlayer.Pose.Position != Vector3.Zero
                    && _prefPlayer.Pose.Orientation != Quaternion.Identity)
                {
                    vVelocity = _prefPlayer.Velocity.Linear;
                    _vCarrotVelocity = vVelocity;
                    haveVelocity = true;
                }
            }
        }

        if (!haveVelocity)
        {
            if (_eCarrot.IsAlive && _eCarrot.Has<engine.joyce.components.Motion>())
            {
                vVelocity = _eCarrot.Get<engine.joyce.components.Motion>().Velocity;
                _vCarrotVelocity = vVelocity;
                haveVelocity = true;
            }
        }
    }


    private void _computePerfectCameraOrientationMouseOffsets(
        float dt,
        in Matrix4x4 cToParentMatrix,
        out Quaternion qPerfectCameraOrientation)
    {
        /*
         * Count the time we are moving.
         */
        _dtMoving += dt;
        _dtStopped += dt;


        /*
         * Prepare by computing the front orientation of the carrot matrix.
         */
        float l;
        Vector3 vOrientationFront = new Vector3(-cToParentMatrix.M31, 0f, -cToParentMatrix.M33);
        {
            l = vOrientationFront.Length();
            if (l < 0.3f)
            {
                vOrientationFront = new(0f, 0f, -1f);
            }
            else
            {
                vOrientationFront /= l;
            }
        }

        /*
         * First compute the desired direction of the camera. The camera is position up
         * and backward with respect to the direction to the subject (the carrot).
         *
         * If we have physics, we take the velocity of the carrot as direction.
         *
         * If the velocity is too small, we do not change the direction.
         *
         * If we do not have physics, we compute it from the orientation of the target.
         *
         * If the direction has not been initialised before, we derive the direction from
         * the orientation of the target.
         *
         * The "right" vector is computed by assuming the up vector is the y axis.
         * The correct up vector is computed from the front and from the right vector.
         */

        bool isBackward = false;

        //Vector3 vuMovingToFront = default;
        Quaternion qMovingToFront = default;
        //Vector3 vuPreviousFront = default;
        Quaternion qPreviousFront = default;
        //Vector3 vuOrientationFront = default;
        Quaternion qOrientationFront = default;


        /*
         * If we are a physics object that does have linear velocity, we use the
         * linear velocity as a strong motivation to align our view.
         */
        {
            _readEntityVelocity(out var vVelocity);
            Vector2 vVelocity2 = new(vVelocity.X, vVelocity.Z);
            float velocity = vVelocity2.Length();
            if (velocity > 5f / 3.6f)
            {
                var vFront2 = vVelocity2 / velocity;
                Vector3 vuMovingToFront = new Vector3(vFront2.X, 0f, vFront2.Y);

                /*
                 * Also check, if we are riding forward or backward
                 */
                float dir = Vector3.Dot(vuMovingToFront, vOrientationFront);
                if (dir < 0) isBackward = true;

                if (isBackward)
                {
                    vuMovingToFront = -vuMovingToFront;
                }

                qMovingToFront = engine.geom.Camera.CreateQuaternionFromPlaneFront(vuMovingToFront);
                _dtStopped = 0f;
            }
            else
            {
                _dtMoving = 0f;
            }
        }

        qPreviousFront = _qPreviousCameraFront;

        /*
         * Fallback: Use the front according to the transformation matrix.
         */
        {
            var vuOrientationFront = new Vector3(-cToParentMatrix.M31, 0f, -cToParentMatrix.M33);
            l = vuOrientationFront.Length();
            if (l < 0.3f)
            {
                vuOrientationFront = new(0f, 0f, -1f);
            }
            else
            {
                vuOrientationFront /= vuOrientationFront.Length();
            }

            qOrientationFront = engine.geom.Camera.CreateQuaternionFromPlaneFront(vuOrientationFront);
        }


        /*
         * Now let's use the information we have to compute the desired target camera position.
         *
         * Basically:
         * - if we are moving we want to quickly turn to the object and follow it.
         *   However, we want to wait for a short time for the movement to settle.
         * - if we are standing still we want to keep the perspective. If we are standing for longer than
         *   two seconds, we slowly adapt the perspective to the orientation of the carrot.
         *
         * Note, that physics will pull the camera pretty fast and linear to the desired position, so
         * any smooth camera movements should be implemented here.
         */
        Quaternion qFront;
        if (_dtMoving > FOLLOW_AFTER_MOVING_FOR)
        {
            if (qMovingToFront != default)
            {
                _cameraAngle = CameraAngle.MiddleBetweenOrientationAndMoving;
                qFront = Quaternion.Slerp(qMovingToFront, qOrientationFront, CONSIDER_ORIENTATION_WHILE_DRIVING);
            }
            else
            {
                _cameraAngle = CameraAngle.Orientation;
                qFront = qOrientationFront;
            }
        }
        else
        {
            if (qPreviousFront != default)
            {
                if (_dtStopped < ADJUST_AFTER_STOPPING_FOR)
                {
                    _cameraAngle = CameraAngle.PreviousFront;
                    qFront = qPreviousFront;
                }
                else
                {
                    _cameraAngle = CameraAngle.SlerpBackToOrientation;
                    qFront = Quaternion.Slerp(qPreviousFront, qOrientationFront,
                        CAMERA_BACK_TO_ORIENTATION_SLERP_AMOUNT);
                }
            }
            else
            {
                _cameraAngle = CameraAngle.Orientation;
                qFront = qOrientationFront;
            }
        }

        qPerfectCameraOrientation = qFront;
    }


    private void _computePerfectCameraOrientationMouseControls(
        float dt,
        in Matrix4x4 cToParentMatrix,
        out Quaternion qPerfectCameraOrientation)
    {
        _cameraAngle = CameraAngle.Orientation;
        Quaternion qNewFront = _qPreviousCameraFront;
        if (_vMouseMove.Y != 0)
        {
            float mouseAngleOrientation = -(_vMouseMove.X) * (float)Math.PI / 180f;
            //Trace($"_vMouseMove.Y = {_vMouseMove.X}");

            var rotRight = Quaternion.CreateFromAxisAngle(new Vector3(0f, 1f, 0f), mouseAngleOrientation);
            qNewFront = Quaternion.Concatenate(qNewFront, rotRight);
        }

        qPerfectCameraOrientation = qNewFront;
    }
    
    
    private void _computePerfectCameraOrientation(
        float dt,
        in Matrix4x4 cToParentMatrix,
        out Quaternion qPerfectCameraOrientation)
    {
        if (_mouseOffsetsCamera)
        {
            _computePerfectCameraOrientationMouseOffsets(dt, cToParentMatrix, out qPerfectCameraOrientation);
        }
        else
        {
            _computePerfectCameraOrientationMouseControls(dt, cToParentMatrix, out qPerfectCameraOrientation);
        }
    }
    

    /**
     * Compute the desired camera position.
     *
     * Note, the orientation is included just for historic reasons.
     *
     * The desired position is input to the physics system as a goal to reach.
     * However, buildings, object etc. might obstruct the path so we do not
     * reach it.
     */
    private void _computePlainCameraPos(
        in Vector3 vCarrotPos,
        in Quaternion qFront,
        out Vector3 vPerfectCameraPos,
        out Vector3 vPerfectCameraOffset)
    {

        /*
         * Derive front vector from Quaternion.
         */
        Vector3 vFront = Vector3.Transform(new Vector3(0f, 0f, -1f), qFront);


        if (_mouseOffsetsCamera)
        {
            /*
             * Rotate the front vector by the mouse orientation.
             */
            var rotRight = Quaternion.CreateFromAxisAngle(new Vector3(0f, 1f, 0f), _vMouseAnglesOffseting.Y);
            vFront = Vector3.Transform(vFront, rotRight);
        }

        /*
         * Derive right from front and the Y vector, up is straight up,
         * real front then again from -(richt x up)
         */
        engine.geom.Camera.CreateVectorsFromPlaneFront(vFront,
            out var vPerfectCameraFront, out var vPerfectCameraRight);

        /*
         * Now we have a camera corrdinate system.
         * Front (i.e. -z) facing the carrot, up and right as we want it.
         *
         * Now let's move the camera back to where we want it to be.
         */
        float zoomDistance = _zoomDistance();
        
        /*
         * Set up the vertical camera angle.
         * Our default is dx = 1 , dy = 0.25, which is about 15 degree (from the horizontal).
         */
        float vertAngle = Single.Clamp(_vMouseAnglesOffseting.X,
            -85f * Single.Pi / 180f, 85f * Single.Pi / 180f);
        float dy = Single.Sin(vertAngle);
        float dz = Single.Cos(vertAngle);
        vPerfectCameraOffset = zoomDistance * vPerfectCameraFront * dz - zoomDistance  * _vuUp * dy;

        vPerfectCameraPos = vCarrotPos - vPerfectCameraOffset;

    }


    /**
     * The real camera transformation is defined by the positon of the physical camera object
     * and a direction that we want to determine.
     */
    private void _computeRealCameraDirection(
        in Vector3 vRealCameraPosition,
        in Vector3 vCarrotPos,
        out Quaternion qRealCameraOrientation
    )
    {
        var vRealCameraOffset = vCarrotPos - vRealCameraPosition;
        float l = vRealCameraOffset.Length();
        if (l < 0.5f)
        {
            vRealCameraOffset = _vPreviousCameraOffset;
            l = vRealCameraOffset.Length();
            if (l < 0.5f)
            {
                vRealCameraOffset = new Vector3(0f, 0f, 1f);
            }
            else
            {
                vRealCameraOffset /= l;
            }
        }
        else
        {
            vRealCameraOffset /= l;
        }

        /*
         * Derive
         * - right from front and the Y vector
         * - up from (right x front)
         */
        var vRealCameraRight = Vector3.Cross(vRealCameraOffset, new Vector3(0f, 1f, 0f));
        var vRealCameraUp = Vector3.Cross(vRealCameraRight, vRealCameraOffset);

        /*
         * Compute camera orientation from the coordinate system.
         */
        var qOrientationToCarrot = Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateWorld(
                Vector3.Zero,
                vRealCameraOffset,
                vRealCameraUp)
        );

        //var qOrientation = Quaternion.Slerp(_qLastPerfectCameraRotation, qCarrotOrientation, ORIENTATION_SLERP_AMOUNT);
        //_qLastPerfectCameraRotation = qOrientation;
        qRealCameraOrientation = qOrientationToCarrot;
    }


    private void _computeCameraVelocity(float dt, Vector3 vRealCameraPos)
    {
        /*
         * Compute camera object velocity for audio effects etc.
         */
        if (!_firstFrame)
        {
            var vCameraVelocity = (vRealCameraPos - _vPreviousCameraPosition) / dt;
            _eTarget.Set(new engine.joyce.components.Motion(vCameraVelocity));
        }
    }
    

    /**
     * We have a camera position that is too close to our subject.
     * Go and create another angle that is most likely not obstructed (i.e. from the above).
     */
    private void _computeNotTooClose(Vector3 vClose, out Vector3 vNotTooClose)
    {
        vNotTooClose = vClose;
        vClose.Y += _zoomDistance();
    }


    private void _findClosestView(Vector3 vCarrotPosition, in Vector3 vCameraPos, out Vector3 vVisibleCameraPos)
    {
        SortedDictionary<float, CollisionProperties> mapCollisions = new();
        var aPhysics = I.Get<engine.physics.API>();
        Vector3 vDiff = vCameraPos - vCarrotPosition;
        float l = vDiff.Length();
        float maxLength = vDiff.Length();
        lock (_engine.Simulation)
        {
            aPhysics.RayCastSync(vCarrotPosition, vDiff, 1f,
                (CollidableReference cRef, CollisionProperties props, float t, Vector3 vCollision) =>
                {
                    bool isRelevant = false;
                    switch (cRef.Mobility)
                    {
                        case CollidableMobility.Dynamic:
                            break;
                        case CollidableMobility.Kinematic:
                            break;
                        case CollidableMobility.Static:
                            isRelevant = true;
                            break;
                    }

                    if (isRelevant)
                    {
                        mapCollisions[t] = props;
                    }
                });
        }

        Vector3 vClosestCameraPos = vCameraPos;
        if (mapCollisions.Count > 0)
        {
            float l0 = mapCollisions.Keys.First();

            /*
             * This is the real collision.
             * Look, if our way from the carrot to here still would be
             * obstructed by the floor
             */
            if (engine.elevation.Cache.Instance().ElevationCacheRayCast(
                    vCarrotPosition, vDiff, l, out var lFloor))
            {
                /*
                 * Yes, there is a floor intersection.
                 */
                l0 = lFloor;
            }

            /*
             * Use the intersection point offset by 0.5m for the new camera.
             */
            Vector3 vHit = vCarrotPosition + (l0) * vDiff;
            if (l0 < 2f)
            {
                _computeNotTooClose(vHit, out var vNotTooClose);
                vClosestCameraPos = vNotTooClose;
            }
            else
            {
                vClosestCameraPos = vHit;
            }
        }
        else
        {
            /*
             * This is the real collision.
             * Look, if our way from the carrot to here still would be
             * obstructed by the floor
             */
            if (engine.elevation.Cache.Instance().ElevationCacheRayCast(
                    vCarrotPosition, vDiff, l, out var lFloor))
            {
                /*
                 * Yes, there is a floor intersection.
                 */
                vClosestCameraPos = vCarrotPosition + (lFloor) * vDiff;
            }
            else
            {
                vClosestCameraPos = vCameraPos;
            }
            // TXWTODO: from carrot to this position perform a raycast against the floor. 
        }

        vVisibleCameraPos = vClosestCameraPos;
    }


    /**
     * Actually setup the camera to the position and direction requested.
     */
    private void _setRaycastCameraBody(in Vector3 vCarrotPos, in Vector3 vRealCameraPosition,
        in Vector3 vPerfectCameraPos, in Quaternion qUserCameraOrientation)
    {
        Vector3 vFinalCameraPos = vPerfectCameraPos;
        Vector3 vVisibleCameraPos;
        _findClosestView(vCarrotPos, vFinalCameraPos, out vVisibleCameraPos);
        //vVisibleCameraPos = vFinalCameraPos;
        lock (_engine.Simulation)
        {
            _prefCameraBall.Pose.Orientation = qUserCameraOrientation;
            _prefCameraBall.Pose.Position = vVisibleCameraPos;
            _prefCameraBall.Velocity.Linear = Vector3.Zero;
            _prefCameraBall.Velocity.Angular = Vector3.Zero;
        }
        /*
         * Also modify the camera angle with the speed.
         */
        if (_eTarget.Has<Camera3>())
        {
            float relSpeed = (Single.Min(_vCarrotVelocity.Length(), 100f) / 100f);
            _eTarget.Get<Camera3>().Angle = 45f + relSpeed * relSpeed * 45f; 
        }
    }


    /**
     * Apply the idea of the perfect camera position to the real camera.
     */
    private void _applyRealCamera(in Vector3 vCarrotPos, in Vector3 vRealCameraPosition, in Vector3 vPerfectCameraPos,
        in Quaternion qUserCameraOrientation)
    {
        //_servePhysicalCameraBody(vRealCameraPosition, vPerfectCameraPos, qUserCameraOrientation);
        _setRaycastCameraBody(vCarrotPos, vRealCameraPosition, vPerfectCameraPos, qUserCameraOrientation);
    }


    private void _transitionFromRealCameraPos(
        in Vector3 vPerfectCameraPos, in Vector3 vLastRealCameraPos, out Vector3 vDesiredCameraPos)
    {
        // TXWTODO: Slerp or something
        vDesiredCameraPos = 0.9f * vLastRealCameraPos + 0.1f * vPerfectCameraPos;
        // vDesiredCameraPos = vPerfectCameraPos;
    }


    protected override void OnLogicalFrame(object sender, float dt)
    {
        if (!_eCarrot.Has<engine.joyce.components.Transform3ToWorld>()
            || !_eCarrot.Has<engine.joyce.components.Transform3>())
        {
            return;
        }
        
        /*
         * We allow the user to move the cam.
         */
        if (_isInputEnabled)
        {
            engine.I.Get<builtin.controllers.InputController>().GetMouseMove(out _vMouseMove);
            engine.I.Get<builtin.controllers.InputController>().GetStickOffset(out _vStickOffset);
        }
        else
        {
            _vMouseMove = Vector2.Zero;
        }

        _vMouseAnglesOffseting.X = (_vStickOffset.Y*STICK_VERTICAL_SENSITIVITY + _vMouseOffseting.Y + YAngleDefault) * (float)Math.PI / 180f;
        _vMouseAnglesOffseting.Y = -(_vStickOffset.X*STICK_HORIZONTAL_SENSITIVITY + _vMouseOffseting.X) * (float)Math.PI / 180f;


        var cToParent = _eCarrot.Get<engine.joyce.components.Transform3ToWorld>();
        
        /*
         * Look slightly above the car.
         */
        var vCarrotPos = cToParent.Matrix.Translation + Vector3.UnitY*1f;

        /*
         * First compute the desired camera position (vPerfectCameraPos).
         * Then transition the real camera to the perfect pos (previous pos + perfect pos => desiredCameraPos)
         * The find a visible, unobstructed place, starting with the desired pos.
         */
        _computePerfectCameraOrientation(dt, cToParent.Matrix, out var qFront);

        /*
         * smoothen camera movement.
         */
        qFront = Quaternion.Slerp(_qPreviousCameraFront, qFront, 0.07f);

        /*
         * Derive the perfect camera position from the angle.
         */
        _computePlainCameraPos(
            vCarrotPos,
            qFront,
            out var vPerfectCameraPos,
            out var vPerfectCameraOffset);

        Vector3 vDesiredCameraPos;
        if (false && _vPreviousCameraPosition != default)
        {
            _transitionFromRealCameraPos(vPerfectCameraPos, _vPreviousCameraPosition, out vDesiredCameraPos);
        }
        else
        {
            vDesiredCameraPos = vPerfectCameraPos;
        }

        _findClosestView(vCarrotPos, vDesiredCameraPos, out var vVisibleCameraPos);

        var vRealCameraPosition = vVisibleCameraPos;

        _computeRealCameraDirection(vRealCameraPosition, vCarrotPos, out var qRealCameraOrientation);

        /*
         * Compute real camera object velocity for audio.
         */
        _computeCameraVelocity(dt, vRealCameraPosition);

        /*
         * Some up the relative mouse movement.
         */
        _vMouseOffseting += _vMouseMove * MOUSE_RELATIVE_AMOUNT;
        if (_vMouseMove.X == 0f && _vMouseMove.Y == 0f)
        {
            _lastMouseMove += dt;
        }
        else
        {
            _lastMouseMove = 0f;
        }


        //var vUserCameraFront = 
        //    vRealCameraFront * (float)Math.Cos(-angleY)
        //    + vRealCameraRight * (float)Math.Sin(-angleY);

        _vPreviousCameraOffset = vPerfectCameraOffset;
        _qPreviousCameraFront = qFront;
        _vPreviousCameraPosition = vRealCameraPosition;
        _previousZoomDistance = _zoomDistance();
        _previousCameraAngle = _cameraAngle;

        /*
         * Apply relative mouse movement
         */
        var qUserCameraOrientation = qRealCameraOrientation;
    
        /*
         * Now we have computed the position we want to target the camera object to.
         */
        _applyRealCamera(vCarrotPos, vRealCameraPosition, vPerfectCameraPos, qUserCameraOrientation);

        /*
         * Look, how to deal with the mouse offset. If the camera quaternion is reasonably identical
         * to the object
         */

        if (_mouseOffsetsCamera)
        {
            /*
             * ramp down mouse offset after 1.5s of inactivity.
             */
            if (_lastMouseMove > MOUSE_INACTIVE_BEFORE_RETURN_TIMEOUT)
            {
                _vMouseOffseting *= MOUSE_RETURN_SLERP;
            }
        }
        else
        {
        }

        _firstFrame = false;
    }


    public void ForcePreviousZoomDistance(float dist)
    {
        _previousZoomDistance = dist;
    }


    public void InputPartOnInputEvent(engine.news.Event ev)
    {
        if (ev.Type != Event.INPUT_MOUSE_WHEEL) return;

        ev.IsHandled = true;
        
        /*
         * size y contains the delta.
         */
        float deltaZoomStep = -ev.Position.Y / ZOOM_STEP_FRACTION;
        lock (_lo)
        {
            _zoomState = Single.Clamp(_zoomState + deltaZoomStep, 0f, 1f);
        }
    }


    public void EnableInput(bool isInputEnabled)
    {
        _isInputEnabled = isInputEnabled;
    }


    private void _onRequestMode(Event ev)
    {
        switch (ev.Code)
        {
            default:
            case "MouseOffsetsCamera":
                _mouseOffsetsCamera = true;
                break;
            case "MouseControlsCamera":
                _mouseOffsetsCamera = false;
                break;
        }
        Trace($"Control mode switched to {_mouseOffsetsCamera}");
    }
    
    
    protected override void OnModuleDeactivate()
    {
        I.Get<SubscriptionManager>().Subscribe(EventTypeRequestMode, _onRequestMode);

        I.Get<InputEventPipeline>().RemoveInputPart(this);
        _destroyPhysics();
    }


    protected override void OnModuleActivate()
    {
        Debug.Assert(_eTarget != default);
        Debug.Assert(_eCarrot != default);
        
        if (_eCarrot.Has<engine.physics.components.Body>())
        {
            _prefPlayer = _eCarrot.Get<engine.physics.components.Body>().Reference;
        }
        else if (_eCarrot.Has<engine.physics.components.Body>())
        {
            _prefPlayer = _eCarrot.Get<engine.physics.components.Body>().Reference;
        }
        else
        {
            ErrorThrow<InvalidOperationException>($"Entity {_eCarrot} does not have physics attached.");
            return;
        }


        if (_eTarget.Has<engine.physics.components.Body>())
        {
            ErrorThrow<InvalidOperationException>($"Entity {_eTarget} already has physics attached.");
        }
    
        _zoomState = CameraDistance;
        _buildPhysics();
        I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        
        I.Get<SubscriptionManager>().Subscribe(EventTypeRequestMode, _onRequestMode);
    }
}
