using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using BepuPhysics.Collidables;
using DefaultEcs;
using engine;
using engine.gongzuo;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;
using static engine.Logger;

namespace nogame.modules.playerhover;

public class WalkModule : AModule, IInputPart
{
    public static float MY_Z_ORDER = 25f;

    static public readonly string PhysicsName = "nogame.playerhover.person";

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<InputEventPipeline>(),
        new SharedModule<PlayerPosition>(),
        new MyModule<UpdateEmissionContext>(),
        new MyModule<DriveCarCollisionsController>(),
        new MyModule<WalkTouchButton>()
    };

    private DefaultEcs.Entity _ePerson;
    private DefaultEcs.Entity _eAnimations;
    private DefaultEcs.Entity _eRightHand;
    private DefaultEcs.Entity _eLeftHand;
    private BepuPhysics.BodyReference _prefPerson;
    private Entity _eMapPerson;
    private AnimationState _animStatePerson = new();

    private TransformApi _aTransform;
    
    private Model _model;

    public CharacterModelDescription CharacterModelDescription{
        get;
        set;
    }
    
    public float MassPerson { get; set; } = 100f;

    /**
      * Sound API
      */
    private Boom.ISoundAPI _aSound;
    
    public void InputPartOnInputEvent(Event ev)
    {
        if (ev.Type != Event.INPUT_BUTTON_PRESSED)
        {
            return;
        }

        if (ev.Code == "<change>")
        {
            /*
             * We are supposed to get out of the car.
             */
            ev.IsHandled = true;
            I.Get<EventQueue>().Push(new Event(MainPlayModule.EventCodeGetIntoHover, ""));
        }
    }

    
    private void _onLogicalFrame(object? sender, float dt)
    {
    }


    private void _cleanupPlayer()
    {
        _engine.Player.Value = default;
        I.Get<HierarchyApi>().Delete(ref _ePerson);
        
        I.Get<EventQueue>().Push(new Event(MainPlayModule.EventCodeIsPersonDeactivated, ""));
    }
    
    
    protected override void OnModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);
        _engine.OnLogicalFrame -= _onLogicalFrame;

        _engine.QueueMainThreadAction(_cleanupPlayer);
    }


    private async Task _setupPlayer()
    {
        try
        {
            _aTransform = I.Get<engine.joyce.TransformApi>();

            _aSound = I.Get<Boom.ISoundAPI>();

            InstantiateModelParams instantiateModelParams = new()
            {
                GeomFlags = CharacterModelDescription.ModelGeomFlags,
                MaxDistance = 200f
            };

            _model = await I.Get<ModelCache>().LoadModel(
                new ModelCacheParams()
                {
                    Url = CharacterModelDescription.ModelUrl,
                    Params = instantiateModelParams,
                    Properties = new() {Properties = new()
                    {
                        { "AnimationUrls", CharacterModelDescription.AnimationUrls },
                        { "CPUNodes", CharacterModelDescription.CPUNodes },
                        { "Scale", CharacterModelDescription.Scale },
                        { "ModelBaseBone", CharacterModelDescription.ModelBaseBone }
                    }}
                });

            /*
             * Read the current position.
             * Note, that we need to apply the player's position to the entity for
             * the walking figure, because it is kinematic as opposed to the ship,
             * that is dynamic, and thus needs the position on the physics.
             */
            M<PlayerPosition>().GetPlayerPosition(out var v3Person, out var qPerson);

            /*
             * Create the ship entities. This needs to run in logical thread.
             */
            _engine.QueueMainThreadAction(() =>
            {
                _ePerson = _engine.CreateEntity("RootScene.playerperson");

                _aTransform.SetPosition(_ePerson, v3Person);
                _aTransform.SetRotation(_ePerson, qPerson);
                _aTransform.SetVisible(_ePerson, engine.GlobalSettings.Get("nogame.PlayerVisible") != "false");
                _aTransform.SetCameraMask(_ePerson, 0x0000ffff);

                {
                    builtin.tools.ModelBuilder modelBuilder = new(_engine, _model, instantiateModelParams);
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

                /*
                 * I have absolutely no clue why, but with the real radius of the model (1.039f) the
                 * thing bounces away to nirvana very soon.
                 * Therefore we set the previously hard coded 1.4 as a lower limit.
                 */
                float bodyRadius = _model.ModelNodeTree.RootNode.InstanceDesc != null
                    ? _model.ModelNodeTree.RootNode.InstanceDesc.AABBTransformed.Radius
                    : 1.4f;

                engine.physics.CollisionProperties collisionProperties =
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
                        v3Person, qPerson, new(0f, personHeight / 2f, 0f))
                    {
                        CollisionProperties = collisionProperties
                    }.AddContactListener();
                    _prefPerson = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
                }

                _ePerson.Set(new engine.physics.components.Body(po, _prefPerson));
                _ePerson.Set(new engine.behave.components.Behavior(new WalkBehavior()
                {
                    MassTarget = MassPerson,
                    CharacterModelDescription = CharacterModelDescription
                }));

                /*
                 * Create a right hand entity attached to animation
                 */
                {
                    _eRightHand = _engine.CreateEntity("RootScene.playerperson.righthand");
                    I.Get<HierarchyApi>().SetParent(_eRightHand, _ePerson);
                    I.Get<TransformApi>().SetTransforms(_eRightHand, true,
                        0x0000ffff,
                        Quaternion.Identity, Vector3.Zero);
                    var idRightHandCube = InstanceDesc.CreateFromMatMesh(
                        new MatMesh(
                            I.Get<ObjectRegistry<Material>>().Get("nogame.characters.polytope.materials.cube"),
                            engine.joyce.mesh.Tools.CreateCubeMesh("RootScene.playerperson.righthand", 0.5f)
                        ), 1000f
                    );
                    _eRightHand.Set(new CpuAnimated() { AnimationState = _animStatePerson, ModelNodeName = "MiddleFinger2_R"});
                    _eRightHand.Set(new Instance3(idRightHandCube));
                }

                /*
                 * Now add an entity as a child that will display in the map
                 */
                _eMapPerson = _engine.CreateEntity("RootScene.playership.map");
                I.Get<HierarchyApi>().SetParent(_eMapPerson, _ePerson);
                I.Get<TransformApi>().SetTransforms(_eMapPerson, true,
                    nogame.modules.map.Module.MapCameraMask,
                    Quaternion.Identity, Vector3.Zero);
                _eMapPerson.Set(new engine.world.components.MapIcon()
                    { Code = engine.world.components.MapIcon.IconCode.Player0 });

                _engine.OnLogicalFrame += _onLogicalFrame;

                _engine.Player.Value = _ePerson;

                M<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);

                /*
                 * Finally, we are boarded.
                 */
                I.Get<EventQueue>().Push(new Event(MainPlayModule.EventCodeIsPersonActivated, ""));
            }); // End of queue mainthread action.
        }
        catch (Exception e)
        {
            Warning($"Exception in _setupPlayer main code: {e}");
        }
    }


    protected override void OnModuleActivate()
    {
        _engine.Run(_setupPlayer);
    }
}