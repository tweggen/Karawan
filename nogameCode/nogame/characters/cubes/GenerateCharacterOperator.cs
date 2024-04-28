using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.joyce;
using engine.physics;
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

        private ShapeFactory _shapeFactory = I.Get<ShapeFactory>();

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

        private ClusterDesc _clusterDesc;
        private builtin.tools.RandomSource _rnd;
        private string _myKey;

        private bool _trace = false;

        private int _characterIndex = 0;

        public string FragmentOperatorGetPath()
        {
            return $"7001/GenerateCubeCharacterOperatar/{_myKey}/{_clusterDesc.IdString}";
        }
        
        
        public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
        {
            _clusterDesc.GetAABB(out aabb);
        }


        public Func<Task> FragmentOperatorApply(engine.world.Fragment worldFragment, FragmentVisibility visib) => new (async () =>
        {
            if (0 == (visib.How & engine.world.FragmentVisibility.Visible3dAny))
            {
                return;
            }
        
            var aPhysics = I.Get<engine.physics.API>();

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

            if (_trace) Trace($"cluster '{_clusterDesc.IdString}' ({_clusterDesc.Pos.X}, {_clusterDesc.Pos.Z}) in range");
            _rnd.Clear();

            float propMaxDistance = (float) engine.Props.Get("nogame.characters.cube.maxDistance", 250f); 
            
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
            int nCharacters = (int)((float)l * 2f / 5f);

            for (int i = 0; i < nCharacters; i++)
            {

                var idxPoint = (int)(_rnd.GetFloat() * l);
                StreetPoint chosenStreetPoint = null;
                chosenStreetPoint = streetPoints[idxPoint];
                if (null == chosenStreetPoint || !chosenStreetPoint.HasStrokes())
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
                    var jInstanceDesc = InstanceDesc.CreateFromMatMesh(new MatMesh(I.Get<ObjectRegistry<Material>>().Get("nogame.characters.cube.materials.cube"), _getCubeMesh()), 300f);

                    var wf = worldFragment;


                    var speed = 25f + _rnd.GetFloat() * 15f;
                    int fragmentId = worldFragment.NumericalId;
                    var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
                    {
                        eTarget.Set(new engine.world.components.FragmentId(fragmentId));
                        eTarget.Set(new engine.joyce.components.Instance3(jInstanceDesc));
                        eTarget.Set(new engine.behave.components.Behavior(
                            new Behavior(wf.Engine, _clusterDesc, chosenStreetPoint, speed))
                            { MaxDistance = propMaxDistance }
                        );

                        BodyReference prefSphere;
                        engine.physics.Object po;
                        lock (wf.Engine.Simulation)
                        {
                            po = new(wf.Engine, eTarget, _shapeFactory.GetSphereShape(_cubeSize / 1.4f))
                            {
                                CollisionProperties = new engine.physics.CollisionProperties
                                {
                                    DebugInfo = $"_chrIdx {_characterIndex}",
                                    Entity = eTarget, 
                                    Flags = CollisionProperties.CollisionFlags.IsDetectable,
                                    Name = "nogame.characters.cube",
                                }
                            };
                            prefSphere = wf.Engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
                        }
                        eTarget.Set(new engine.audio.components.MovingSound(
                            _getCubeSound(), 150f));
                        eTarget.Set(new engine.physics.components.Body(po, prefSphere));
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
            _rnd = new builtin.tools.RandomSource(strKey);
            I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.characters.cube.materials.cube",
                name => new Material()
                {
                    Texture = I.Get<TextureCatalogue>().FindColorTexture(0xff226666)
                });
        }

        public static IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
        {
            return new GenerateCharacterOperator(
                (ClusterDesc)p["clusterDesc"],
                (string)p["strKey"]);
        }
    }
}
