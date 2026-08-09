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


    public string AnimName { get; set; } = "";
    public string ModelUrl { get; set; } = "car6.obj";
    public int ModelGeomFlags { get; set; } = 0
                                              | InstantiateModelParams.CENTER_X
                                              | InstantiateModelParams.CENTER_Z
                                              | InstantiateModelParams.ROTATE_Y180
                                              | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC
                                              ;
    
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
            I.Get<EventQueue>().Push(new Event(MainPlayModule.EventTypeGetOutOfHover, ""));
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
        
        I.Get<EventQueue>().Push(new Event(MainPlayModule.EventTypeIsHoverDeactivated, ""));
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


        InstantiateModelParams instantiateModelParams = new() { GeomFlags = ModelGeomFlags, MaxVisibilityDistance = 200f };

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
                var mapAnimations = _model.AnimationCollection.MapAnimations;
                if (mapAnimations != null && mapAnimations.Count > 0)
                {
                    if (mapAnimations.TryGetValue(AnimName, out var animation))
                    {

                        _eAnimations.Set(new GPUAnimationState
                        {
                            AnimationState = new()
                            {
                                ModelAnimation = animation,
                                ModelAnimationFrame = 0
                            }
                        });
                        // Trace($"Setting up animation {animation.Name}");
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
            float bodyRadius = _model.ModelNodeTree.RootNode.InstanceDesc != null
                ? _model.ModelNodeTree.RootNode.InstanceDesc.AABBTransformed.Radius
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
                    SolidLayerMask = CollisionProperties.Layers.PlayerVehicle,
                    SensitiveLayerMask = CollisionProperties.Layers.PlayerSensitive
                };
            engine.physics.Object po;
            lock (_engine.Simulation)
            {
                /*
                 * KNOWN BUG, DELIBERATELY NOT FIXED HERE. Read before "correcting" this.
                 *
                 * The second argument is the cylinder's LENGTH, and the expression below is
                 * BB.Y minus ITSELF - zero, for every model, always. AA is the AABB minimum and
                 * BB its maximum, so the height was meant to be BB.Y - AA.Y; the `: 1.0f`
                 * fallback on the null branch shows a real height was always intended. The
                 * player ship's collision body is therefore a flat disc of zero height.
                 *
                 * It was corrected once (#48) and REVERTED (#49), because with the real model
                 * height the physical system is unstable from the very start - worse than the
                 * disc, not better. Every tuning constant around it (MassShip, the hover forces,
                 * the self-righting gain, the damping) has been tuned for years against a
                 * zero-height body, so changing the shape invalidates all of them at once.
                 *
                 * It is also NOT PROVEN to be the cause of the angular runaway that prompted the
                 * change. The evidence was circumstantial: angular velocity in the hundreds
                 * against a limit of 0.8, recovering within frames of being clamped to zero, and
                 * no "Too fast" warning on the controller's own impulse (limit 500) - consistent
                 * with impulses arriving from the solver rather than from this code, but only
                 * consistent, not demonstrated.
                 *
                 * So: fixing the shape is a physics retune, not a bug fix, and it needs the root
                 * cause established first. The emergency clamp in HoverController keeps the
                 * runaway survivable meanwhile.
                 */
                uint uintShape = (uint)engine.physics.actions.CreateCylinderShape.Execute(
                    _engine.PLog, _engine.Simulation,
                    Single.Max(1.4f, bodyRadius),
                    _model.ModelNodeTree.RootNode.InstanceDesc != null
                        ? _model.ModelNodeTree.RootNode.InstanceDesc.AABBTransformed.BB.Y-_model.ModelNodeTree.RootNode.InstanceDesc.AABBTransformed.BB.Y
                        : 1.0f,
                    out var pbody);

                /*
                 * Diagnostic only - no behaviour depends on it. Prints the dimensions the body is
                 * ACTUALLY built with, next to the model extent it was meant to derive them from,
                 * so the discrepancy above is visible in a log instead of having to be read out of
                 * the source.
                 */
                Trace($"Player ship physics body: radius {Single.Max(1.4f, bodyRadius)}, "
                      + $"height {pbody.Length} (model Y extent "
                      + $"{(_model.ModelNodeTree.RootNode.InstanceDesc != null ? _model.ModelNodeTree.RootNode.InstanceDesc.AABBTransformed.BB.Y - _model.ModelNodeTree.RootNode.InstanceDesc.AABBTransformed.AA.Y : 1.0f)}), "
                      + $"mass {MassShip}.");

                var inertia = pbody.ComputeInertia(MassShip);
                po = new engine.physics.Object(_engine, _eShip, 
                        inertia, new TypedIndex() { Packed = uintShape },
                        v3Ship, qShip)
                    { CollisionProperties = collisionProperties }.AddContactListener();
                _prefShip = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));

                /*
                 * RESTORED, and this time not on a hunch.
                 *
                 * AbiProbe case N FAILED on device (probeRev=15): a constructor prologue
                 * containing ONLY Vector3.Zero and Quaternion.Identity is enough to corrupt
                 * the trailing value-type argument on Mono/ARM64. So the struct passed into
                 * engine.physics.Object and forwarded to CreateDynamic.Execute cannot be
                 * trusted to arrive intact, however carefully that class is written.
                 *
                 * This repair sidesteps argument passing entirely: `inertia` is correct HERE,
                 * in this frame, and the seven values are copied into the live body one
                 * SCALAR at a time. Scalars are not aggregates, so the defect cannot touch
                 * them. It is a workaround for a runtime bug, not a design.
                 *
                 * Removing it during the WP-2.3 cleanup is what brought the angular runaway
                 * back: an indefinite inverse inertia tensor amplifies angular impulses ~442x
                 * and reverses them. Do not remove it again without a device run that shows
                 * the tensor arriving correct WITHOUT it.
                 *
                 * See docs/BUGS/MONO-ARM64-CTOR-PROLOGUE-ARG-CORRUPTION.md.
                 */
                {
                    ref var liShip = ref _prefShip.LocalInertia;
                    liShip.InverseInertiaTensor.XX = inertia.InverseInertiaTensor.XX;
                    liShip.InverseInertiaTensor.YX = inertia.InverseInertiaTensor.YX;
                    liShip.InverseInertiaTensor.YY = inertia.InverseInertiaTensor.YY;
                    liShip.InverseInertiaTensor.ZX = inertia.InverseInertiaTensor.ZX;
                    liShip.InverseInertiaTensor.ZY = inertia.InverseInertiaTensor.ZY;
                    liShip.InverseInertiaTensor.ZZ = inertia.InverseInertiaTensor.ZZ;
                    liShip.InverseMass = inertia.InverseMass;

                    Trace($"Ship inertia repaired scalar-wise: "
                          + $"XX={liShip.InverseInertiaTensor.XX} YY={liShip.InverseInertiaTensor.YY} "
                          + $"ZZ={liShip.InverseInertiaTensor.ZZ} InvMass={liShip.InverseMass} "
                          + $"(off-diagonals YX={liShip.InverseInertiaTensor.YX} "
                          + $"ZX={liShip.InverseInertiaTensor.ZX} ZY={liShip.InverseInertiaTensor.ZY}).");
                }

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
            I.Get<EventQueue>().Push(new Event(MainPlayModule.EventTypeIsHoverActivated, ""));
        }); // End of queue mainthread action.
    }


    protected override void OnModuleActivate()
    {
        _engine.Run(_setupPlayer);
    }
}