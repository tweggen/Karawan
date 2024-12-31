using BepuPhysics;
using BepuPhysics.Collidables;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.draw;
using engine.gongzuo;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;
using engine.world;
using static engine.Logger;

namespace nogame.modules.playerhover;

public class Module : engine.AModule
{
    static public readonly string PhysicsName = "nogame.playerhover";

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>(),
        new MyModule<nogame.modules.playerhover.UpdateEmissionContext>()
    };


    private engine.joyce.TransformApi _aTransform;

    private DefaultEcs.Entity _eCamera;

    private DateTime _timestampFacingObject = DateTime.Now;
    private string _strFacingObject = "";
    private CollisionProperties _cpFacingObject = null;

    private DefaultEcs.Entity _eShip;
    private BepuPhysics.BodyReference _prefShip;
    private Entity _eMapShip;

    private PlayerViewer _playerViewer;
    
    /**
     * Display the current cluster name.
     */
    private DefaultEcs.Entity _eClusterDisplay;

    /**
     * Display the object we are facing.
     */
    private DefaultEcs.Entity _eTargetDisplay;

    /**
     * Sound API
     */
    private Boom.ISoundAPI _aSound;
    
    private PlingPlayer _plingPlayer = new();
    private Boom.ISound _polyballSound;

    public float MassShip { get; set; } = 500f;

    public string ModelUrl { get; set; } = "car6.obj";
    public int ModelGeomFlags { get; set; } = 0
                                              | InstantiateModelParams.CENTER_X
                                              | InstantiateModelParams.CENTER_Z
                                              | InstantiateModelParams.ROTATE_Y180
                                              | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC;

    private ClusterDesc _currentCluster = null;

    private Boom.ISound _soundCrash = null;
    private Boom.ISound _soundMyEngine = null;


    private string _getClusterSound(ClusterDesc clusterDesc)
    {
        if (null == clusterDesc)
        {
            return "lvl-6.ogg";
        }
        else
        {
            if (clusterDesc.Pos.Length() > 200)
            {
                return "lvl-1-01c.ogg";
            }
            else
            {
                return "shaklengokhsi.ogg";
            }
        }
    }


    private void _decreaseHealth(int less)
    {
        lock (_lo)
        {
            var gameState = M<AutoSave>().GameState;
            gameState.Health = int.Max(0, gameState.Health - less);
        }
    }


    private void _playCollisionSound()
    {
        _soundCrash.Stop();
        _soundCrash.Volume = 0.1f;
        _soundCrash.Play();
    }


    private void _createCollisionParticles(ContactEvent cev)
    {
        _engine.QueueEntitySetupAction("carcollision", e =>
        {
            var jFountainCubesInstanceDesc = InstanceDesc.CreateFromMatMesh(
                new MatMesh(
                    I.Get<ObjectRegistry<Material>>().Get("nogame.characters.polytope.materials.cube"),
                    engine.joyce.mesh.Tools.CreatePlaneMesh("carcrashfragments", new Vector2(0.1f,0.1f))
                ), 20f
            );
            Vector3 v3Pos;
            lock (_engine.Simulation)
            {
                v3Pos = _prefShip.Pose.Position;
            }

            v3Pos += cev.ContactInfo.ContactOffset;
            e.Set(new engine.behave.components.ParticleEmitter()
            {
                Position = Vector3.Zero,
                ScalePerSec = 1f,
                RandomPos = Vector3.One,
                EmitterTimeToLive = 10,
                Velocity = 3f * cev.ContactInfo.ContactNormal,
                ParticleTimeToLive = 30,
                InstanceDesc = jFountainCubesInstanceDesc,
                RandomDirection = 0.5f,
                MaxDistance = 20f,
                CameraMask = 0x00000001,
            });
            e.Set(new engine.joyce.components.Transform3ToWorld()
                {
                    Matrix = Matrix4x4.CreateTranslation(v3Pos),
                    CameraMask = 0x00000001,
                    IsVisible = true
                }
            );

        });
    }
    

    private void _onAnonymousCollision(engine.news.Event ev)
    {
        var cev = ev as ContactEvent;
        _createCollisionParticles(cev);
        _playCollisionSound();
        _decreaseHealth(14);
    }


    private void _onPolytopeCollision(engine.news.Event ev)
    {
        var cev = ev as ContactEvent;
        cev.ContactInfo.PropertiesB.Entity.Set(
            new engine.behave.components.Behavior(new nogame.cities.PolytopeVanishBehaviour() { Engine = _engine }));
        var gameState = M<AutoSave>().GameState;
        gameState.NumberPolytopes++;
        gameState.Health = 1000;

        _polyballSound.Stop();
        _polyballSound.Play();
    }


    private void _onCubeCollision(engine.news.Event ev)
    {
        var cev = ev as ContactEvent;
        _createCollisionParticles(cev);

        cev.ContactInfo.PropertiesB.Entity.Set(
            new engine.behave.components.Behavior(new nogame.characters.cubes.CubeVanishBehavior()
                { Engine = _engine }));

        _plingPlayer.PlayPling();
        _plingPlayer.Next();

        var gameState = M<AutoSave>().GameState;
        gameState.NumberCubes++;
    }


    private void _onCarCollision(engine.news.Event ev)
    {
        var cev = ev as ContactEvent;
        _createCollisionParticles(cev);

        var other = cev.ContactInfo.PropertiesB;

        _playCollisionSound();
        _decreaseHealth(17);
    }


    public DefaultEcs.Entity GetShipEntity()
    {
        return _eShip;
    }


    private void _onCameraEntityChanged(DefaultEcs.Entity entity)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (_eCamera != entity)
            {
                _eCamera = entity;
                isChanged = true;
            }
        }
    }


    private void _onCenterRayHit(
        CollidableReference collidableReference,
        CollisionProperties collisionProperties,
        float t,
        Vector3 vNormal)
    {
        if (null != collisionProperties)
        {
            lock (_lo)
            {
                _timestampFacingObject = DateTime.Now;
                if (_cpFacingObject != collisionProperties)
                {
                    _cpFacingObject = collisionProperties;
                    _strFacingObject =
                        $"{collisionProperties.Entity} {collisionProperties.Name} ({collisionProperties.DebugInfo})";
                }
            }
        }
    }


    private void _testResetFacingObject()
    {
        lock (_lo)
        {
            if (_cpFacingObject != null)
            {
                if ((DateTime.Now - _timestampFacingObject).TotalMilliseconds > 1000)
                {
                    _cpFacingObject = null;
                    _strFacingObject = "";
                }
            }
        }
    }


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
    

    private void _onLogicalFrame(object? sender, float dt)
    {
        if (!_eShip.Has<Transform3ToWorld>()) return;
        Matrix4x4 mShip = _eShip.Get<Transform3ToWorld>().Matrix;
        Vector3 velShip = _prefShip.Velocity.Linear;
        Vector3 posShip = mShip.Translation;

        
        /*
         * Look up the object we are facing.
         */
        if (false) {
            if (_eCamera.IsAlive)
            {
                if (_eCamera.Has<engine.joyce.components.Transform3ToWorld>() &&
                    _eCamera.Has<engine.joyce.components.Camera3>())
                {
                    var cCamTransform = _eCamera.Get<engine.joyce.components.Transform3ToWorld>();
                    var cCamera = _eCamera.Get<engine.joyce.components.Camera3>();
                    var mCameraToWorld = cCamTransform.Matrix;
                    Vector3 vZ = new Vector3(mCameraToWorld.M31, mCameraToWorld.M32, mCameraToWorld.M33);
                    var vCamPosition = mCameraToWorld.Translation;

                    I.Get<engine.physics.API>().RayCast(vCamPosition, -vZ, 200f, _onCenterRayHit);
                }
            }

            _testResetFacingObject();
        }

        /*
         * Look up the zone we are in.
         */
        bool newZone = false;
        ClusterDesc foundCluster = I.Get<ClusterList>().GetClusterAt(posShip);
        if (foundCluster != null)
        {
            if (_currentCluster != foundCluster)
            {
                /*
                 * We entered a new cluster. Trigger cluster song.
                 */

                /*
                 * Remember new cluster.
                 */
                _currentCluster = foundCluster;
                newZone = true;
            }
        }
        else
        {
            if (_currentCluster != null)
            {
                /*
                 * We just left a cluster. Trigger void music.
                 */

                /*
                 * Remember we are outside.
                 */
                _currentCluster = null;
                newZone = true;
            }
        }

        string displayName;
        if (_currentCluster != null)
        {
            displayName = $"{_currentCluster.Name}";
        }
        else
        {
            displayName = "void";
        }

        if (false) {
            string strFacingObject;
            lock (_lo)
            {
                strFacingObject = _strFacingObject;
            }

            float width = 320f;
            _eTargetDisplay.Set(new engine.draw.components.OSDText(
                new Vector2((786f - width) / 2f, 360f),
                new Vector2(width, 18f),
                $"{strFacingObject}",
                10,
                0xff22aaee,
                0x00000000,
                HAlign.Center));
        }

        if (newZone)
        {
            _eClusterDisplay.Set(new engine.draw.components.OSDText(
                new Vector2(768f/2f - 64f - 48f - 96f, 48f),
                new Vector2(96f, 18f),
                $"{displayName}",
                10,
                0xff448822,
                0x00000000,
                HAlign.Right));


            I.Get<Boom.Jukebox>().LoadThenPlaySong(
                _getClusterSound(_currentCluster), 0.05f, true, () => { }, () => { });
        }

        /*
         * Adjust the sound pitch.
         */
        _updateSound(velShip);

        var gameState = M<AutoSave>().GameState;

    }


    /**
     * Find and return a suitable start position for the player.
     * We know there is a cluster around 0/0, so find it, and find an estate
     * within without a house build upon it.
     */
    private void _findStartPosition(out Vector3 v3Start, out Quaternion qStart)
    {
        ClusterDesc startCluster = I.Get<ClusterList>().GetClusterAt(Vector3.Zero);
        if (null != startCluster)
        {
            
            startCluster.FindStartPosition(out v3Start, out qStart);
            v3Start += startCluster.Pos;
            Trace($"Startposition is {v3Start} {qStart}");
        }
        else
        {
            v3Start = new Vector3(0f, 200f, 0f);
            qStart = Quaternion.Identity;
            Trace($"No start cluster found, using default startposition is {v3Start} {qStart}");
        }
    }


    public override void Dispose()
    {
        _soundCrash.Dispose();
        _soundCrash = null;
    }


    public override void ModuleDeactivate()
    {
        // TXWTODO: Deactivate player entity. But we don't remove the player entity at all...
        // _engine.SetPlayerEntity(new DefaultEcs.Entity());
        I.Get<MetaGen>().Loader.RemoveViewer(_playerViewer);
        
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _engine.Camera.RemoveOnChange(_onCameraEntityChanged);

        
        I.Get<SubscriptionManager>().Unsubscribe(
            Behavior.PLAYER_COLLISION_ANONYMOUS, _onAnonymousCollision);
        I.Get<SubscriptionManager>().Unsubscribe(
            Behavior.PLAYER_COLLISION_CUBE, _onCubeCollision);
        I.Get<SubscriptionManager>().Unsubscribe(
            Behavior.PLAYER_COLLISION_CAR3, _onCarCollision);
        I.Get<SubscriptionManager>().Unsubscribe(
            Behavior.PLAYER_COLLISION_POLYTOPE, _onPolytopeCollision);

        _engine.RemoveModule(this);

        base.ModuleDeactivate();
    }


    private async Task _setupPlayer()
    {
        _aTransform = I.Get<engine.joyce.TransformApi>();

        _aSound = I.Get<Boom.ISoundAPI>();
        if (null == _soundCrash)
        {
            _soundCrash = _aSound.FindSound($"car-collision.ogg");
        }

        {
            _polyballSound = _aSound.FindSound("polyball.ogg");
            _polyballSound.Volume = 0.03f;
            _soundMyEngine = _aSound.FindSound("sd_my_engine.ogg");
            _soundMyEngine.Volume = 0f;
            _soundMyEngine.IsLooped = true;
            _soundMyEngine.Speed = 0.81f;
            _soundMyEngine.SoundMask = 0xffffffff;
        }


        InstantiateModelParams instantiateModelParams = new() { GeomFlags = ModelGeomFlags, MaxDistance = 200f };

        Model model = await I.Get<ModelCache>().LoadModel( 
            new ModelCacheParams() {
            Url = ModelUrl,
            Params = instantiateModelParams});


        var gameState = M<AutoSave>().GameState;
        Vector3 v3Ship = gameState.PlayerPosition;
        Quaternion qShip = Quaternion.Normalize(gameState.PlayerOrientation);
        if (v3Ship == Vector3.Zero)
        {
            Error($"Unbelievably could not come up with a valid start position, so generate one here.");
            _findStartPosition(out v3Ship, out qShip);
        }

        /*
         * Create the ship entiiies. This needs to run in logical thread.
         */
        _engine.QueueMainThreadAction(() =>
        {
            _eShip = _engine.CreateEntity("RootScene.playership");

            _aTransform.SetPosition(_eShip, v3Ship);
            _aTransform.SetRotation(_eShip, qShip);
            _aTransform.SetVisible(_eShip, engine.GlobalSettings.Get("nogame.PlayerVisible") != "false");
            _aTransform.SetCameraMask(_eShip, 0x0000ffff);

            {
                builtin.tools.ModelBuilder modelBuilder = new(_engine, model, instantiateModelParams);
                modelBuilder.BuildEntity(_eShip);
            }

            _eShip.Set(new engine.joyce.components.PointLight(
                new Vector3(0f, 0f, -1f),
                new Vector4(1.0f, 0.95f, 0.9f, 1f),
                10f, 0.9f));
            _eShip.Set(
                new engine.gongzuo.components.LuaScript(
                    new LuaScriptEntry()
                    {
                        LuaScript = "print(\"Script successfully has been loaded.\")"
                    }));

            /*
             * I have absolutely no clue why, but with the real radius of the model (1.039f) the
             * thing bounces away to nirvana very soon.
             * Therefore we set the previously hard coded 1.4 as a lower limit.
             */
            float bodyRadius = model.RootNode.InstanceDesc != null
                ? model.RootNode.InstanceDesc.AABBTransformed.Radius
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
                uint uintShape = (uint)engine.physics.actions.CreateSphereShape.Execute(
                    _engine.PLog, _engine.Simulation,
                    Single.Max(1.4f, bodyRadius), out var pbody);
                var inertia = pbody.ComputeInertia(MassShip);
                po = new engine.physics.Object(_engine, _eShip,
                        v3Ship, qShip, inertia, new TypedIndex() { Packed = uintShape })
                    { CollisionProperties = collisionProperties }.AddContactListener();
                _prefShip = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
            }

            _eShip.Set(new engine.physics.components.Body(po, _prefShip));
            _eShip.Set(new engine.behave.components.Behavior(new Behavior(MassShip)));

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

            _eClusterDisplay = _engine.CreateEntity("OsdClusterDisplay");
            _eTargetDisplay = _engine.CreateEntity("OsdTargetDisplay");

            if (_engine.Camera.TryGet(out var eCam))
            {
                _onCameraEntityChanged(eCam);
            }

            _engine.Camera.AddOnChange(_onCameraEntityChanged);
            _engine.OnLogicalFrame += _onLogicalFrame;

            _engine.Player.Value = GetShipEntity();

            /*
             * Create a viewer for the player itself, defining what parts
             * of the world shall be loaded.
             */
            _playerViewer = new(_engine);
            I.Get<MetaGen>().Loader.AddViewer(_playerViewer);
        }); // End of queue mainthread action.
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);

        I.Get<SubscriptionManager>().Subscribe(
            Behavior.PLAYER_COLLISION_ANONYMOUS, _onAnonymousCollision);
        I.Get<SubscriptionManager>().Subscribe(
            Behavior.PLAYER_COLLISION_CUBE, _onCubeCollision);
        I.Get<SubscriptionManager>().Subscribe(
            Behavior.PLAYER_COLLISION_CAR3, _onCarCollision);
        I.Get<SubscriptionManager>().Subscribe(
            Behavior.PLAYER_COLLISION_POLYTOPE, _onPolytopeCollision);

        _engine.Run(_setupPlayer);
    }
}