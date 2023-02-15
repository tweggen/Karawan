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
                var jShipMesh = engine.joyce.mesh.Tools.CreateCubeMesh(2f);
                _aTransform.SetPosition(_eShip, posShip);
                _aTransform.SetVisible(_eShip, true);
                var jShipMaterial = new engine.joyce.Material();
                jShipMaterial.AlbedoColor = 0xffeedd00;
                engine.joyce.InstanceDesc jInstanceDesc = new();
                jInstanceDesc.Meshes.Add(jShipMesh);
                jInstanceDesc.MeshMaterials.Add(0);
                jInstanceDesc.Materials.Add(jShipMaterial);
                _eShip.Set<engine.joyce.components.Instance3>(
                    new engine.joyce.components.Instance3(jInstanceDesc));

                var pbodySphere = new BepuPhysics.Collidables.Sphere(1.4f);
                pbodySphere.ComputeInertia(_massShip, out var pinertiaSphere);
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
