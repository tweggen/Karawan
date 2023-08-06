using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.joyce;
using engine.world;
using engine.streets;
using engine.world.components;
using static engine.Logger;   

namespace nogame.characters.cubes
{
    internal class GenerateCharacterOperator : engine.world.IFragmentOperator
    {
        public static readonly string PhysicsName = "nogame.characters.cube";
        private static object _classLock = new();

        private static engine.audio.Sound _jCubeSound;

        private static engine.audio.Sound _getCubeSound()
        {
            lock (_classLock)
            {
                if (_jCubeSound == null)
                {
                    _jCubeSound = new engine.audio.Sound(
                        "cubeloopmono.ogg", true, 0.6f, 1.0f);
                }

                return _jCubeSound;
            }
        }

        private static float _cubeSize = 0.5f; 
        
        private static engine.joyce.Mesh _jMeshCube;
        private static engine.joyce.Mesh _getCubeMesh()
        {
            lock(_classLock)
            {
                if( null==_jMeshCube)
                {
                    _jMeshCube = engine.joyce.mesh.Tools.CreateCubeMesh("cubecharacter", _cubeSize);
                }
                return _jMeshCube;
            }
        }

        private static BepuPhysics.Collidables.TypedIndex _pshapeSphere;
        private static BepuPhysics.Collidables.Sphere _pbodySphere;
        private static BepuPhysics.Collidables.TypedIndex _getSphereShape(in Engine engine)
        {
            lock(_classLock)
            {
                if( !_pshapeSphere.Exists )
                {
                    _pbodySphere = new(_cubeSize/1.4f);
                    lock (engine.Simulation)
                    {
                        _pshapeSphere = engine.Simulation.Shapes.Add(_pbodySphere);
                    }
                }
                return _pshapeSphere;
            }
        }


        private ClusterDesc _clusterDesc;
        private engine.RandomSource _rnd;
        private string _myKey;

        private bool _trace = false;

        private int _characterIndex = 0;

        public string FragmentOperatorGetPath()
        {
            return $"7001/GenerateCubeCharacterOperatar/{_myKey}/";
        }
        
        
        public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
        {
            _clusterDesc.GetAABB(out aabb);
        }


        public Task FragmentOperatorApply(engine.world.Fragment worldFragment) => new Task(() =>
        {
            var aPhysics = worldFragment.Engine.GetAPhysics();

            float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
            float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

            /*
             * We don't apply the operator if the fragment completely is
             * outside our boundary box (the cluster)
             */
            {
                {
                    float csh = _clusterDesc.Size / 2.0f;
                    float fsh = engine.world.MetaGen.FragmentSize / 2.0f;
                    if (
                        (cx - csh) > (fsh)
                        || (cx + csh) < (-fsh)
                        || (cz - csh) > (fsh)
                        || (cz + csh) < (-fsh)
                    )
                    {
                        // trace( "Too far away: x="+_clusterDesc.x+", z="+_clusterDesc.z);
                        return;
                    }
                }
            }

            if (_trace) Trace($"cluster '{_clusterDesc.Id}' ({_clusterDesc.Pos.X}, {_clusterDesc.Pos.Z}) in range");
            _rnd.Clear();

            /*
             * Now, that we have read the cluster description that is associated, we
             * can place the characters randomly on the streetpoints.
             *
             * TXWTODO: We would be more intersint to place them on the streets.
             */

            var strokeStore = _clusterDesc.StrokeStore();
            IList<StreetPoint> streetPoints = strokeStore.GetStreetPoints();
            if (streetPoints.Count == 0)
            {
                return;
            }

            int l = streetPoints.Count;
            int nCharacters = (int)((float)l * 7f / 5f);

            for (int i = 0; i < nCharacters; i++)
            {

                var idxPoint = (int)(_rnd.GetFloat() * l);
                var idx = 0;
                StreetPoint chosenStreetPoint = null;
                foreach (var sp in streetPoints)
                {
                    if (idx == idxPoint)
                    {
                        chosenStreetPoint = sp;
                        break;
                    }

                    idx++;
                }

                if (null == chosenStreetPoint)
                {
                    Error("chosenStreetPoint must not be null at this point.");
                }

                if (!chosenStreetPoint.HasStrokes())
                {
                    continue;
                }

                /*
                 * Check, wether the given street point really is inside our fragment.
                 * That way, every fragment owns only the characters spawn on their
                 * territory.
                 */
                {
                    float px = chosenStreetPoint.Pos.X + _clusterDesc.Pos.X;
                    float pz = chosenStreetPoint.Pos.Y + _clusterDesc.Pos.Z;
                    if (!worldFragment.IsInside(new Vector2(px, pz)))
                    {
                        // chosenStreetPoint = null;
                        continue;
                    }
                }

                if (_trace)
                {
                    Trace(
                        $"GenerateCubeCharacterOperator(): Starting on streetpoint {idxPoint} {chosenStreetPoint.Pos}.");
                }

                ++_characterIndex;
                {
                    var jInstanceDesc = InstanceDesc.CreateFromMatMesh(new MatMesh(MaterialCache.Get("nogame.characters.cube.materials.cube"), _getCubeMesh()));

                    var wf = worldFragment;


                    var speed = 25f + _rnd.GetFloat() * 15f;
                    int fragmentId = worldFragment.NumericalId;
                    var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
                    {
                        eTarget.Set(new engine.world.components.FragmentId(fragmentId));
                        eTarget.Set(new engine.joyce.components.Instance3(jInstanceDesc));
                        eTarget.Set(new engine.behave.components.Behavior(
                            new Behavior(wf.Engine, _clusterDesc, chosenStreetPoint, speed)));
                        eTarget.Set(new components.CubeSpinner(Quaternion.CreateFromAxisAngle(
                            new Vector3(_rnd.GetFloat() * 2f - 1f, _rnd.GetFloat() * 2f - 1f,
                                _rnd.GetFloat() * 2f - 1f),
                            _rnd.GetFloat() * 2f * (float)Math.PI / 180f)));

                        BodyHandle phandleSphere = wf.Engine.Simulation.Bodies.Add(
                            BodyDescription.CreateKinematic(
                                new Vector3(0f, 0f, 0f), // infinite mass, this is a kinematic object.
                                new BepuPhysics.Collidables.CollidableDescription(
                                    _getSphereShape(wf.Engine),
                                    0.1f),
                                new BodyActivityDescription(0.01f)
                            )
                        );
                        BodyReference prefSphere = wf.Engine.Simulation.Bodies.GetBodyReference(phandleSphere);
                        engine.physics.CollisionProperties collisionProperties =
                            new engine.physics.CollisionProperties
                            {
                                Entity = eTarget, Name = "nogame.characters.cube",
                                DebugInfo = $"_chrIdx {_characterIndex}", IsTangible = false
                            };
                        aPhysics.AddCollisionEntry(prefSphere.Handle, collisionProperties);
                        eTarget.Set(new engine.audio.components.MovingSound(
                            _getCubeSound(), 150f));
                        eTarget.Set(new engine.physics.components.Kinetic(
                            prefSphere, collisionProperties)
                        );
                    });

                    wf.Engine.QueueEntitySetupAction("nogame.characters.cube", tSetupEntity);


                }
            }
        });


        public GenerateCharacterOperator(
            in ClusterDesc clusterDesc, in string strKey)
        {
            _clusterDesc = clusterDesc;
            _myKey = strKey;
            _rnd = new engine.RandomSource(strKey);
            MaterialCache.Register("nogame.characters.cube.materials.cube",
                name => new Material()
                {
                    AlbedoColor = 0xff226666
                });
        }
    }
}
