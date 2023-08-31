using DefaultEcs;
using DefaultEcs.Internal;
using DefaultEcs.Resource;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using engine;
using engine.draw;
using static engine.Logger;

namespace nogame.modules.playerhover
{
    internal class WASDPhysics : AModule, IInputPart
    {
        public static float MY_Z_ORDER = 25f;
        private engine.Engine _engine;
        private DefaultEcs.Entity _eTarget;
        private engine.transform.API _aTransform;

        private BepuPhysics.BodyReference _prefTarget;

        private float _massShip;

        private DefaultEcs.Entity _ePhysDisplay;

        private readonly float LinearThrust = 180f;
        private readonly float AngularThrust = 20.0f;
        private readonly float MaxLinearVelocity = 50f;
        private readonly float MaxAngularVelocity = 0.8f;
        private readonly float LevelUpThrust = 16f;
        private readonly float LevelDownThrust = 16f;
        
        private void _onLogicalFrame(object sender, float dt)
        {
            Vector3 vTotalImpulse = new Vector3(0f, 9.81f, 0f);
            
            Vector3 vTotalAngular = new Vector3(0f, 0f, 0f);

            /*
             * Balance height first.
             *
             * Create an impulse appropriate to work against the
             * gravity. Note, that the empty impulse is pre-set with
             * an impulse to accelerate against gravity.
             */
            Vector3 vTargetPos = _prefTarget.Pose.Position;
            Vector3 vTargetVelocity = _prefTarget.Velocity.Linear;
            Vector3 vTargetAngularVelocity = _prefTarget.Velocity.Angular;
            float heightAtTarget = engine.world.MetaGen.Instance().Loader.GetNavigationHeightAt(vTargetPos);
            {
                var properDeltaY = 0;
                var deltaY = vTargetPos.Y - (heightAtTarget+properDeltaY);
                const float threshDiff = 0.01f;

                Vector3 impulse;
                float properVelocity = 0f;
                if ( deltaY < -threshDiff )
                {
                    properVelocity = LevelUpThrust; // 1ms-1 up.
                }
                else if( deltaY > threshDiff )
                {
                    properVelocity = -LevelDownThrust; // 1ms-1 down.
                }
                float deltaVelocity = properVelocity - vTargetVelocity.Y;
                float fireRate = deltaVelocity;
                impulse = new Vector3(0f, fireRate, 0f);
                vTotalImpulse += impulse;
            }

            /*
             * Apply controls
             */
            var cTransform3 = _eTarget.Get<engine.transform.components.Transform3>();
            var cToParent = _eTarget.Get<engine.transform.components.Transform3ToParent>();

            /*
             * We cheat a bit, reading the matrix for the direction matrix,
             * applying the position change to the transform parameters,
             * applying rotation directly to the transform parameters.
             */
            var vFront = new Vector3(-cToParent.Matrix.M31, -cToParent.Matrix.M32, -cToParent.Matrix.M33);
            var vUp = new Vector3(cToParent.Matrix.M21, cToParent.Matrix.M22, cToParent.Matrix.M23);
            var vRight = new Vector3(cToParent.Matrix.M11, cToParent.Matrix.M12, cToParent.Matrix.M13);

            if (MY_Z_ORDER == Implementations.Get<InputEventPipeline>().GetFrontZ())
            {
                Implementations.Get<builtin.controllers.InputController>().GetControllerState(out var controllerState);

                var frontMotion = controllerState.FrontMotion;
                var upMotion = controllerState.UpMotion;
                var turnMotion = controllerState.TurnRight - controllerState.TurnLeft;

                if (frontMotion != 0f)
                {
                    // The acceleration looks wrong when combined with rotation.
                    vTotalImpulse += LinearThrust * vFront * frontMotion / 256f;

                    /*
                     * Move nose down when accelerating and vice versa.
                     */
                    vTotalAngular += vRight * (-AngularThrust * frontMotion / 4096f);
                }

                if (upMotion != 0f)
                {
                    vTotalImpulse += LinearThrust * vUp * upMotion / 256f;
                }

                if (turnMotion != 0f)
                {
                    /*
                     * gently lean to the right iof turning right.
                     */
                    vTotalAngular += vFront * (AngularThrust * turnMotion / 1024f);

                    /*
                     * And finally turn
                     */
                    vTotalAngular += new Vector3(0f, AngularThrust * -turnMotion / 256f, 0f);

                }
            }

            /*
             * Now apply a damping on velocity, i.e. computing linear and angular impulses
             * proportional to the velocity
             */
            // vTotalImpulse += vTargetVelocity * -0.8f;
            // vTotalAngular += vTargetAngularVelocity * -0.8f;

            /*
             * Also, try to rotate me back to horizontal plane.
             * Look, where my local up vector points and find the shortes rotation to get it back up.
             * Do that by adding some angular impulse, making it swing in the end.
             */

            /*
             * We have two vectors, the perfect up vector and the real up vector.
             * If the real up vector only differs in minimal fractions from the perfect 
             * one, we ignore the difference.
             * 
             * If there is a larger deviation, we apply an angular impulse. The axis for 
             * this impulse is the cross product of both of the up vectors.
             */
            Vector3 vSpinTopAxis = Vector3.Cross(vUp, new Vector3(0f, 1f, 0f));
            if (vSpinTopAxis.Length() > 0.01f)
            {
                vTotalAngular += 10f*vSpinTopAxis;
            }


            /*
             * Finally, clip the height with the ground.
             * To increase environment interaction, try to tilt the ship accordingly.
             */
            if( vTargetPos.Y < heightAtTarget )
            {
                /*
                 * Read the height at our front, or kind of at the front.
                 * Give us an impulse accordingly.
                 */
                float heightAtFront = engine.world.MetaGen.Instance().Loader.GetNavigationHeightAt(
                    vTargetPos+2f*vFront);
                if (heightAtFront > heightAtTarget)
                {
                    //heightAtTarget = heightAtFront;
                    vTotalAngular += vRight * 10f*(heightAtFront-heightAtTarget);
                }

                vTargetPos.Y = heightAtTarget;
                _prefTarget.Pose.Position = vTargetPos;
                vTotalImpulse += new Vector3(0f, 10f, 0f);
            }

            var mass = 500f;
            if (vTotalImpulse.Length() > mass)
            {
                Trace($"Too fast: {vTotalImpulse.Length()}.");
            }
            if (vTotalAngular.Length() > mass)
            {
                Trace($"Too fast: {vTotalAngular.Length()}.");
            }

            
            /*
             * TXWTODO: Workaround to limit top speed.
             */
            if (vTargetVelocity.Length() > MaxLinearVelocity)
            {
                float vel = vTargetVelocity.Length();
                vTotalImpulse += -(vTargetVelocity * (vel - MaxLinearVelocity) / vel) / dt;
            }
            if (vTargetAngularVelocity.Length() > MaxAngularVelocity)
            {
                float avel = vTargetAngularVelocity.Length();
                vTotalAngular += -(vTargetAngularVelocity * (avel - MaxAngularVelocity) / avel) / dt;
            }

            /*
             * Set current velocity.
             * TXWTODO: (or would that be the previous one?)
             */
            _eTarget.Set(new engine.joyce.components.Motion(_prefTarget.Velocity.Linear));
            
            _prefTarget.ApplyImpulse(vTotalImpulse * dt * _massShip, new Vector3(0f, 0f, 0f));
            _prefTarget.ApplyAngularImpulse(vTotalAngular * dt * _massShip);

            _ePhysDisplay.Set(new engine.draw.components.OSDText(
                new Vector2(20f, 370f),
                new Vector2(400, 54),
                $"@{vTargetPos}"
                //+$"v: {vTargetVelocity.Length()}, "
                //+$"a: {vTargetAngularVelocity.Length()}"
                ,
                9,
                0xff22aaee,
                0x00000000,
                HAlign.Left
            ));
            
        }


        public void ModuleDeactivate()
        {
            _engine.OnLogicalFrame -= _onLogicalFrame;
            Implementations.Get<InputEventPipeline>().RemoveInputPart(this);

        }


        public void ModuleActivate(engine.Engine engine)
        {
            _engine = engine;
            _aTransform = Implementations.Get<engine.transform.API>();
            _ePhysDisplay = _engine.CreateEntity("OsdPhysDisplay");

            _prefTarget = _eTarget.Get<engine.physics.components.Body>().Reference;
            
            Implementations.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
            _engine.OnLogicalFrame += _onLogicalFrame;
        }

        
        public void InputPartOnInputEvent(engine.news.Event ev)
        {
        }


        public void Dispose()
        {
        }
        

        public WASDPhysics(
            in DefaultEcs.Entity eTarget,
            in float massShip)
        {
            _eTarget = eTarget;
            _massShip = massShip;
        }
    }
}
