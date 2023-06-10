﻿using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Numerics;

namespace engine.physics
{
    public interface IContactEventHandler
    {
        void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            in Vector3 contactOffset, in Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : struct, IContactManifold<TManifold>;
    }

    //The simulation has a variety of extension points that must be defined. 
    //The demos tend to reuse a few types like the DemoNarrowPhaseCallbacks, but this demo will provide its own (super simple) versions.
    //If you're wondering why the callbacks are interface implementing structs rather than classes or events, it's because 
    //the compiler can specialize the implementation using the compile time type information. That avoids dispatch overhead associated
    //with delegates or virtual dispatch and allows inlining, which is valuable for extremely high frequency logic like contact callbacks.
    internal unsafe struct NarrowPhaseCallbacks<TEventHandler> : INarrowPhaseCallbacks where TEventHandler : IContactEventHandler
    {
        private Engine _engine;
        private ContactEvents<TEventHandler> _events;

        public NarrowPhaseCallbacks(Engine engine, ContactEvents<TEventHandler> events)
        {
            _engine = engine;
            _events = events;
        }

        /// <summary>
        /// Performs any required initialization logic after the Simulation instance has been constructed.
        /// </summary>
        /// <param name="simulation">Simulation that owns these callbacks.</param>
        public void Initialize(Simulation simulation)
        {
            //Often, the callbacks type is created before the simulation instance is fully constructed, so the simulation will call this function when it's ready.
            //Any logic which depends on the simulation existing can be put here.
            _events.Initialize(simulation.Bodies);
        }

        private bool _simpleShallCollide(CollidableReference a, CollidableReference b)
        {
            /*
             * Short circuit, only care about collisions with the player (that is the
             * only dynamic object).
             */
            if (a.Mobility != CollidableMobility.Dynamic && b.Mobility != CollidableMobility.Dynamic)
            {
                return false;
            }
            
            /*
             * Try to obtain collision properties of either body.
             *
             * Currently, we only care about collision between dynamic and [static, kinetic] objects.
             */
            bool doACollide = false;
            switch (a.Mobility)
            {
                case CollidableMobility.Dynamic:
                    doACollide = true;
                    break;
                case CollidableMobility.Kinematic:
                    bool haveProperties = _engine.GetAPhysics().GetCollisionProperties(a.BodyHandle, out var collisionProperties);
                    if (haveProperties)
                    {
                        doACollide = collisionProperties.IsTangible;
                    }

                    break;
                case CollidableMobility.Static:
                    doACollide = true;
                    break;
            }

            bool doBCollide = false;
            switch (b.Mobility)
            {
                case CollidableMobility.Dynamic:
                    doBCollide = true;
                    break;
                case CollidableMobility.Kinematic:
                    bool haveProperties = _engine.GetAPhysics().GetCollisionProperties(b.BodyHandle, out var collisionProperties);
                    if (haveProperties)
                    {
                        doBCollide = collisionProperties.IsTangible;
                    }

                    break;
                case CollidableMobility.Static:
                    doBCollide = true;
                    break;
            }

            return doACollide && doBCollide;

        }
        
        
        /// <summary>
        /// Chooses whether to allow contact generation to proceed for two overlapping collidables.
        /// </summary>
        /// <param name="workerIndex">Index of the worker that identified the overlap.</param>
        /// <param name="a">Reference to the first collidable in the pair.</param>
        /// <param name="b">Reference to the second collidable in the pair.</param>
        /// <param name="speculativeMargin">Reference to the speculative margin used by the pair.
        /// The value was already initialized by the narrowphase by examining the speculative margins of the involved collidables, but it can be modified.</param>
        /// <returns>True if collision detection should proceed, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
        {
            //Before creating a narrow phase pair, the broad phase asks this callback whether to bother with a given pair of objects.
            //This can be used to implement arbitrary forms of collision filtering. See the RagdollDemo or NewtDemo for examples.
            //Here, we'll make sure at least one of the two bodies is dynamic.
            //The engine won't generate static-static pairs, but it will generate kinematic-kinematic pairs.
            //That's useful if you're trying to make some sort of sensor/trigger object, but since kinematic-kinematic pairs
            //can't generate constraints (both bodies have infinite inertia), simple simulations can just ignore such pairs.

            //This function also exposes the speculative margin. It can be validly written to, but that is a very rare use case.
            //Most of the time, you can ignore this function's speculativeMargin parameter entirely.
            return _simpleShallCollide(a, b); 
            // a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
        }

        /// <summary>
        /// Chooses whether to allow contact generation to proceed for the children of two overlapping collidables in a compound-including pair.
        /// </summary>
        /// <param name="workerIndex">Index of the worker thread processing this pair.</param>
        /// <param name="pair">Parent pair of the two child collidables.</param>
        /// <param name="childIndexA">Index of the child of collidable A in the pair. If collidable A is not compound, then this is always 0.</param>
        /// <param name="childIndexB">Index of the child of collidable B in the pair. If collidable B is not compound, then this is always 0.</param>
        /// <returns>True if collision detection should proceed, false otherwise.</returns>
        /// <remarks>This is called for each sub-overlap in a collidable pair involving compound collidables. If neither collidable in a pair is compound, this will not be called.
        /// For compound-including pairs, if the earlier call to AllowContactGeneration returns false for owning pair, this will not be called. Note that it is possible
        /// for this function to be called twice for the same subpair if the pair has continuous collision detection enabled; 
        /// the CCD sweep test that runs before the contact generation test also asks before performing child pair tests.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
        {
            //This is similar to the top level broad phase callback above. It's called by the narrow phase before generating subpairs between children in parent shapes. 
            //This only gets called in pairs that involve at least one shape type that can contain multiple children, like a Compound.
            return true;
        }

        /// <summary>
        /// Provides a notification that a manifold has been created for a pair. Offers an opportunity to change the manifold's details. 
        /// </summary>
        /// <param name="workerIndex">Index of the worker thread that created this manifold.</param>
        /// <param name="pair">Pair of collidables that the manifold was detected between.</param>
        /// <param name="manifold">Set of contacts detected between the collidables.</param>
        /// <param name="pairMaterial">Material properties of the manifold.</param>
        /// <returns>True if a constraint should be created for the manifold, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
        {

            /*
             * Setup a bogus pair material.

            //The engine does not define any per-body material properties. Instead, all material lookup and blending operations are handled by the callbacks.
            //For the purposes of this demo, we'll use the same settings for all pairs.
            //(Note that there's no 'bounciness' or 'coefficient of restitution' property!
            //Bounciness is handled through the contact spring settings instead. Setting See here for more details: https://github.com/bepu/bepuphysics2/issues/3 and check out the BouncinessDemo for some options.)
             */
            pairMaterial.FrictionCoefficient = 1f;
            pairMaterial.MaximumRecoveryVelocity = 10;
            pairMaterial.SpringSettings = new SpringSettings(30, 1);

            //The IContactManifold parameter includes functions for accessing contact data regardless of what the underlying type of the manifold is.
            //If you want to have direct access to the underlying type, you can use the manifold.Convex property and a cast like Unsafe.As<TManifold, ConvexContactManifold or NonconvexContactManifold>(ref manifold).
            _events.HandleManifold(workerIndex, pair, ref manifold);

            bool shallCollide = _simpleShallCollide(pair.A, pair.B);
            
            /*
             * If either one is not collidable, ignore it.
             */
            if (!shallCollide)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Provides a notification that a manifold has been created between the children of two collidables in a compound-including pair.
        /// Offers an opportunity to change the manifold's details. 
        /// </summary>
        /// <param name="workerIndex">Index of the worker thread that created this manifold.</param>
        /// <param name="pair">Pair of collidables that the manifold was detected between.</param>
        /// <param name="childIndexA">Index of the child of collidable A in the pair. If collidable A is not compound, then this is always 0.</param>
        /// <param name="childIndexB">Index of the child of collidable B in the pair. If collidable B is not compound, then this is always 0.</param>
        /// <param name="manifold">Set of contacts detected between the collidables.</param>
        /// <returns>True if this manifold should be considered for constraint generation, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
        {
            return true;
        }

        /// <summary>
        /// Releases any resources held by the callbacks. Called by the owning narrow phase when it is being disposed.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
