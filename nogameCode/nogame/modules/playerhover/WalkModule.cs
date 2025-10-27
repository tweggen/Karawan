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
using nogame.characters;
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
    private BepuPhysics.BodyReference _prefRightHand;   
    private Entity _eMapPerson;
    private AnimationState _animStatePerson = new();

    private TransformApi _aTransform;
    
    private Model _model;

    public nogame.characters.CharacterModelDescription CharacterModelDescription{
        get;
        set;
    }
    
    public float MassPerson { get; set; } = 100f;

    /**
      * Sound API
      */
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

    
    private void _cleanupPlayer()
    {
        _engine.Player.Value = default;
        I.Get<HierarchyApi>().Delete(ref _ePerson);
        
        I.Get<EventQueue>().Push(new Event(MainPlayModule.EventCodeIsPersonDeactivated, ""));
    }
    
    
    protected override void OnModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);

        _engine.QueueMainThreadAction(_cleanupPlayer);
    }


    private async Task _setupPlayer()
    {
        try
        {
            _aTransform = I.Get<engine.joyce.TransformApi>();

            /*
             * Read the current position.
             * Note, that we need to apply the player's position to the entity for
             * the walking figure, because it is kinematic as opposed to the ship,
             * that is dynamic, and thus needs the position on the physics.
             */
            M<PlayerPosition>().GetPlayerPosition(out var v3Person, out var qPerson);

            EntityCreator creator = new()
            {
                CharacterModelDescription = CharacterModelDescription,
                Position = v3Person,
                Orientation = qPerson,
                PhysicsName = PhysicsName,
                CreateRightHand = true,
                BehaviorFactory = entity => new WalkBehavior()
                {
                    MassTarget = 200f,
                    CharacterModelDescription = CharacterModelDescription
                },
                CollisionPropertiesFactory = entity => new engine.physics.CollisionProperties
                {
                    Entity = entity,
                    Flags =
                        CollisionProperties.CollisionFlags.IsTangible
                        | CollisionProperties.CollisionFlags.IsDetectable
                        | CollisionProperties.CollisionFlags.TriggersCallbacks,
                    Name = PhysicsName,
                    LayerMask = 0x00ff,
                },
                InstantiateModelParams = new()
                {
                    MaxDistance = 200f
                }
            };
            
            _model = await creator.CreateAsync();
    
            
            _engine.QueueMainThreadAction(() =>
            {
                _ePerson = creator.CreateLogical();
                
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

                if (default != creator.EntityAnimations && _model.MapAnimations != null)
                {
                    var mapAnimations = _model.MapAnimations;
                    if (mapAnimations != null && mapAnimations.Count > 0)
                    {
                        if (mapAnimations.TryGetValue(
                                CharacterModelDescription.IdleAnimName, out var animation))
                        {
                            _animStatePerson.ModelAnimation = animation;
                            _animStatePerson.ModelAnimationFrame = 0;

                            CharacterModelDescription.EntityAnimations.Set(new GPUAnimationState
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