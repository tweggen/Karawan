using BepuPhysics;
using BepuUtilities;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace engine.physics
{
    //Note that the engine does not require any particular form of gravity- it, like all the contact callbacks, is managed by a callback.
    public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        private Engine _engine;
        public Vector3 Gravity;
        private Vector3Wide gravityWideDt;

        // TXWTODO: Move this into object properties
        private Vector3Wide angularDampingDt;
        private Vector3Wide linearDampingDt;
        // TXWTODO: End of object properties.
        
        private float _dt = 0.1f;
        
        public readonly bool AllowSubstepsForUnconstrainedBodies => false;
        public readonly bool IntegrateVelocityForKinematics => false;

        /// <summary>
        /// Performs any required initialization logic after the Simulation instance has been constructed.
        /// </summary>
        /// <param name="simulation">Simulation that owns these callbacks.</param>
        public void Initialize(Simulation simulation)
        {
            //In this demo, we don't need to initialize anything.
            //If you had a simulation with per body gravity stored in a CollidableProperty<T> or something similar, having the simulation provided in a callback can be helpful.
        }

        /// <summary>
        /// Gets how the pose integrator should handle angular velocity integration.
        /// </summary>
        public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving; //Don't care about fidelity in this demo!

        public PoseIntegratorCallbacks(Engine engine, Vector3 gravity) : this()
        {
            _engine = engine;
            Gravity = gravity;
        }

        /// <summary>
        /// Called prior to integrating the simulation's active bodies. When used with a substepping timestepper, this could be called multiple times per frame with different time step values.
        /// </summary>
        /// <param name="dt">Current time step duration.</param>
        public void PrepareForIntegration(float dt)
        {
            /*
             * Remember the time period
             */
            _dt = dt;

            //No reason to recalculate gravity * dt for every body; just cache it ahead of time.
            gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
            angularDampingDt = Vector3Wide.Broadcast(new Vector3(0.95f, 0.95f, 0.95f));
            linearDampingDt = Vector3Wide.Broadcast(new Vector3(0.95f, 0.95f, 0.95f));
        }

        /// <summary>
        /// Callback called for each active body within the simulation during body integration.
        /// </summary>
        /// <param name="bodyIndex">Index of the body being visited.</param>
        /// <param name="pose">Body's current pose.</param>
        /// <param name="localInertia">Body's current local inertia.</param>
        /// <param name="workerIndex">Index of the worker thread processing this body.</param>
        /// <param name="velocity">Reference to the body's current velocity to integrate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        {
            // velocity.Angular *= angularDampingDt;
            // velocity.Linear *= linearDampingDt;
            velocity.Linear += gravityWideDt;
        }

    }
}
