using BepuPhysics;
using BepuPhysics.Collidables;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks.Dataflow;
using engine.physics;
using static engine.Logger;

namespace nogame.playerhover
{
    internal class Part : engine.IPart
    {
        static public readonly string PhysicsName = "nogame.playerhover";
        
        private readonly object _lock = new object();

        private engine.Engine _engine;
        private engine.IScene _scene;

        private DefaultEcs.World _ecsWorld;
        private engine.transform.API _aTransform;

        private DefaultEcs.Entity _eShip;
        private BepuPhysics.BodyHandle _phandleShip;
        private BepuPhysics.BodyReference _prefShip;

        private WASDPhysics _controllerWASDPhysics;

        private const float MassShip = 500f;


        private int _plingCounter;
        private static readonly int FirstPling = 1;
        private static readonly int LastPling = 19;


        private void _playPling(int plingCounter)
        {
            string plingName = $"pling{(plingCounter):D2}.ogg";
            _engine.GetASound()?.PlaySound(plingName);
        }
        

        private void _nextCubeCollected()
        {
            int playPling = 0;
            lock (_lock)
            {
                playPling = _plingCounter;
                if (_plingCounter == LastPling)
                {
                    _plingCounter = FirstPling;
                }
                else
                {
                    ++_plingCounter;
                }
            }
            
            _playPling(playPling);
        }

        
        private void _resetPling()
        {
            lock (_lock)
            {
                _plingCounter = FirstPling;
            }
        }

        private void _onContactInfo(object eventSource, engine.physics.ContactInfo contactInfo)
        {
            /*
             * If this contact involved us, we store the other contact info in this variable.
             * If the other does not have collision properties, this variable also is empty.
             */
            CollisionProperties other = null;
            
            Trace( $"ship reference is {_prefShip.Handle}, contactEventSource is {contactInfo.EventSource}, pair is {contactInfo.ContactPair}" );
            CollisionProperties propsA = contactInfo.PropertiesA;
            CollisionProperties propsB = contactInfo.PropertiesB;

            if (null != propsA)
            {
                Trace( $"A: {{ Name: \"{ propsA.Name }\" }}");
                if (propsA.Name == PhysicsName)
                {
                    if (propsB != null)
                    {
                        other = propsB;
                    }
                }
            }
            if (null != propsB)
            {
                Trace( $"B: {{ Name: \"{ propsB.Name }\" }}");
                if (propsB.Name == PhysicsName)
                {
                    if (propsA != null)
                    {
                        other = propsA;
                    }
                }
            }

            if (other == null)
            {
                return;
            }
            
            /*
             * Now let's check for explicit other components.
             */
            if (other.Name == nogame.characters.cubes.GenerateCharacterOperator.PhysicsName)
            {
                Trace($"Cube");
                _nextCubeCollected();
            }
        }

        public void PartDeactivate()
        {
            _controllerWASDPhysics.DeactivateController();
            _engine.RemovePart(this);
            lock (_lock)
            {
                _engine = null;
                _scene = null;
            }
        }


        public DefaultEcs.Entity GetShipEntity()
        {
            return _eShip;
        }


        public void PartActivate(
            in engine.Engine engine0,
            in engine.IScene scene0)
        {
            lock (_lock)
            {
                _engine = engine0;
                _scene = scene0;
                _ecsWorld = _engine.GetEcsWorld();
                _aTransform = _engine.GetATransform();
            }

            /*
             * Create a ship
             */
            {
                _eShip = _engine.CreateEntity("RootScene.playership");
                var posShip = new Vector3(0f, 35f, 0f);
                _aTransform.SetPosition(_eShip, posShip);
                _aTransform.SetVisible(_eShip, engine.GlobalSettings.Get("nogame.PlayerVisible") != "false");
                _aTransform.SetCameraMask(_eShip, 0x0000ffff);
                
                engine.joyce.InstanceDesc jInstanceDesc = builtin.loader.Obj.LoadModelInstance("car2.obj");
                jInstanceDesc.ModelTransform = Matrix4x4.CreateRotationY((float)Math.PI);

                _eShip.Set(new engine.joyce.components.Instance3(jInstanceDesc));
                //_eShip.Set(new engine.joyce.components.PointLight(new Vector4(1.0f, 0.95f, 0.9f, 1f)/3f));

                var pbodySphere = new BepuPhysics.Collidables.Sphere(1.4f);
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
                    { Name = PhysicsName, IsTangible = true };
                _engine.GetAPhysics().AddCollisionEntry(_prefShip.Handle, collisionProperties);
                _eShip.Set(new engine.physics.components.Body(_prefShip, collisionProperties));

                /*
                 * Activate collision detection for ship.
                 */
                _engine.AddContactListener(_eShip);
            }

            _resetPling();
            
            /*
             * And the ship's controller
             */
            _controllerWASDPhysics = new WASDPhysics(_engine, _eShip, MassShip);
            _controllerWASDPhysics.ActivateController();

            _engine.OnContactInfo += _onContactInfo;
            _engine.AddPart(0, scene0, this);
        }

        
        public Part()
        {
        }
    }
}
