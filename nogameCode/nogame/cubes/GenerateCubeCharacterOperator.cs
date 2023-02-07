using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.world;
using engine.streets;

namespace nogame.cubes
{
    internal class GenerateCubeCharacterOperator : engine.world.IFragmentOperator
    {
        private static void trace(string message)
        {
            Console.WriteLine(message);
        }

        private static object _classLock = new();
        private static engine.joyce.Material _jMaterialCube;
        private static engine.joyce.Material _getCubeMaterial()
        {
            lock(_classLock)
            {
                if(_jMaterialCube == null)
                {
                    _jMaterialCube = new engine.joyce.Material();
                    _jMaterialCube.AlbedoColor = 0xff00bbee;
                }
                return _jMaterialCube;
            }
        }
        private static engine.joyce.Mesh _jMeshCube;
        private static engine.joyce.Mesh _getCubeMesh()
        {
            lock(_classLock)
            {
                if( null==_jMeshCube)
                {
                    _jMeshCube = engine.joyce.mesh.Tools.CreateCubeMesh(1f);
                }
                return _jMeshCube;
            }
        }




        private ClusterDesc _clusterDesc;
        private engine.RandomSource _rnd;
        private string _myKey;

        private bool _trace = true;

        private int _characterIndex = 0;

        public string FragmentOperatorGetPath()
        {
            return $"7001/GenerateCubeCharacterOperatar/{_myKey}/";
        }
        public void FragmentOperatorApply(in engine.world.Fragment worldFragment)
        {
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

            if (_trace) trace( $"GenerateCubeCharacterOperator(): cluster '{_clusterDesc.Id}' ({_clusterDesc.Pos.X}, {_clusterDesc.Pos.Z}) in range");
            _rnd.clear();

            /*
                * Now, that we have read the cluster description that is associated, we
                * can place the characters randomly on the streetpoints.
                *
                * TXWTODO: We would be more intersint to place them on the streets.
                */

            var strokeStore = _clusterDesc.strokeStore();
            IList<StreetPoint> streetPoints = strokeStore.GetStreetPoints();
            int l = streetPoints.Count;
            int nCharacters = (int)((float)l * 7f / 5f);

            for (int i=0; i<nCharacters; i++)
            {

                var idxPoint = (int)(_rnd.getFloat() * l);
                var idx = 0;
                StreetPoint chosenStreetPoint = null;
                foreach (var sp in streetPoints )
                {
                    if (idx == idxPoint)
                    {
                        chosenStreetPoint = sp;
                        break;
                    }
                    idx++;
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
                        chosenStreetPoint = null;
                    }
                }
                if (null != chosenStreetPoint)
                {
                    if (_trace) trace($"GenerateCubeCharacterOperator(): Starting on streetpoint $idxPoint ${chosenStreetPoint.Pos.X}, ${chosenStreetPoint.Pos.Y}.");

                    ++_characterIndex;
                    {
                        DefaultEcs.Entity eCube;
                        eCube = worldFragment.Engine.GetEcsWorld().CreateEntity();

                        engine.joyce.InstanceDesc jInstanceDesc = new();
                        jInstanceDesc.Meshes.Add(_getCubeMesh());
                        jInstanceDesc.MeshMaterials.Add(0);
                        jInstanceDesc.Materials.Add(_getCubeMaterial());
                        eCube.Set(new engine.joyce.components.Instance3(jInstanceDesc));

                        eCube.Set(new engine.behave.components.Behavior(
                            new CubeBehavior(worldFragment.Engine, _clusterDesc, chosenStreetPoint) ) );

                    }
                }
                else
                {
                    if (_trace) trace("GenerateCubeCharacterOperator(): No streetpoint found.");
                }
            }
        }


        public GenerateCubeCharacterOperator(
            in ClusterDesc clusterDesc, in string strKey)
        {
            _clusterDesc = clusterDesc;
            _myKey = strKey;
            _rnd = new engine.RandomSource(strKey);
        }
    }
}
