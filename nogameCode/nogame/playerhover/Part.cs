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

        public void PartOnLogicalFrame(float dt)
        {

        }


        public void PartDeactivate()
        {
            _engine.RemovePart(this);
            lock (_lock)
            {
                _engine = null;
                _scene = null;
            }
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
                var jShipMesh = engine.joyce.mesh.Tools.CreateCubeMesh(5f);
                _aTransform.SetPosition(_eShip, new Vector3(0f, 35f, 0f));
                _aTransform.SetVisible(_eShip, true);
                var jShipMaterial = new engine.joyce.Material();
                jShipMaterial.AlbedoColor = 0xffeedd00;
                engine.joyce.InstanceDesc jInstanceDesc = new();
                jInstanceDesc.Meshes.Add(jShipMesh);
                jInstanceDesc.MeshMaterials.Add(0);
                jInstanceDesc.Materials.Add(jShipMaterial);
                _eShip.Set<engine.joyce.components.Instance3>(
                    new engine.joyce.components.Instance3(jInstanceDesc));
            }

            _engine.AddPart(0, scene0, this);
        }

        public Part() { }
    }
}
