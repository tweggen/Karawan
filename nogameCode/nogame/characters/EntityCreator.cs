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
    public Vector3 Position = Vector3.Zero;
    public Quaternion Orientation = Quaternion.Identity;
    public required string PhysicsName;
    public engine.world.Fragment Fragment = null;
    
    public Func<Entity, IBehavior>? BehaviorFactory = null;
    public Func<Entity, IEntityStrategy>? EntityStrategyFactory = null;
    public Func<Entity, CollisionProperties>? CollisionPropertiesFactory = null;
    
    public bool CreateRightHand = false;

    private TransformApi _aTransform;
    private Model _model;
    private Engine _engine = I.Get<Engine>();

    private Entity _ePerson;
    private Entity _eRightHand;
    private AnimationState _animStatePerson = new();
    private BodyReference _prefPerson;
    private BodyReference _prefRightHand;

    /**
     * Output member valid after CreateAsync.
     */
    public InstantiateModelParams? InstantiateModelParams = null;
    public ModelCacheParams ModelCacheParams { get; private set; } = null;

    /**
     * Output member valid after CreateLogical.
     */
    public Entity EntityAnimations;


    private Entity _createLogical()
    {
        /*
         * Read the current position.
         * Note, that we need to apply the player's position to the entity for
         * the walking figure, because it is kinematic as opposed to the ship,
         * that is dynamic, and thus needs the position on the physics.
         */
        ref var v3PlayerPerson = ref Position;
        ref var qPlayerPerson = ref Orientation;


        _aTransform.SetPosition(_ePerson, v3PlayerPerson);
        _aTransform.SetRotation(_ePerson, qPlayerPerson);
        _aTransform.SetVisible(_ePerson, engine.GlobalSettings.Get("nogame.PlayerVisible") != "false");
        _aTransform.SetCameraMask(_ePerson, 0x0000ffff);

        {
            builtin.tools.ModelBuilder modelBuilder = new(_engine, _model, InstantiateModelParams);
            modelBuilder.BuildEntity(_ePerson);
            I.Get<ModelCache>().BuildPerInstancePhysics(_ePerson, modelBuilder, _model, ModelCacheParams);
            EntityAnimations = modelBuilder.GetAnimationsEntity();
            
            /*
             * We already setup the FromModel in case we utilize one of the characters as
             * subject of a Quest.
             */
            _ePerson.Set(new engine.joyce.components.FromModel()
            {
                Model = _model, ModelCacheParams = ModelCacheParams
            });


        }

        if (Fragment != null)
        {
            int fragmentId = Fragment.NumericalId;
            _ePerson.Set(new engine.world.components.Owner(fragmentId));
            
            /*
             * We need to set a preliminary Transform3World component. Invisible, but inside the fragment.
             * That way, the character will not be cleaned up immediately.
             */
            _ePerson.Set(new engine.joyce.components.Transform3ToWorld(0, 0,
                Matrix4x4.CreateTranslation(Fragment.Position)));

        }

        if (default != EntityAnimations)
        {
            CharacterModelDescription.EntityAnimations = EntityAnimations;
            CharacterModelDescription.Model = _model;
            CharacterModelDescription.AnimationState = _animStatePerson;
        }
        
        if (CollisionPropertiesFactory != null) {
            
            engine.physics.CollisionProperties personCollisionProperties = CollisionPropertiesFactory(_ePerson);
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

        {
            /*
             * If we created physics for this one, take care to minimize
             * the distance for physics support.
             */
            if (_ePerson.Has<engine.physics.components.Body>())
            {
                ref var cBody = ref _ePerson.Get<engine.physics.components.Body>();
                if (cBody.PhysicsObject != null)
                {
                    cBody.PhysicsObject.MaxDistance = CharacterModelDescription.PhysicsDistance;
                }
            }
        }


        if (default != EntityStrategyFactory)
        {
            try
            {
                IEntityStrategy strategy = EntityStrategyFactory(_ePerson);
                _ePerson.Set(new engine.behave.components.Strategy(strategy));
            }
            catch (Exception e)
            {
                Warning($"Unable to instantiate entity strategy: {e}");
            }
        }
        
        if (default != BehaviorFactory)
        {
            IBehavior behavior;
            try
            {
                behavior = BehaviorFactory(_ePerson);
                _ePerson.Set(new engine.behave.components.Behavior(behavior));
            }
            catch (Exception e)
            {
                Warning($"Unable to instantiate behavior: {e}");
            }
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
                        SolidLayerMask = CollisionProperties.Layers.PlayerMelee,
                        SensitiveLayerMask = 0
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


    public Entity CreateLogical(DefaultEcs.Entity eTarget)
    {
        _ePerson = eTarget;
        return _createLogical();
    }

    public Entity CreateLogical()
    {
        _ePerson = _engine.CreateEntity("RootScene.playerperson");
        return _createLogical();
    }

    public async Task<Model> CreateAsync()
    {
        try
        {
            _aTransform = I.Get<engine.joyce.TransformApi>();

            if (null == CharacterModelDescription.InstantiateModelParams)
            {
                InstantiateModelParams = new();
            }
            else
            {
                InstantiateModelParams = CharacterModelDescription.InstantiateModelParams;
            }

            ModelCacheParams = new ModelCacheParams()
            {
                Url = CharacterModelDescription.ModelUrl,
                Params = InstantiateModelParams,
                Properties = new()
                {
                    Properties = new()
                    {
                        { "Scale", CharacterModelDescription.Scale },
                    }
                }
            };
            if (CharacterModelDescription.CPUNodes != null)
            {
                ModelCacheParams.Properties.Properties.Add("CPUNodes", CharacterModelDescription.CPUNodes);    
            }

            if (CharacterModelDescription.AnimationUrls != null)
            {
                ModelCacheParams.Properties.Properties.Add("AnimationUrls", CharacterModelDescription.AnimationUrls);    
            }

            if (CharacterModelDescription.ModelBaseBone != null)
            {
                ModelCacheParams.Properties.Properties.Add("ModelBaseBone", CharacterModelDescription.ModelBaseBone);   
            }
            
            _model = await I.Get<ModelCache>().LoadModel(ModelCacheParams);
        }
        catch (Exception e)
        {
            Warning($"Exception in _setupPlayer main code: {e}");
        }
        
        return _model;
    }

}