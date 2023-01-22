using System;
using System.Numerics;
using Karawan.engine;

namespace Karawan.nogame
{
    public class RootScene : engine.IScene
    {
        private engine.Engine _engine;

        private DefaultEcs.World _ecsWorld;

        private engine.hierarchy.API _aHierarchy;
        private engine.transform.API _aTransform;

        private systems.CubeSpinnerSystem _cubeSpinnerSystem;

        private int _nCubes;
        private DefaultEcs.Entity[] _eCubes;
        private engine.joyce.Mesh _jMeshCube;
        private Random _rnd;

        private DefaultEcs.Entity _eCubeParent;

        private DefaultEcs.Entity _eCamera;

        private int _testCount;

        public void SceneOnLogicalFrame( float dt )
        {
            /*
             * Advance all cubes
             */
            _cubeSpinnerSystem.Update(dt);

            /*
             * Do test rotation of everything. That's what the eCubeParent is for.
             */
            ++_testCount;
            {
                var qParent = Quaternion.CreateFromAxisAngle(
                    Vector3.Normalize(new Vector3(2.1f, 0.2f, -1.4f)),
                    (50f + _testCount / 2) * (float)Math.PI / 180f);
                _aTransform.SetRotation(_eCubeParent, qParent);
            }

        }


        public void SceneDeactivate()
        {
            /*
             * Null out everything we don't need when the scene is unloaded.
             */
            _engine.RemoveScene(this);
            _rnd = null;
            _cubeSpinnerSystem = null;
        }


        /**
         * Create 1000 entities with
         * - cube mesh, instance etc.
         * - a Scene Specific Spinner to make them turn
         */
        private void _createCubes()
        {
            _nCubes = 1000;
            _eCubes = new DefaultEcs.Entity[_nCubes];
            _jMeshCube = engine.joyce.mesh.Tools.CreateCubeMesh();

            for (int i = 0; i < _nCubes; ++i)
            {
                /*
                 * Create a standard Instance3
                 */
                _eCubes[i] = _ecsWorld.CreateEntity();
                _eCubes[i].Set<engine.joyce.components.Instance3>(
                    new engine.joyce.components.Instance3(_jMeshCube));
                _aTransform.SetPosition(_eCubes[i], 
                    new Vector3(
                        (float) _rnd.NextDouble()*30-15,
                        (float) _rnd.NextDouble()*30-15,
                        (float) _rnd.NextDouble()*30-15));
                _aTransform.SetRotation(
                    _eCubes[i],
                    Quaternion.CreateFromAxisAngle(
                        Vector3.Normalize(
                            new Vector3(
                                (float)_rnd.NextDouble() - 0.5f,
                                (float)_rnd.NextDouble() - 0.5f,
                                (float)_rnd.NextDouble() - 0.5f
                            )
                        ),
                        (float)((_rnd.NextDouble() * 100 - 5) * Math.PI / 180)
                    )
                );
                _aTransform.SetVisible(_eCubes[i], true);
                _aHierarchy.SetParent(_eCubes[i], _eCubeParent);

                /*
                 * Add our own component
                 */
                _eCubes[i].Set<components.CubeSpinner>(
                    new components.CubeSpinner(Quaternion.CreateFromAxisAngle(
                        Vector3.Normalize(
                            new Vector3(
                                (float)_rnd.NextDouble() - 0.5f,
                                (float)_rnd.NextDouble() - 0.5f,
                                (float)_rnd.NextDouble() - 0.5f
                            )
                        ),
                        (float)((_rnd.NextDouble() * 10 - 5) * Math.PI / 180)
                    ))
                );
            }
        }


        public void SceneActivate(engine.Engine engine0)
        {
            _engine = engine0;

            /*
             * Some local shortcuts
             */
            _ecsWorld = _engine.GetEcsWorld();
            _aHierarchy = _engine.GetAHierarchy();
            _aTransform = _engine.GetATransform();

            /*
             * Local state
             */
            _rnd = new Random();
            _cubeSpinnerSystem = new(_engine);

            /*
             * Create one parent that rotates.
             */
            _eCubeParent = _ecsWorld.CreateEntity();
            _aTransform.SetPosition(_eCubeParent, new Vector3(0f, 0f, 0f));
            _aTransform.SetVisible(_eCubeParent, true);
            _createCubes();

            /*
             * Create a camera.
             */
            // TXWTODO: Not really in a scene, right?
            {
                _eCamera = _ecsWorld.CreateEntity();
                var cCamera = new engine.joyce.components.Camera3();
                cCamera.Angle = 60.0f;
                cCamera.NearFrustrum = 1f;
                cCamera.FarFrustrum = 100f;
                _eCamera.Set<engine.joyce.components.Camera3>(cCamera);
                _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 10f));
            }

            _engine.AddScene(this);

        }

        public RootScene()
        {
            _testCount = 0;
        }
    }
}
