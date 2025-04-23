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
        new MyModule<nogame.modules.playerhover.UpdateEmissionContext>(),
        new MyModule<nogame.modules.playerhover.DriveCarCollisionsModule>(),
    };

    private DefaultEcs.Entity _ePerson;
    private DefaultEcs.Entity _eAnimations;
    private BepuPhysics.BodyReference _prefPerson;
    private Entity _eMapPerson;

    private TransformApi _aTransform;
    
    private Model _model;

    public CharacterModelDescription CharacterModelDescription{
        get;
        set;
    }
    
    public float MassPerson { get; set; } = 100f;

    public Vector3 StartPosition { get; set; } = Vector3.Zero;
    public Quaternion StartOrientation { get; set; } = Quaternion.Identity;
    
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
    
    
    public override void ModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);
        _engine.OnLogicalFrame -= _onLogicalFrame;

        _engine.QueueMainThreadAction(_cleanupPlayer);
        
        _engine.RemoveModule(this);

        base.ModuleDeactivate();
    }


    private async Task _setupPlayer()
    {
        _aTransform = I.Get<engine.joyce.TransformApi>();

        _aSound = I.Get<Boom.ISoundAPI>();

        InstantiateModelParams instantiateModelParams = new()
        {
            GeomFlags = CharacterModelDescription.ModelGeomFlags, 
            MaxDistance = 200f
        };

        _model = await I.Get<ModelCache>().LoadModel( 
            new ModelCacheParams() {
            Url = CharacterModelDescription.ModelUrl,
            Params = instantiateModelParams});

        Vector3 v3Person = StartPosition;
        Quaternion qPerson = StartOrientation;

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
                var mapAnimations = _model.MapAnimations;
                if (mapAnimations != null && mapAnimations.Count > 0)
                {
                    if (mapAnimations.TryGetValue(
                            CharacterModelDescription.IdleAnimName, out var animation))
                    {

                        _eAnimations.Set(new AnimationState
                        {
                            ModelAnimation = animation,
                            ModelAnimationFrame = 0
                        });
                        Trace($"Setting up animation {animation.Name}");
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
            float bodyRadius = _model.RootNode.InstanceDesc != null
                ? _model.RootNode.InstanceDesc.AABBTransformed.Radius
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
                uint uintShape = (uint)engine.physics.actions.CreateSphereShape.Execute(
                    _engine.PLog, _engine.Simulation,
                    Single.Max(1.4f, bodyRadius), out var pbody);
                var inertia = pbody.ComputeInertia(MassPerson);
                po = new engine.physics.Object(_engine, _ePerson,
                        v3Person, qPerson, inertia, new TypedIndex() { Packed = uintShape })
                    { CollisionProperties = collisionProperties }.AddContactListener();
                _prefPerson = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
            }

            _ePerson.Set(new engine.physics.components.Body(po, _prefPerson));
            _ePerson.Set(new engine.behave.components.Behavior(new WalkBehavior() { MassTarget = MassPerson }));

            /*
             * Now add an entity as a child that will display in the map
             */
            _eMapPerson = _engine.CreateEntity("RootScene.playership.map");
            I.Get<HierarchyApi>().SetParent(_eMapPerson, _ePerson);
            I.Get<TransformApi>().SetTransforms(_eMapPerson, true,
                nogame.modules.map.Module.MapCameraMask,
                Quaternion.Identity, new Vector3(0f, 0f, 0f));
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


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);

        _engine.Run(_setupPlayer);
    }
}