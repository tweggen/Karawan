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
    private static readonly engine.Dc _dc = engine.Dc.Animation;

    public required CharacterModelDescription CharacterModelDescription;
    public Vector3 Position = Vector3.Zero;
    public Quaternion Orientation = Quaternion.Identity;
    public required string PhysicsName;
    public engine.world.Fragment Fragment = null;
    public string InitialAnimName = null;
    
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

    /**
     * Output member valid after CreateLogical when CreateRightHand is true.
     * The hand entity is a child of the player entity with a kinematic
     * sphere collider that follows the right-hand bone of the animated model.
     */
    public Entity RightHandEntity { get; private set; }


    private Entity _createLogical()
    {
        try
        {
            /*
             * If true, we placec the character in this function.
             * If false, somebody else (behavior, strategy) will place the character,
             * likely in the very moment we attach the behavior or strategy.
             */
            bool doWePlace = true;
            
            IEntityStrategy? entityStrategy = null;
            
            /*
             * Create a strategy if we have one.
             * But do not set it yet.
             */
            if (default != EntityStrategyFactory)
            {
                try
                {
                    entityStrategy = EntityStrategyFactory(_ePerson);
                    
                    /*
                     * If there is an entity strategy, and it has a position
                     * description, take the initial position from the entity
                     * strategy.
                     */
                    if (entityStrategy != null)
                    {
                        doWePlace = false;
                    }
                }
                catch (Exception e)
                {
                    Warning($"Unable to instantiate entity strategy: {e}");
                }
            }


            if (doWePlace)
            {
                /*
                 * If we are supposed to position the figure, do it right now.
                 */
                ref var v3PlayerPerson = ref Position;
                ref var qPlayerPerson = ref Orientation;

                _aTransform.SetPosition(_ePerson, v3PlayerPerson);
                _aTransform.SetRotation(_ePerson, qPlayerPerson);
                _aTransform.SetVisible(_ePerson, engine.GlobalSettings.Get("nogame.PlayerVisible") != "false");
                _aTransform.SetCameraMask(_ePerson, 0x0000ffff);
            }

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

            /*
             * The animation state is attached UNCONDITIONALLY, and this is the fix for the
             * T-posed NPCs that survived the retry added in #106.
             *
             * All of this used to sit behind `if (default != EntityAnimations)`.
             * EntityAnimations comes from ModelBuilder, which records the first node whose
             * model carries a MapAnimations - so when it comes back default the character
             * got NO GPUAnimationState component and CharacterModelDescription.AnimationState
             * stayed null. The behaviours then waited on `entity.Has<GPUAnimationState>()`
             * forever: #106 made them retry, but there was nothing for the retry to find,
             * which is exactly why that fix did not help.
             *
             * Attaching the component regardless costs nothing when there are no
             * animations - SetAnimation simply keeps returning false - and turns a
             * permanent bind pose into something the per-frame retry can still repair.
             */
            CharacterModelDescription.Model = _model;
            CharacterModelDescription.AnimationState = _animStatePerson;

            if (default != EntityAnimations)
            {
                CharacterModelDescription.EntityAnimations = EntityAnimations;
            }
            else
            {
                /*
                 * Loud, because it is the difference between a character that animates and
                 * one that stands in a T-pose, and until now it was completely silent.
                 */
                Error($"No animations entity for model '{CharacterModelDescription.ModelUrl}' "
                      + $"(pack '{CharacterModelDescription.AnimationPackName ?? "(none)"}'). "
                      + "The character will render in its bind pose - a T-pose - until an "
                      + "animation can be selected.");
            }

            if (!_ePerson.Has<engine.joyce.components.GPUAnimationState>())
            {
                _ePerson.Set(new engine.joyce.components.GPUAnimationState()
                {
                    AnimationState = CharacterModelDescription.AnimationState
                });
            }

            if (InitialAnimName != null)
            {
                // TXWTODO: Maybe we can even do an initial animation setup generically?
                ref var cGpuAnimationState = ref _ePerson.Get<engine.joyce.components.GPUAnimationState>();

                /*
                 * The result is checked, because for some callers this is the ONLY
                 * attempt that will ever be made.
                 *
                 * Callers that also attach a strategy get a behaviour that re-issues the
                 * clip every frame until it takes, and reports through
                 * StuckAnimationReporter when it does not. Callers that pass only an
                 * InitialAnimName - the niceday NPCs and the taxi passenger - have
                 * nothing behind them: if this one call refuses, the character stands in
                 * its bind pose, a T-pose, for its entire life. Silently, until now.
                 *
                 * Unchecking a one-shot exactly like this one is what made the first
                 * T-pose fix (#106) a no-op, twice.
                 */
                bool didSet = true == cGpuAnimationState.AnimationState?.SetAnimation(_model, InitialAnimName);
                if (!didSet)
                {
                    Error($"Unable to select initial animation '{InitialAnimName}' for "
                          + $"'{CharacterModelDescription.ModelUrl}' (pack "
                          + $"'{CharacterModelDescription.AnimationPackName ?? "(none)"}') - "
                          + $"{engine.joyce.AnimationState.DescribeFailure(_model, InitialAnimName)}. "
                          + "Nothing will retry this; the character renders in its bind "
                          + "pose (a T-pose).");
                }
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
                    /*
                     * We place the physics object into the off because the system will
                     * position the kinematic to its world position anyway. 
                     */
                    po = new engine.physics.Object(_engine, _ePerson, new TypedIndex() { Packed = uintShape },
                        engine.physics.Object.OffPosition, Quaternion.Identity, new(0f, personHeight / 2f, 0f))
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
                            // Initial layer mask is 0 (inert). WalkController toggles it
                            // to PlayerMelee only during the punch window.
                            SolidLayerMask = 0,
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
                            engine.physics.Object.OffPosition, Quaternion.Identity)
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

                RightHandEntity = _eRightHand;
            }
            return _ePerson;
        }
        catch (Exception e)
        {
            /*
             * A HALF-BUILT CHARACTER IS LEFT IN THE WORLD, AND THAT IS THE BUG.
             *
             * Everything above runs against _ePerson, which the caller created and still
             * owns - every call site passes one in and ignores the return value. So a
             * throw part way through does not abort a creation, it FREEZES one: the mesh
             * and transform set before the throw stay, and everything after it never
             * happens. No animation state, no physics body, no collision properties, no
             * behaviour, no strategy.
             *
             * That is precisely the reported triple - a T-posed figure with no "E to talk"
             * marker that a car drives straight through - and it was logged as a Warning,
             * one line, with no indication that a character had been left in the scene.
             *
             * It used to be HIDDEN rather than removed, on the grounds that "the entity
             * belongs to the caller and to its fragment Owner, so disposing it from here
             * risks a double dispose". **That reason expired on 2026-08-29**, when
             * engine.DoomedEntitySet made dooming idempotent precisely so that two owners
             * who cannot see each other may both doom the same entity - a fragment tearing
             * down everything it owns while a behaviour on one of them dooms itself is the
             * case it was built for, and this is the same shape.
             *
             * So the half-built character is doomed as well as hidden. Hiding alone left
             * exactly one hole open: SetVisible needs the TransformApi out of the container
             * and takes a ref to a component, so if IT throws - which the inner catch below
             * proves was considered possible - the result is a visible, behaviour-less,
             * physics-less T-pose that stands there until its fragment unloads. Dooming
             * closes it whichever way the hiding goes.
             */
            Error($"Failed to build character '{CharacterModelDescription?.ModelUrl ?? "(no model)"}' "
                  + $"at {Position} (fragment {Fragment?.NumericalId.ToString() ?? "none"}): {e}");

            try
            {
                if (_ePerson != default && _ePerson.IsAlive)
                {
                    I.Get<engine.joyce.TransformApi>().SetVisible(_ePerson, false);
                }
            }
            catch (Exception eHide)
            {
                Error($"...and it could not even be hidden: {eHide.Message}");
            }

            try
            {
                if (_ePerson != default && _ePerson.IsAlive)
                {
                    _engine.AddDoomedEntity(_ePerson);
                }
            }
            catch (Exception eDoom)
            {
                Error($"...and it could not be removed either: {eDoom.Message}");
            }
        }

        return default;
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

            // Resolve AnimationPackName → animation URLs via the registry.
            // Fallback: use AnimationUrls directly if no pack name or if pack lookup fails.
            string? animationUrls = null;
            if (!string.IsNullOrEmpty(CharacterModelDescription.AnimationPackName))
            {
                Trace(_dc, $"EntityCreator: Looking up animation pack '{CharacterModelDescription.AnimationPackName}' for model '{CharacterModelDescription.ModelUrl}'");
                animationUrls = I.Get<engine.joyce.AnimationPackRegistry>().GetPackAnimationUrls(
                    CharacterModelDescription.ModelUrl,
                    CharacterModelDescription.AnimationPackName);
                if (animationUrls == null)
                {
                    Warning($"Animation pack '{CharacterModelDescription.AnimationPackName}' not found for " +
                            $"model '{CharacterModelDescription.ModelUrl}'; falling back to AnimationUrls.");
                    I.Get<engine.joyce.AnimationPackRegistry>().LogAllRegisteredPacks();
                    animationUrls = CharacterModelDescription.AnimationUrls;
                    Trace(_dc, $"EntityCreator: Fallback AnimationUrls = {animationUrls ?? "(null)"}");
                }
                else
                {
                    Trace(_dc, $"EntityCreator: Found animation pack, URLs = {animationUrls}");
                }
            }
            else
            {
                animationUrls = CharacterModelDescription.AnimationUrls;
                Trace(_dc, $"EntityCreator: No AnimationPackName set, using AnimationUrls = {animationUrls ?? "(null)"}");
            }

            if (animationUrls != null)
            {
                ModelCacheParams.Properties.Properties.Add("AnimationUrls", animationUrls);
            }

            if (CharacterModelDescription.ModelBaseBone != null)
            {
                ModelCacheParams.Properties.Properties.Add("ModelBaseBone", CharacterModelDescription.ModelBaseBone);   
            }
            
            _model = await I.Get<ModelCache>().LoadModel(ModelCacheParams);
        }
        catch (Exception e)
        {
            /*
             * Returning null here does not stop anything: the caller goes on to
             * CreateLogical, ModelBuilder's constructor dereferences the model, and the
             * NullReferenceException lands in _createLogical's catch - where it presents
             * as a character that failed to build for no stated reason, two layers from
             * the actual cause. Naming the model here is what makes those two lines
             * readable as one story.
             */
            Error($"Unable to load model '{CharacterModelDescription?.ModelUrl ?? "(none)"}' "
                  + $"(pack '{CharacterModelDescription?.AnimationPackName ?? "(none)"}'): {e}");
        }

        return _model;
    }

}