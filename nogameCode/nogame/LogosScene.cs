using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace nogame
{
    public class LogosScene : engine.IScene
    {
        private object _lo = new();
        engine.Engine _engine;

        private DefaultEcs.World _ecsWorld;

        private engine.hierarchy.API _aHierarchy;
        private engine.transform.API _aTransform;

        private DefaultEcs.Entity _eCamera;
        private DefaultEcs.Entity _eLogo;
        private bool _isCleared = false;

        private float _t;
        public void SceneOnLogicalFrame(float dt)
        {
            float t;
            lock (_lo)
            {
                _t += dt;
                t = _t;
            }
            if (_isCleared)
            {
                if (t > 1.5f)
                {
                    _engine.SetMainScene("root");
                    return;
                }
            }
            else
            {
                if (t < 1.0f)
                {
                    _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 10f + _t));
                    _aTransform.SetRotation(_eLogo, Quaternion.CreateFromAxisAngle(new Vector3(0.1f, 0.9f, 0f), (t - 1f) * 2f * (float)Math.PI / 180f));
                } else
                { 
                     _eLogo.Dispose();
                    _eCamera.Dispose();
                    _isCleared = true;
                }
            }
        }

        public void SceneOnPhysicalFrame(float dt)
        {
            engine.Engine engine = null;
            float t;
            lock(_lo)
            {
                engine = _engine;
            }
            if( null != engine )
            {
            }

        }

        private DefaultEcs.Entity _createLogoBoard()
        {
            Vector2 vSize = new(16f, 16f);
            var jMesh = engine.joyce.mesh.Tools.CreatePlaneMesh(
                vSize,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f));
            var jMaterial = new engine.joyce.Material();
            jMaterial.Texture = new engine.joyce.Texture("assets\\logos\\joyce\\albedo-joyce-engine.png");
            jMaterial.EmissiveTexture = new engine.joyce.Texture("assets\\logos\\joyce\\emissive-joyce-engine.png");
            engine.joyce.InstanceDesc jInstanceDesc = new();
            jInstanceDesc.Meshes.Add(jMesh);
            jInstanceDesc.MeshMaterials.Add(0);
            jInstanceDesc.Materials.Add(jMaterial);            

            var entity = _ecsWorld.CreateEntity();
            entity.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            _aTransform.SetTransforms(
                entity, true, 0xffffffff,
                new Quaternion(0f, 0f, 0f, 1f),
                new Vector3(0f, 0f, 0f));
            return entity;

        }

        public void SceneDeactivate()
        {
            engine.Engine engine = null;
            lock (_lo)
            {
                engine = _engine;
                _engine = null;
                _ecsWorld = null;
                _aHierarchy = null;
                _aTransform = null;
            }

            /*
             * Null out everything we don't need when the scene is unloaded.
             */
            engine.RemoveScene(this);
        }

        public void SceneActivate(engine.Engine engine0)
        {
            lock(_lo)
            {
                _engine = engine0;

                /*
                 * Some local shortcuts
                 */
                _ecsWorld = _engine.GetEcsWorld();
                _aHierarchy = _engine.GetAHierarchy();
                _aTransform = _engine.GetATransform();

            }

            /*
             * Create a camera.
             */
            {
                _eCamera = _ecsWorld.CreateEntity();
                var cCamera = new engine.joyce.components.Camera3();
                cCamera.Angle = 60.0f;
                cCamera.NearFrustum = 1f;

                /*
                 * We need to be as far away as the skycube is. Plus a bonus.
                 */
                cCamera.FarFrustum = (float)100f;
                cCamera.CameraMask = 0x00000001;
                _eCamera.Set<engine.joyce.components.Camera3>(cCamera);
                _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 10f));
            }
            {
                _eLogo = _createLogoBoard();
            }


            _engine.AddScene(5, this);

        }

        public LogosScene()
        {
        }
    }
}