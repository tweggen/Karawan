using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    private SortedDictionary<int, CollisionProperties> _mapNonstaticCollisionProperties = new();
    private SortedDictionary<int, CollisionProperties> _mapStaticCollisionProperties = new();

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
                    _mapNonstaticCollisionProperties.TryGetValue(pair.A.BodyHandle.Value, out propsA);
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
                    _mapNonstaticCollisionProperties.TryGetValue(pair.B.BodyHandle.Value, out propsB);
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
                        /*
                         * Send the proper event
                         */
                        physics.ContactInfo contactInfo = new(eventSource,
                            new CollidablePair(pair.A, pair.B),
                            vContactOffset, vContactNormal, depth)
                        {
                            PropertiesA =  propsA, PropertiesB = propsB
                        };
                        var cev = new ContactEvent(contactInfo);

                        if ((ahandle & 0x80000000) == 0)
                        {
                            if (entity.Has<engine.physics.components.Body>())
                            {
                                var cBody = entity.Get<engine.physics.components.Body>();
                                cBody.PhysicsObject?.OnCollision?.Invoke(cev);
                            }
                        }
                        if (entity.Has<engine.behave.components.Behavior>())
                        {
                            var cBehavior = entity.Get<engine.behave.components.Behavior>();
                            var iBehaviorProvider = cBehavior.Provider;
                            if (iBehaviorProvider != null)
                            {
                                iBehaviorProvider.OnCollision(cev);
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
                        /*
                         * Send the proper event
                         */
                        physics.ContactInfo contactInfo = new(eventSource,
                            new CollidablePair(pair.B, pair.A),
                            vContactOffset, vContactNormal, depth)
                        {
                            PropertiesA =  propsB, PropertiesB = propsA
                        };
                        var cev = new ContactEvent(contactInfo);
                        
                        if ((bhandle & 0x80000000) == 0)
                        {
                            if (entity.Has<engine.physics.components.Body>())
                            {
                                var cBody = entity.Get<engine.physics.components.Body>();
                                cBody.PhysicsObject?.OnCollision?.Invoke(cev);
                            }
                        }
                        if (entity.Has<engine.behave.components.Behavior>())
                        {
                            var cBehavior = entity.Get<engine.behave.components.Behavior>();
                            var iBehaviorProvider = cBehavior.Provider;
                            if (iBehaviorProvider != null)
                            {
                                iBehaviorProvider.OnCollision(cev);
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
        lock (_engine.Simulation)
        {
            try
            {
                Simulation.Timestep(dt, _physicsThreadDispatcher);
                _contactEvents.Flush();
            }
            catch (Exception e)
            {
                if (e.Data.Contains("Handle"))
                {
                    int o = (int) e.Data["Handle"];
                    if (I.Get<engine.physics.ObjectCatalogue>().FindObject(o, out var po))
                    {
                        BodyReference prefBody = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
                        Trace($"Found problem with physics object of entity {po.Entity} bodyhandle {po.IntHandle}.");
                        Trace("Body's constraints:");
                        int nConstraintsOfBody = prefBody.Constraints.Count;
                        for (int i = 0; i < nConstraintsOfBody; ++i)
                        {
                            ref var cref = ref prefBody.Constraints[i];
                            var conHandle = cref.ConnectingConstraintHandle;
                            var biMy = cref.BodyIndexInConstraint;
                            Trace($"Body is in constraint {conHandle} at index {biMy}");
                        }
                        Trace("Known constraints:" );
                        ref var kincons  = ref _engine.Simulation.Solver.ConstrainedKinematicHandles;
                        int nConstrainedKinematics = kincons.Count;
                        for (int i = 0; i < nConstrainedKinematics; ++i)
                        {
                            ref var cref = ref kincons[i];
                            Trace($"Kinematic Handle {cref}");
                        }

                        prefBody.GetDescription(out var desc);
                        System.Threading.Thread.Sleep(200);
                        Trace($"body #{prefBody.Handle}, exists={prefBody.Exists}, isAwake={prefBody.Awake}, Velocity={prefBody.Velocity.Linear}, Pos={prefBody.Pose.Position}, MotionState={prefBody.MotionState}, SolverState={prefBody.SolverState}, Collidable={prefBody.Collidable}, BodyActivity={prefBody.Activity}, NConstraints={prefBody.Constraints.Count}, description={desc.ToString()}");
                    }
                }
                // Trace($"Exception during physics step: {e}");
                throw e;
            }
        }
    }


    public void RayCastSync(Vector3 origin, Vector3 target, float length,
        Action<CollidableReference, CollisionProperties, float, Vector3> action)
    {
        return;
        lock (_engine.Simulation)
        {
            DefaultRayHitHandler drh = new(this, action);
            Simulation.RayCast(origin, target, length, ref drh, drh.GetRayHitId());
        }
    }
    
    
    public void RayCast(Vector3 origin, Vector3 target, float length, 
        Action<CollidableReference, CollisionProperties, float, Vector3> action)
    {
        return;
        _engine.QueueMainThreadAction(() => RayCastSync(origin, target, length, action));
    }

    private SortedSet<int> _setRegisteredEntities = new();
    
    /**
     * Register a listener who is notified on callbacks.
     */
    public void AddContactListener(in DefaultEcs.Entity entity)
    {
        var handle = entity.Get<physics.components.Body>().Reference.Handle;

        lock (_lo)
        {
            if (_setRegisteredEntities.Contains(handle.Value))
            {
                ErrorThrow<ArgumentException>($"Trying to add a contact that already is registered.");
            }

            _setRegisteredEntities.Add(handle.Value);
        }

        lock (_engine.Simulation)
        {
            _contactEvents.RegisterListener(
                new CollidableReference(
                    CollidableMobility.Dynamic,
                    handle));
        }
    }

    
    /**
     * Unregister a notify listener.
     */
    public void RemoveContactListener(
        in DefaultEcs.Entity entity, 
        in BepuPhysics.BodyHandle bodyHandle)
    {
        var handle = entity.Get<physics.components.Body>().Reference.Handle;

        lock (_lo)
        {
            if (!_setRegisteredEntities.Contains(handle.Value))
            {
                ErrorThrow<ArgumentException>($"Trying to remove a contact that already is registered.");
            }
        }

        lock (_engine.Simulation)
        {
            _contactEvents.UnregisterListener(
                new CollidableReference(
                    CollidableMobility.Dynamic,
                    bodyHandle));
        }
    }
    
    
    public bool GetCollisionProperties(in StaticHandle staticHandle, out CollisionProperties collisionProperties)
    {
        lock (_lo)
        {
            return _mapStaticCollisionProperties.TryGetValue(staticHandle.Value, out collisionProperties);
        }
    }
    
    
    /**
     * Add a record of collision properties for the given body
     */
    public void AddCollisionEntry(in StaticHandle staticHandle, CollisionProperties collisionProperties)
    {
        lock (_lo)
        {
            _mapStaticCollisionProperties[staticHandle.Value] = collisionProperties;
        }
    }

    
    /**
     * Remove a record of collision properties for the given body.
     */
    public void RemoveCollisionEntry(in StaticHandle staticHandle)
    {
        lock (_lo)
        {
            _mapStaticCollisionProperties.Remove(staticHandle.Value);
        }
    }


    public bool GetCollisionProperties(in BodyHandle bodyHandle, out CollisionProperties collisionProperties)
    {
        lock (_lo)
        {
            return _mapNonstaticCollisionProperties.TryGetValue(bodyHandle.Value, out collisionProperties);
        }
    }
    
    
    /**
     * Add a record of collision properties for the given body
     */
    public void AddCollisionEntry(in BodyHandle bodyHandle, CollisionProperties collisionProperties)
    {
        lock (_lo)
        {
            _mapNonstaticCollisionProperties[bodyHandle.Value] = collisionProperties;
        }
    }

    
    /**
     * Remove a record of collision properties for the given body.
     */
    public void RemoveCollisionEntry(in BodyHandle bodyHandle)
    {
        lock (_lo)
        {
            _mapNonstaticCollisionProperties.Remove(bodyHandle.Value);
        }
    }


    public API(Engine engine)
    {
        _engine = engine;
        BufferPool = new BufferPool();
        _physicsThreadDispatcher = new(4); /* Environment.ProcessorCount */
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