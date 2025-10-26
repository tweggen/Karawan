using System;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using BepuPhysics.Collidables;
using DefaultEcs;
using engine;
using engine.behave;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;
using nogame.modules.playerhover;
using static engine.Logger;

namespace nogame.characters;

public class EntityCreator
{
    public required CharacterModelDescription CharacterModelDescription;
    public Vector3 Position;
    public Quaternion Orientation;
    public required string PhysicsName;
    public required float MaxDistance;
    public Func<Entity, IBehavior>? BehaviorFactory = null;
    public bool CreateRightHand = false;

    private TransformApi _aTransform;
    private Model _model;
    private Engine _engine = I.Get<Engine>();

    private Entity _ePerson;
    private Entity _eRightHand;
    private Entity _eAnimations;
    private AnimationState _animStatePerson = new();
    private BodyReference _prefPerson;
    private BodyReference _prefRightHand;

    private InstantiateModelParams _instantiateModelParams;
    
    public Entity CreateLogical()
    {
        
        /*
         * Read the current position.
         * Note, that we need to apply the player's position to the entity for
         * the walking figure, because it is kinematic as opposed to the ship,
         * that is dynamic, and thus needs the position on the physics.
         */
        ref var v3PlayerPerson = ref Position;
        ref var qPlayerPerson = ref Orientation;

        _ePerson = _engine.CreateEntity("RootScene.playerperson");

        _aTransform.SetPosition(_ePerson, v3PlayerPerson);
        _aTransform.SetRotation(_ePerson, qPlayerPerson);
        _aTransform.SetVisible(_ePerson, engine.GlobalSettings.Get("nogame.PlayerVisible") != "false");
        _aTransform.SetCameraMask(_ePerson, 0x0000ffff);

        {
            builtin.tools.ModelBuilder modelBuilder = new(_engine, _model, _instantiateModelParams);
            modelBuilder.BuildEntity(_ePerson);
            _eAnimations = modelBuilder.GetAnimationsEntity();
        }

        if (default != _eAnimations)
        {
            CharacterModelDescription.EntityAnimations = _eAnimations;
            CharacterModelDescription.Model = _model;
            CharacterModelDescription.AnimationState = _animStatePerson;

            var mapAnimations = _model.MapAnimations;
            if (mapAnimations != null && mapAnimations.Count > 0)
            {
                if (mapAnimations.TryGetValue(
                        CharacterModelDescription.IdleAnimName, out var animation))
                {
                    _animStatePerson.ModelAnimation = animation;
                    _animStatePerson.ModelAnimationFrame = 0;

                    _eAnimations.Set(new GPUAnimationState
                    {
                        AnimationState = _animStatePerson
                    });
                }
                else
                {
                    Trace($"Test animation {CharacterModelDescription.IdleAnimName} not found.");
                }
            }
        }


        {
            engine.physics.CollisionProperties personCollisionProperties =
                new engine.physics.CollisionProperties
                {
                    Entity = _ePerson,
                    Flags =
                        CollisionProperties.CollisionFlags.IsTangible
                        | CollisionProperties.CollisionFlags.IsDetectable
                        | CollisionProperties.CollisionFlags.TriggersCallbacks,
                    Name = PhysicsName,
                    LayerMask = 0x00ff,
                };
            engine.physics.Object po;
            lock (_engine.Simulation)
            {
                float personHeight = 1.8f;
                uint uintShape = (uint)engine.physics.actions.CreateCylinderShape.Execute(
                    _engine.PLog, _engine.Simulation,
                    0.3f, 1.8f,
                    out var pbody);
                po = new engine.physics.Object(_engine, _ePerson, new TypedIndex() { Packed = uintShape },
                    v3PlayerPerson, qPlayerPerson, new(0f, personHeight / 2f, 0f))
                {
                    CollisionProperties = personCollisionProperties
                }.AddContactListener();
                _prefPerson = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
            }

            _ePerson.Set(new engine.physics.components.Body(po, _prefPerson));
        }

        if (default != BehaviorFactory)
        {
            _ePerson.Set(new engine.behave.components.Behavior(BehaviorFactory(_ePerson)));
        }

        /*
         * Create a right hand entity attached to animation
         */
        if (CreateRightHand)
        {
            _eRightHand = _engine.CreateEntity("RootScene.playerperson.righthand");
            I.Get<HierarchyApi>().SetParent(_eRightHand, _ePerson);
            I.Get<TransformApi>().SetTransforms(_eRightHand, true,
                0x0000ffff,
                Quaternion.Identity, Vector3.Zero);
            var idRightHandCube = InstanceDesc.CreateFromMatMesh(
                new MatMesh(
                    I.Get<ObjectRegistry<Material>>().Get("nogame.characters.polytope.materials.cube"),
                    engine.joyce.mesh.Tools.CreateCubeMesh("RootScene.playerperson.righthand", 0.2f)
                ), 1000f
            );
            _eRightHand.Set(new CpuAnimated()
                { AnimationState = _animStatePerson, ModelNodeName = "MiddleFinger2_R" });
            _eRightHand.Set(new Instance3(idRightHandCube));

            {
                engine.physics.CollisionProperties rightHandCollisionProperties =
                    new engine.physics.CollisionProperties
                    {
                        Entity = _eRightHand,
                        Flags =
                            CollisionProperties.CollisionFlags.IsTangible
                            | CollisionProperties.CollisionFlags.IsDetectable
                            | CollisionProperties.CollisionFlags.TriggersCallbacks,
                        Name = $"{PhysicsName}.RightHand",
                        LayerMask = 0x00ff,
                    };
                engine.physics.Object po;
                lock (_engine.Simulation)
                {
                    uint uintShape = (uint)engine.physics.actions.CreateSphereShape.Execute(
                        _engine.PLog, _engine.Simulation,
                        0.1f,
                        out var pbody);
                    po = new engine.physics.Object(_engine, _eRightHand,
                        new TypedIndex() { Packed = uintShape },
                        v3PlayerPerson, qPlayerPerson)
                    {
                        CollisionProperties = rightHandCollisionProperties
                    }.AddContactListener();
                    _prefRightHand = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
                }

                _eRightHand.Set(new engine.physics.components.Body(po, _prefRightHand));
                _eRightHand.Set(new engine.behave.components.Behavior(new HandBehavior()
                {
                }));
            }
        }

        return _ePerson;
    }


    public async Task<Entity> CreateAsync()
    {
        try
        {
            _aTransform = I.Get<engine.joyce.TransformApi>();

            _instantiateModelParams = new()
            {
                GeomFlags = CharacterModelDescription.ModelGeomFlags,
                MaxDistance = MaxDistance
            };

            _model = await I.Get<ModelCache>().LoadModel(
                new ModelCacheParams()
                {
                    Url = CharacterModelDescription.ModelUrl,
                    Params = _instantiateModelParams,
                    Properties = new()
                    {
                        Properties = new()
                        {
                            { "AnimationUrls", CharacterModelDescription.AnimationUrls },
                            { "CPUNodes", CharacterModelDescription.CPUNodes },
                            { "Scale", CharacterModelDescription.Scale },
                            { "ModelBaseBone", CharacterModelDescription.ModelBaseBone }
                        }
                    }
                });
        }
        catch (Exception e)
        {
            Warning($"Exception in _setupPlayer main code: {e}");
        }
        
        return _ePerson;
    }

}