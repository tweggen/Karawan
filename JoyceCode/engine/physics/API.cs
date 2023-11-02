using System;
using System.Collections.Generic;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Memory;
using engine.news;
using static engine.Logger;
using Trace = System.Diagnostics.Trace;


namespace engine.physics;

/**
 * Implements the binding of the physics engine to the main game engine.
 */
public class API
    : physics.IContactEventHandler
{
    private object _lo = new();

    private uint _frameId = 0;
    
    private Engine _engine;

    private SortedDictionary<int, CollisionProperties> _mapCollisionProperties = new();

    private SortedDictionary<ulong, uint> _previousCollisions = new();
    
    /**
     * This is the single one callback called for every collision.
     * Therefore we need to dispatch and detect new collisions.
     */
    public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
        in Vector3 contactOffset, in Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : struct, IContactManifold<TManifold>
    {
        // Trace($"having contact.");

        if (contactManifold.Count > 0)
        {
            // Trace("There is something in the manifold.");
        }

        //physics.ContactInfo contactInfo = new(
        //    eventSource, pair, contactOffset, contactNormal, depth);
        
        CollisionProperties propsA = null;
        CollisionProperties propsB = null;

        uint ahandle = 0;
        uint bhandle = 0;
        lock (_lo)
        {
            switch (pair.A.Mobility)
            {
                case CollidableMobility.Dynamic:
                case CollidableMobility.Kinematic:
                    _mapCollisionProperties.TryGetValue(pair.A.BodyHandle.Value, out propsA);
                    ahandle = (uint) pair.A.BodyHandle.Value;
                    break;
                case CollidableMobility.Static:
                    ahandle = (uint)0x80000000 | (uint) pair.A.StaticHandle.Value;
                    // _mapCollisionProperties.TryGetValue(pair.A.StaticHandle.Value, out propsA);
                    break;
            }
            
            switch (pair.B.Mobility)
            {
                case CollidableMobility.Dynamic:
                case CollidableMobility.Kinematic:
                    _mapCollisionProperties.TryGetValue(pair.B.BodyHandle.Value, out propsB);
                    bhandle = (uint) pair.B.BodyHandle.Value;
                    break;
                case CollidableMobility.Static:
                    // _mapCollisionProperties.TryGetValue(pair.B.StaticHandle.Value, out propsB);
                    bhandle = (uint)0x80000000 | (uint) pair.B.StaticHandle.Value;
                    break;
            }
        }

        ulong collHash = ((ulong)bhandle << 32) | (ulong)ahandle;
        bool havePreviousCollision = false;
        lock (_lo)
        {
            havePreviousCollision = _previousCollisions.TryGetValue(collHash, out uint lastFrameId);
            /*
             * Regardless, if we had a collision or not, update the frameId.
             */
            _previousCollisions[collHash] = _frameId;
        }
        
        if (!havePreviousCollision)
        {
            /*
             * We deliver collisions by calling the contact event handler of the behavior.
             */
            // TXWTODO: Maybe there's a faster way to enqueue?
            Vector3 vContactOffset = contactOffset;
            Vector3 vContactNormal = contactNormal;
            if (null != propsA && 0 != (propsA.Flags & CollisionProperties.CollisionFlags.TriggersCallbacks))
            {
                _engine.QueueMainThreadAction(() =>
                {
                    DefaultEcs.Entity entity = propsA.Entity;
                    if (entity.IsAlive && entity.IsEnabled())
                    {
                        if (entity.Has<engine.behave.components.Behavior>())
                        {
                            var cBehavior = entity.Get<engine.behave.components.Behavior>();
                            var iBehaviorProvider = cBehavior.Provider;
                            if (iBehaviorProvider != null)
                            {
                                /*
                                 * Send the proper event
                                 */
                                physics.ContactInfo contactInfo = new(eventSource,
                                    new CollidablePair(pair.A, pair.B),
                                    vContactOffset, vContactNormal, depth)
                                {
                                    PropertiesA =  propsA, PropertiesB = propsB
                                };
                                iBehaviorProvider.OnCollision(new ContactEvent(contactInfo));
                            }
                        }
                    }
                });
            }
            if (null != propsB && 0 != (propsB.Flags & CollisionProperties.CollisionFlags.TriggersCallbacks))
            {
                _engine.QueueMainThreadAction(() =>
                {
                    DefaultEcs.Entity entity = propsB.Entity;
                    if (entity.IsAlive && entity.IsEnabled())
                    {
                        if (entity.Has<engine.behave.components.Behavior>())
                        {
                            var cBehavior = entity.Get<engine.behave.components.Behavior>();
                            var iBehaviorProvider = cBehavior.Provider;
                            if (iBehaviorProvider != null)
                            {
                                /*
                                 * Send the proper event
                                 */
                                physics.ContactInfo contactInfo = new(eventSource,
                                    new CollidablePair(pair.B, pair.A),
                                    vContactOffset, vContactNormal, depth)
                                {
                                    PropertiesA =  propsB, PropertiesB = propsA
                                };
                                iBehaviorProvider.OnCollision(new ContactEvent(contactInfo));
                            }
                        }
                    }
                });
            }
        }

    }


    public Simulation Simulation { get; private set;  }
    public BufferPool BufferPool { get; private set; }
    private physics.ContactEvents<API> _contactEvents;
    private ThreadDispatcher  _physicsThreadDispatcher;


    private void _refreshCollisions()
    {
        lock (_lo)
        {
            List<ulong> deleteKeys = new();
            foreach (var kvp in _previousCollisions)
            {
                if (kvp.Value != _frameId)
                {
                    deleteKeys.Add(kvp.Key);
                }
            }

            foreach (var key in deleteKeys)
            {
                _previousCollisions.Remove(key);
            }

            ++_frameId;
        }
    }


    public void Update(float dt)
    {
        _refreshCollisions();
        Simulation.Timestep(dt,_physicsThreadDispatcher);
    }


    public void RayCast(Vector3 origin, Vector3 target, float length, 
        Action<CollidableReference, CollisionProperties, Vector3> action)
    {
        _engine.QueueMainThreadAction(() =>
        {
            DefaultRayHitHandler drh = new(this, action);
            Simulation.RayCast(origin, target, length, ref drh, drh.GetRayHitId());
        });
        
    }
    
    
    /**
     * Register a listener who is notified on callbacks.
     */
    public void AddContactListener(in DefaultEcs.Entity entity)
    {
        _contactEvents.RegisterListener(
            new CollidableReference(
                CollidableMobility.Dynamic, 
                entity.Get<physics.components.Body>().Reference.Handle));
    }

    
    /**
     * Unregister a notify listener.
     */
    public void RemoveContactListener(
        in DefaultEcs.Entity entity, 
        in BepuPhysics.BodyReference bodyReference)
    {
        _contactEvents.UnregisterListener(
            new CollidableReference(
                CollidableMobility.Dynamic,
                bodyReference.Handle));
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