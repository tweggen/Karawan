using System;
using System.Collections.Generic;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Memory;
using static engine.Logger;


namespace engine.physics;

/**
 * Implements the binding of the physics engine to the main game engine.
 */
public class API
    : physics.IContactEventHandler
{
    private object _lo = new();

    private Engine _engine;

    private SortedDictionary<int, CollisionProperties> _mapCollisionProperties = new();

    public event EventHandler<physics.ContactInfo> OnContactInfo;

    
    public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
        in Vector3 contactOffset, in Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : struct, IContactManifold<TManifold>
    {
        // Trace($"having contact.");
        
        physics.ContactInfo contactInfo = new(
            eventSource, pair, contactOffset, contactNormal, depth);

        lock (_lo)
        {
            switch (pair.A.Mobility)
            {
                case CollidableMobility.Dynamic:
                case CollidableMobility.Kinematic:
                    _mapCollisionProperties.TryGetValue(pair.A.BodyHandle.Value, out contactInfo.PropertiesA);
                    break;
                case CollidableMobility.Static:
                    _mapCollisionProperties.TryGetValue(pair.A.StaticHandle.Value, out contactInfo.PropertiesA);
                    break;
            }
            
            switch (pair.B.Mobility)
            {
                case CollidableMobility.Dynamic:
                case CollidableMobility.Kinematic:
                    _mapCollisionProperties.TryGetValue(pair.B.BodyHandle.Value, out contactInfo.PropertiesB);
                    break;
                case CollidableMobility.Static:
                    _mapCollisionProperties.TryGetValue(pair.B.StaticHandle.Value, out contactInfo.PropertiesB);
                    break;
            }

            // TXWTODO: Get/Filter out by name
        }

        OnContactInfo?.Invoke(this, contactInfo);
    }


    public Simulation Simulation { get; private set;  }
    public BufferPool BufferPool { get; private set; }
    private physics.ContactEvents<API> _contactEvents;
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
        _contactEvents = new physics.ContactEvents<API>(
            this,
            BufferPool,
            _physicsThreadDispatcher
        );
        Simulation = Simulation.Create(
            BufferPool, 
            new physics.NarrowPhaseCallbacks<API>(
                _engine,
                _contactEvents) /* { Properties = properties } */,
            new physics.PoseIntegratorCallbacks(engine,
                new Vector3(0, -9.81f, 0)),
            new SolveDescription(8, 1)
        );

    }
}