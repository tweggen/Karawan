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
using engine.world;
using nogame.modules.osd;
using static engine.Logger;

namespace nogame.modules.playerhover;

public class HoverModule : AModule, IInputPart
{
    public static float MY_Z_ORDER = 25f;

    static public readonly string PhysicsName = "nogame.playerhover.ship";

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<InputEventPipeline>(),
        new SharedModule<PlayerPosition>(),
        new MyModule<UpdateEmissionContext>(),
        new MyModule<DriveCarCollisionsController>(),
        new MyModule<HoverTouchButton>()
    };

    private DefaultEcs.Entity _eShip;
    private DefaultEcs.Entity _eAnimations;
    private BepuPhysics.BodyReference _prefShip;
    private Entity _eMapShip;

    private TransformApi _aTransform;
    
    private Model _model;
    
    public float MassShip { get; set; } = 500f;

    
#if true
    public string AnimName { get; set; } = "";
    public string ModelUrl { get; set; } = "car6.obj";
    public int ModelGeomFlags { get; set; } = 0
                                              | InstantiateModelParams.CENTER_X
                                              | InstantiateModelParams.CENTER_Z
                                              | InstantiateModelParams.ROTATE_Y180
                                              | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC
                                              ;
#else
    public string AnimName { get; set; } = "Walk_Loop";
    public string ModelUrl { get; set; } = "player.glb";
    public int ModelGeomFlags { get; set; } = 0
        ;
#endif
    
    /**
      * Sound API
      */
    private Boom.ISoundAPI _aSound;
    private Boom.ISound _soundMyEngine = null;

    private bool _isMyEnginePlaying = false;
    private void _updateSound(in Vector3 velShip)
    {

        float vel = Single.Clamp(velShip.Length(), 0f, 200f) / 256f;
        if (vel < 0.05f)
        {
            if (_isMyEnginePlaying)
            {
                _soundMyEngine.Stop();
                _isMyEnginePlaying = false;
            }

            _soundMyEngine.Volume = 0f;
            _soundMyEngine.Speed = 0.8f;
        }
        else
        {
            if ((_aSound.SoundMask & 0x00000001) != 0)
            {
                _soundMyEngine.Speed = 0.1f + vel * 4f;
                float vol = Single.Clamp(0.1f + vel * 3.0f, 0f, 1f);
                _soundMyEngine.Volume = 0.2f * vol;

                if (!_isMyEnginePlaying)
                {
                    _isMyEnginePlaying = true;
                    _soundMyEngine.Play();
                }
            }
        }
    }


    public void InputPartOnInputEvent(Event ev)
    {
        if (ev.Type != Event.INPUT_BUTTON_PRESSED)
        {
            return;
        }

        if (ev.Code == "<change>")
        {
            ev.IsHandled = true;
            I.Get<EventQueue>().Push(new Event(MainPlayModule.EventCodeGetOutOfHover, ""));
        }
    }

    
    private void _onLogicalFrame(object? sender, float dt)
    {
        if (!_eShip.Has<Transform3ToWorld>()) return;
        Vector3 velShip = _prefShip.Velocity.Linear;

        
        /*
         * Adjust the sound pitch.
         */
        _updateSound(velShip);
    }


    private void _stopHoverSound()
    {
        if (_isMyEnginePlaying)
        {
            _soundMyEngine.Stop();
            _isMyEnginePlaying = false;
        }
        _soundMyEngine.Volume = 0f;
        _soundMyEngine.Speed = 0.8f;
        _soundMyEngine.Dispose();
        _soundMyEngine = null;
    }

    
    private void _cleanupPlayer()
    {
        _engine.Player.Value = default;
        I.Get<HierarchyApi>().Delete(ref _eShip);
        
        I.Get<EventQueue>().Push(new Event(MainPlayModule.EventCodeIsHoverDeactivated, ""));
    }
    
    
    protected override void OnModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);
        _engine.OnLogicalFrame -= _onLogicalFrame;

        _stopHoverSound();

        _engine.QueueMainThreadAction(_cleanupPlayer);
    }


    private async Task _setupPlayer()
    {
        _aTransform = I.Get<engine.joyce.TransformApi>();

        _aSound = I.Get<Boom.ISoundAPI>();

        {
            _soundMyEngine = _aSound.FindSound("sd_my_engine.ogg");
            _soundMyEngine.Volume = 0f;
            _soundMyEngine.IsLooped = true;
            _soundMyEngine.Speed = 0.81f;
            _soundMyEngine.SoundMask = 0xffffffff;
        }


        InstantiateModelParams instantiateModelParams = new() { GeomFlags = ModelGeomFlags, MaxDistance = 200f };

        _model = await I.Get<ModelCache>().LoadModel( 
            new ModelCacheParams() {
            Url = ModelUrl,
            Params = instantiateModelParams});

        /*
         * Read the current position.
         * Note, that we need to apply the player's position to the entity for
         * the walking figure, because it is kinematic as opposed to the ship,
         * that is dynamic, and thus needs the position on the physics.
         */
        M<PlayerPosition>().GetPlayerPosition(out var v3Ship, out var qShip);
        
        /*
         * Create the ship entiiies. This needs to run in logical thread.
         */
        _engine.QueueMainThreadAction(() =>
        {
            _eShip = _engine.CreateEntity("RootScene.playership");
            
            /*
             * Note that this position is transient, it is for the initial display only,
             * the position will be read from the physics and applied to the model.
             */
            _aTransform.SetPosition(_eShip, v3Ship);
            _aTransform.SetRotation(_eShip, qShip);
            _aTransform.SetVisible(_eShip, engine.GlobalSettings.Get("nogame.PlayerVisible") != "false");
            _aTransform.SetCameraMask(_eShip, 0x0000ffff);

            {
                builtin.tools.ModelBuilder modelBuilder = new(_engine, _model, instantiateModelParams);
                modelBuilder.BuildEntity(_eShip);
                _eAnimations = modelBuilder.GetAnimationsEntity();
            }

            if (default != _eAnimations)
            {
                var mapAnimations = _model.MapAnimations;
                if (mapAnimations != null && mapAnimations.Count > 0)
                {
                    if (mapAnimations.TryGetValue(AnimName, out var animation))
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
                        Trace($"Test animation {AnimName} not found.");
                    }
                        
                }
            }

            _eShip.Set(new engine.joyce.components.PointLight(
                new Vector3(0f, 0f, -1f),
                new Vector4(1.0f, 0.95f, 0.9f, 1f),
                10f, 0.9f));
            #if false
            _eShip.Set(
                new engine.gongzuo.components.LuaScript(
                    new LuaScriptEntry()
                    {
                        LuaScript = "print(\"Script successfully has been loaded.\")"
                    }));
            #endif

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
                    Entity = _eShip,
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
                uint uintShape = (uint)engine.physics.actions.CreateCylinderShape.Execute(
                    _engine.PLog, _engine.Simulation,
                    Single.Max(1.4f, bodyRadius), 
                    _model.RootNode.InstanceDesc != null 
                        ? _model.RootNode.InstanceDesc.AABBTransformed.BB.Y-_model.RootNode.InstanceDesc.AABBTransformed.BB.Y
                        : 1.0f,
                    out var pbody);
                var inertia = pbody.ComputeInertia(MassShip);
                po = new engine.physics.Object(_engine, _eShip, 
                        inertia, new TypedIndex() { Packed = uintShape },
                        v3Ship, qShip)
                    { CollisionProperties = collisionProperties }.AddContactListener();
                _prefShip = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
                /*
                 * Now actually apply the position to the ship.
                 */
                _prefShip.Velocity.Linear = new Vector3(0f, 0f, 0f);
                _prefShip.Pose.Position = v3Ship;
                _prefShip.Pose.Orientation = qShip;
                _prefShip.Velocity.Angular = new Vector3(0f, 0f, 0f);
            }

            _eShip.Set(new engine.physics.components.Body(po, _prefShip));
            _eShip.Set(new engine.behave.components.Behavior(new HoverBehavior() { MassTarget = MassShip }));

            /*
             * Now add an entity as a child that will display in the map
             */
            _eMapShip = _engine.CreateEntity("RootScene.playership.map");
            I.Get<HierarchyApi>().SetParent(_eMapShip, _eShip);
            I.Get<TransformApi>().SetTransforms(_eMapShip, true,
                nogame.modules.map.Module.MapCameraMask,
                Quaternion.Identity, new Vector3(0f, 0f, 0f));
            _eMapShip.Set(new engine.world.components.MapIcon()
                { Code = engine.world.components.MapIcon.IconCode.Player0 });

            _engine.OnLogicalFrame += _onLogicalFrame;

            _engine.Player.Value = _eShip;

            M<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);

            /*
             * Finally, we are boarded.
             */
            I.Get<EventQueue>().Push(new Event(MainPlayModule.EventCodeIsHoverActivated, ""));
        }); // End of queue mainthread action.
    }


    protected override void OnModuleActivate()
    {
        _engine.Run(_setupPlayer);
    }
}