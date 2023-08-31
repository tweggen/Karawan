using BepuPhysics;
using BepuPhysics.Collidables;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using engine;
using engine.draw;
using engine.news;
using engine.physics;
using engine.world;
using static engine.Logger;

namespace nogame.modules.playerhover
{
    public class Module : engine.AModule
    {
        static public readonly string PhysicsName = "nogame.playerhover";
        
        private readonly object _lo = new object();

        private engine.Engine _engine;

        private DefaultEcs.World _ecsWorld;
        private engine.transform.API _aTransform;

        private DefaultEcs.Entity _eCamera;
        private string _strFacingObject = "";
        
        private DefaultEcs.Entity _eShip;
        private BepuPhysics.BodyHandle _phandleShip;
        private BepuPhysics.BodyReference _prefShip;

        /**
         * Display the current pling score.
         */
        private DefaultEcs.Entity _eScoreDisplay;

        /**
         * Display the current cluster name.
         */
        private DefaultEcs.Entity _eClusterDisplay;

        /**
         * Display the object we are facing.
         */
        private DefaultEcs.Entity _eTargetDisplay;
        
        private PlingPlayer _plingPlayer = new();

        private Boom.ISound _polyballSound;

        private WASDPhysics _controllerWASDPhysics;

        private const float MassShip = 500f;


        private ClusterDesc _currentCluster = null;
        
        private int _score = 0;

        private Boom.ISound _soundCrash = null;

        
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
        
        
        private void _nextCubeCollected()
        {
            _plingPlayer.PlayPling();
            _plingPlayer.Next();
            lock (_lo)
            {
                ++_score;
            }
        }


        private void _onContactInfo(engine.news.Event ev)
        {
            var cev = ev as ContactEvent;

            /*
             * If this contact involved us, we store the other contact info in this variable.
             * If the other does not have collision properties, this variable also is empty.
             */
            CollisionProperties other = null;

            // Trace( $"ship reference is {_prefShip.Handle}, contactEventSource is {contactInfo.EventSource}, pair is {contactInfo.ContactPair}" );
            CollisionProperties propsA = cev.ContactInfo.PropertiesA;
            CollisionProperties propsB = cev.ContactInfo.PropertiesB;

            CollisionProperties me = null;

            if (null != propsA)
            {
                Trace($"A: {{ Name: \"{propsA.Name}\" }}");
                if (propsA.Name == PhysicsName)
                {
                    if (propsB != null)
                    {
                        other = propsB;
                    }

                    if (propsA != null)
                    {
                        me = propsA;
                    }
                }
            }

            if (null != propsB)
            {
                Trace($"B: {{ Name: \"{propsB.Name}\" }}");
                if (propsB.Name == PhysicsName)
                {
                    if (propsA != null)
                    {
                        other = propsA;
                    }

                    if (propsB != null)
                    {
                        me = propsB;
                    }
                }
            }

            bool playSound = true;
            if (other != null)
            {
                /*
                 * Now let's check for explicit other components.
                 */
                if (other.Name == nogame.characters.cubes.GenerateCharacterOperator.PhysicsName)
                {
                    // Trace($"Cube chrIdx {other.DebugInfo}");
                    _nextCubeCollected();
                    _engine.AddDoomedEntity(other.Entity);
                    playSound = false;
                }
                else if (other.Name == "nogame.furniture.polytopeBall")
                {
                    // Trace($"Polyball chrIdx {other.DebugInfo}");
                    _engine.QueueMainThreadAction(() =>
                    {
                        other.Entity.Set(new engine.behave.components.Behavior(new nogame.cities.PolytopeVanishBehaviour()));
                    });
                    playSound = false;
                    _polyballSound.Stop();
                    _polyballSound.Play();
                }
            }
            if (playSound) 
            {
                _soundCrash.Stop();
                _soundCrash.Volume = 0.1f;
                _soundCrash.Play();
            }
        }
    

        public DefaultEcs.Entity GetShipEntity()
        {
            return _eShip;
        }
        

        private void _onCameraEntityChanged(object? sender, DefaultEcs.Entity entity)
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
            Vector3 vNormal)
        {
            if (null != collisionProperties)
            {
                lock (_lo)
                {
                    _strFacingObject = $"{collisionProperties.Name} ({collisionProperties.DebugInfo})";
                }
            }
            else
            {
                lock (_lo)
                {
                    _strFacingObject = $"(nothing)";
                }
            }
        }
        
        
        private void _onLogicalFrame(object? sender, float dt)
        {
            Matrix4x4 mShip = _eShip.Get<engine.transform.components.Transform3ToWorld>().Matrix;
            Vector3 posShip = mShip.Translation;

            /*
             * Look up the object we are facing.
             */
            {
                if (_eCamera.IsAlive)
                {
                    if (_eCamera.Has<engine.transform.components.Transform3ToWorld>() && _eCamera.Has<engine.joyce.components.Camera3>())
                    {
                        var cCamTransform = _eCamera.Get<engine.transform.components.Transform3ToWorld>();
                        var cCamera = _eCamera.Get<engine.joyce.components.Camera3>();
                        var mCameraToWorld = cCamTransform.Matrix;
                        Vector3 vZ = new Vector3(mCameraToWorld.M31, mCameraToWorld.M32, mCameraToWorld.M33);
                        var vCamPosition = mCameraToWorld.Translation;

                        Implementations.Get<engine.physics.API>().RayCast(vCamPosition, -vZ, 200f, _onCenterRayHit);
                    }
                }
            }
            
            /*
             * Look up the zone we are in. 
             */
            bool newZone = false;
            ClusterDesc foundCluster = ClusterList.Instance().GetClusterAt(posShip);
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

            {
                string strFacingObject;
                lock (_lo)
                {
                    strFacingObject = _strFacingObject;
                }
                _eTargetDisplay.Set(new engine.draw.components.OSDText(
                    new Vector2((786f-160f)/2f, 360f),
                    new Vector2(160f, 18f),
                    $"{strFacingObject}",
                    12,
                    0xff22aaee,
                    0x00000000,
                    HAlign.Left));
            }
            
            if (newZone)
            {
                _eClusterDisplay.Set(new engine.draw.components.OSDText(
                    new Vector2(786f-160f-32f, 360f),
                    new Vector2(160f, 18f),
                    $"{displayName}",
                    16,
                    0xff22aaee,
                    0x00000000,
                    HAlign.Right));
                

                Implementations.Get<Boom.Jukebox>().LoadThenPlaySong(
                    _getClusterSound(_currentCluster), 0.05f, true, () => {}, () => {});
            }

            _eScoreDisplay.Set(new engine.draw.components.OSDText(
                new Vector2(786f-64f-32f, 48f),
                new Vector2(64f, 40f),
                $"{_score}",
                40,
                0xff22aaee,
                0x00000000,
                HAlign.Right
            ));
        }


        /**
         * Find and return a suitable start position for the player.
         * We know there is a cluster around 0/0, so find it, and find an estate
         * within without a house build upon it.
         */
        private Vector3 _findStartPosition()
        {
            ClusterDesc startCluster = ClusterList.Instance().GetClusterAt(Vector3.Zero);
            if (null != startCluster)
            {
                return startCluster.FindStartPosition();
            } 
            return new Vector3(0f, 200f, 0f);
        }



        public void Dispose()
        {
            _soundCrash.Dispose();
            _soundCrash = null;
        }
        

        public void ModuleDeactivate()
        {
            _engine.OnLogicalFrame -= _onLogicalFrame;
            _engine.OnCameraEntityChanged -= _onCameraEntityChanged;

            Implementations.Get<SubscriptionManager>().Unsubscribe(ContactEvent.PHYSICS_CONTACT_INFO, _onContactInfo);
            _controllerWASDPhysics.ModuleDeactivate();
            _engine.RemoveModule(this);

            lock (_lo)
            {
                _engine = null;
            }
        }


        public void ModuleActivate(engine.Engine engine0)
        {
            lock (_lo)
            {
                _engine = engine0;
                _ecsWorld = _engine.GetEcsWorld();
                _aTransform = Implementations.Get<engine.transform.API>();
            }

            if (null == _soundCrash)
            {
                var soundApi = Implementations.Get<Boom.ISoundAPI>();
                _soundCrash = soundApi.FindSound($"car-collision.ogg");
            }

            /*
             * Create a ship
             */
            {
                _eShip = _engine.CreateEntity("RootScene.playership");
                var posShip = _findStartPosition();
                _aTransform.SetPosition(_eShip, posShip);
                _aTransform.SetVisible(_eShip, engine.GlobalSettings.Get("nogame.PlayerVisible") != "false");
                _aTransform.SetCameraMask(_eShip, 0x0000ffff);

                /*
                 * Heck, why are we async here?
                 */
                Model model = Task.Run(() => ModelCache.Instance().Instantiate(
                    "car6.obj", null,
                    new InstantiateModelParams()
                    {
                        GeomFlags = 0 
                        | InstantiateModelParams.CENTER_X
                        | InstantiateModelParams.CENTER_Z
                        | InstantiateModelParams.ROTATE_Y180
                    })).GetAwaiter().GetResult();

                ModelInfo modelInfo = model.ModelInfo;
                engine.joyce.InstanceDesc jInstanceDesc = model.InstanceDesc;
                Trace($"Player ship modelInfo {modelInfo}");
                
                _eShip.Set(new engine.joyce.components.Instance3(jInstanceDesc));
                _eShip.Set(new engine.joyce.components.PointLight(
                    new Vector3(0f, 0f, -1f),
                    new Vector4(1.0f, 0.95f, 0.9f, 1f),
                    10f, 0.9f));
                _eShip.Set(new engine.world.components.MapIcon()
                    { Code = engine.world.components.MapIcon.IconCode.Player0 });
                
                /*
                 * I have absolutely no clue why, but with the real radius of the model (1.039f) the
                 * thing bounces away to nirvana very soon.
                 * Therefore we set the previously hard coded 1.4 as a lower limit.
                 */
                var pbodySphere = new BepuPhysics.Collidables.Sphere(Single.Max(1.4f, modelInfo.AABB.Radius));
                var pinertiaSphere = pbodySphere.ComputeInertia(MassShip);

                lock (_engine.Simulation)
                {
                    _phandleShip = _engine.Simulation.Bodies.Add(
                        BodyDescription.CreateDynamic(
                            posShip,
                            pinertiaSphere,
                            new BepuPhysics.Collidables.CollidableDescription(
                                _engine.Simulation.Shapes.Add(pbodySphere),
                                0.1f
                            ),
                            new BodyActivityDescription(0.01f)
                        )
                    );
                    _prefShip = _engine.Simulation.Bodies.GetBodyReference(_phandleShip);
                }
                engine.physics.CollisionProperties collisionProperties = 
                    new engine.physics.CollisionProperties
                    { Entity = _eShip, Name = PhysicsName, IsTangible = true };
                Implementations.Get<engine.physics.API>().AddCollisionEntry(_prefShip.Handle, collisionProperties);
                _eShip.Set(new engine.physics.components.Body(_prefShip, collisionProperties));

                /*
                 * Activate collision detection for ship.
                 */
                _engine.AddContactListener(_eShip);
            }

            _eScoreDisplay = _engine.CreateEntity("OsdScoreDisplay");
            _eClusterDisplay = _engine.CreateEntity("OsdClusterDisplay");
            _eTargetDisplay = _engine.CreateEntity("OsdTargetDisplay");
            
            
            /*
             * And the ship's controller
             */
            _controllerWASDPhysics = new WASDPhysics(_eShip, MassShip);
            _controllerWASDPhysics.ModuleActivate(_engine);

            Implementations.Get<SubscriptionManager>().Subscribe(ContactEvent.PHYSICS_CONTACT_INFO, _onContactInfo);
            _engine.AddModule(this);
            _engine.OnLogicalFrame += _onLogicalFrame;
            _onCameraEntityChanged(this, _engine.GetCameraEntity());
            _engine.OnCameraEntityChanged += _onCameraEntityChanged;

            {
                var api = Implementations.Get<Boom.ISoundAPI>();
                _polyballSound = api.FindSound($"polyball.ogg");
                _polyballSound.Volume = 0.1f;
            }
            
        }

        
        public Module()
        {
        }
    }
}
