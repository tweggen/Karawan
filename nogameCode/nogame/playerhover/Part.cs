using BepuPhysics;
using BepuPhysics.Collidables;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace nogame.playerhover
{
    internal class Part : engine.IPart
    {
        private object _lock = new object();

        private engine.Engine _engine;
        private engine.IScene _scene;

        private DefaultEcs.World _ecsWorld;
        private engine.transform.API _aTransform;

        private DefaultEcs.Entity _eShip;
        private BepuPhysics.BodyHandle _phandleShip;
        private BepuPhysics.BodyReference _prefShip;

        private WASDPhysics _controllerWASDPhysics;

        private const float _massShip = 10f;

        private void _onContactInfo(object eventSource, engine.physics.ContactInfo contactInfo)
        {
            Console.WriteLine( $"ship reference is {_prefShip.Handle}, contactEventSource is {contactInfo.EventSource}, pair is {contactInfo.ContactPair}" );
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
                _eShip = _ecsWorld.CreateEntity();
                var posShip = new Vector3(0f, 35f, 0f);
                _aTransform.SetPosition(_eShip, posShip);
                _aTransform.SetVisible(_eShip, engine.GlobalSettings.Get("nogame.PlayerVisible") != "false");
                _aTransform.SetCameraMask(_eShip, 0xffffffff);
                
                engine.joyce.InstanceDesc jInstanceDesc = builtin.loader.Obj.LoadModelInstance("car2.obj");
                jInstanceDesc.ModelTransform = Matrix4x4.CreateRotationY((float)Math.PI);

                _eShip.Set(new engine.joyce.components.Instance3(jInstanceDesc));
                //_eShip.Set(new engine.joyce.components.PointLight(new Vector4(1.0f, 0.95f, 0.9f, 1f)/3f));

                var pbodySphere = new BepuPhysics.Collidables.Sphere(1.4f);
                var pinertiaSphere = pbodySphere.ComputeInertia(_massShip);

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
                _eShip.Set(new engine.physics.components.Body(_prefShip));
                /*
                 * Activate collision detection for ship.
                 */
                _engine.AddContactListener(_eShip);
            }

            /*
             * And the ship's controller
             */
            _controllerWASDPhysics = new WASDPhysics(_engine, _eShip, _massShip);
            _controllerWASDPhysics.ActivateController();

            _engine.OnContactInfo += _onContactInfo;
            _engine.AddPart(0, scene0, this);
        }

        public Part() { }
    }
}
