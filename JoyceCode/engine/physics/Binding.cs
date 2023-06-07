using System;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuUtilities;
// using BepuUtilities.Collections;
using BepuUtilities.Memory;

namespace engine.physics;

public class Binding
{
    private Engine _engine;

    public event EventHandler<physics.ContactInfo> OnContactInfo;

    class EnginePhysicsEventHandler : physics.IContactEventHandler
    {
        // public Simulation Simulation;
        public Binding Binding;

        public void OnContactAdded<TManifold>(CollidableReference eventSource, CollidablePair pair, ref TManifold contactManifold,
            in Vector3 contactOffset, in Vector3 contactNormal, float depth, int featureId, int contactIndex, int workerIndex) where TManifold : struct, IContactManifold<TManifold>
        {
            physics.ContactInfo contactInfo = new(
                eventSource, pair, contactOffset, contactNormal, depth);
            Binding.OnContactInfo?.Invoke(this, contactInfo);
        }
    }


    public Simulation Simulation { get; private set;  }
    public BufferPool BufferPool { get; private set; }
    private physics.ContactEvents<EnginePhysicsEventHandler> _contactEvents;
    private ThreadDispatcher  _physicsThreadDispatcher;

    
    public void AddContactListener(DefaultEcs.Entity entity)
    {
        _contactEvents.RegisterListener(
            new CollidableReference(
                CollidableMobility.Dynamic, 
                entity.Get<physics.components.Body>().Reference.Handle));
    }

    public void RemoveContactListener(DefaultEcs.Entity entity)
    {
        _contactEvents.UnregisterListener(
            new CollidableReference(
                CollidableMobility.Dynamic,
                entity.Get<physics.components.Body>().Reference.Handle));
    }


    public Binding(Engine engine)
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
        enginePhysicsEventHandler.Binding = this;
        Simulation = Simulation.Create(
            BufferPool, 
            new physics.NarrowPhaseCallbacks<EnginePhysicsEventHandler>(
                _engine,
                _contactEvents) /* { Properties = properties } */,
            new physics.PoseIntegratorCallbacks(new Vector3(0, -9.81f, 0)),
            new SolveDescription(8, 1)
        );
        // enginePhysicsEventHandler.Simulation = Simulation;


    }
}