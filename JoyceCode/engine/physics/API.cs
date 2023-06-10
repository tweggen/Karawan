using System;
using System.Collections.Generic;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Memory;


namespace engine.physics;

/**
 * Implements the binding of the physics engine to the main game engine.
 */
public class API
{
    private object _lo = new();

    private Engine _engine;

    private SortedDictionary<int, CollisionProperties> _mapCollisionProperties = new();

    public event EventHandler<physics.ContactInfo> OnContactInfo;

    
    class EnginePhysicsEventHandler : physics.IContactEventHandler
    {
        // public Simulation Simulation;
        public API API;

        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            in Vector3 contactOffset, in Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : struct, IContactManifold<TManifold>
        {
            physics.ContactInfo contactInfo = new(
                eventSource, pair, contactOffset, contactNormal, depth);
            API.OnContactInfo?.Invoke(this, contactInfo);
        }
    }


    public Simulation Simulation { get; private set;  }
    public BufferPool BufferPool { get; private set; }
    private physics.ContactEvents<EnginePhysicsEventHandler> _contactEvents;
    private ThreadDispatcher  _physicsThreadDispatcher;

    
    /**
     * Register a listener who is notified on callbacks.
     */
    public void AddContactListener(DefaultEcs.Entity entity)
    {
        _contactEvents.RegisterListener(
            new CollidableReference(
                CollidableMobility.Dynamic, 
                entity.Get<physics.components.Body>().Reference.Handle));
    }

    
    /**
     * Unregister a notify listener.
     */
    public void RemoveContactListener(DefaultEcs.Entity entity)
    {
        _contactEvents.UnregisterListener(
            new CollidableReference(
                CollidableMobility.Dynamic,
                entity.Get<physics.components.Body>().Reference.Handle));
    }
    
    
    public bool GetCollisionProperties(in BodyHandle bodyHandle, out CollisionProperties collisionProperties)
    {
        lock (_lo)
        {
            return _mapCollisionProperties.TryGetValue(bodyHandle.Value, out collisionProperties);
        }
    }
    
    
    /**
     * Add a record of collision properties for the given body
     */
    public void AddCollisionEntry(in BodyHandle bodyHandle, CollisionProperties collisionProperties)
    {
        lock (_lo)
        {
            _mapCollisionProperties[bodyHandle.Value] = collisionProperties;
        }
    }

    
    /**
     * Remove a record of collision properties for the given body.
     */
    public void RemoveCollisionEntry(in BodyHandle bodyHandle)
    {
        lock (_lo)
        {
            _mapCollisionProperties.Remove(bodyHandle.Value);
        }
    }


    public API(Engine engine)
    {
        _engine = engine;
        BufferPool = new BufferPool();
        _physicsThreadDispatcher = new(4);
        EnginePhysicsEventHandler enginePhysicsEventHandler = new();
        _contactEvents = new physics.ContactEvents<EnginePhysicsEventHandler>(
            enginePhysicsEventHandler,
            BufferPool,
            _physicsThreadDispatcher
        );
        enginePhysicsEventHandler.API = this;
        Simulation = Simulation.Create(
            BufferPool, 
            new physics.NarrowPhaseCallbacks<EnginePhysicsEventHandler>(
                _engine,
                _contactEvents) /* { Properties = properties } */,
            new physics.PoseIntegratorCallbacks(engine,
                new Vector3(0, -9.81f, 0)),
            new SolveDescription(8, 1)
        );
        // enginePhysicsEventHandler.Simulation = Simulation;


    }
}