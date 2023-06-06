using engine.joyce;
using System;
using System.Collections.Generic;
using System.Numerics;
using static engine.Logger;

namespace engine.streets
{
    public class GenerateClusterQuartersOperator : world.IFragmentOperator
    {
        static private object _lock = new();
        static private Material _jMaterialQuarter = null;

        static private Material _getQuarterMaterial()
        {
            lock(_lock)
            {
                if(_jMaterialQuarter == null)
                {
                    _jMaterialQuarter = new Material();
                    _jMaterialQuarter.AlbedoColor = 0xff441144;
                }
                return _jMaterialQuarter;
            }
        }

        private world.ClusterDesc _clusterDesc;
        private engine.RandomSource _rnd;
        private string _myKey;
        private bool _traceQuarters = false;


        public string FragmentOperatorGetPath()
        {
            return $"5010/GenerateClusterQuartersOperator/{_myKey}/";
        }


        private bool _generateQuarterFloor(
            world.Fragment worldFragment,
            streets.Quarter quarter,
            float cx,
            float cy,
            engine.joyce.Mesh g
        )
        {
            // Trace("{worldFragment.getId()}");
            List<Vector2> vpoly = new();
            var delimList = quarter.GetDelims();
            int n = 0;
            foreach(var delim in delimList)
            {
                ++n;
                vpoly.Add(new Vector2(cx + delim.StartPoint.X, cy + delim.StartPoint.Y));
            }
            if(n<3)
            {
                Trace("No delims found");
                return false;
            }
            /*
             * Need to reverse the quarters, they are 
             */
            // vpoly.Reverse();
            engine.joyce.Mesh j2Mesh = engine.joyce.Mesh.CreateListInstance();
            builtin.tools.Triangulate.ToMesh(vpoly, j2Mesh);
            /*
             * Here we have an XY mesh of the triangles in the mesh.
             */
            float h = _clusterDesc.AverageHeight + 2.15f;
            uint i0 = g.GetNextVertexIndex();
            foreach(Vector3 v in j2Mesh.Vertices)
            {
                g.p(new Vector3(v.X, h, v.Y));
                g.UV(0f, 0f);
            }
            for (int k = 0; k < j2Mesh.Indices.Count; k += 3)
            {
                g.Idx(i0+j2Mesh.Indices[k], i0+j2Mesh.Indices[k+1], i0+j2Mesh.Indices[k+2]);
            }

            return true;
        }


        /**
         * Create meshes for all street strokes with their "A" StreetPoint in this fragment.
         */
        public void FragmentOperatorApply(
            in world.Fragment worldFragment)
        {
            // Perform clipping until we have bounding boxes

            /*
             * cx/cz is the position of the cluster relative to the fragment.
             * The geometry is generated relative to the fragment.
             */
            Vector3 c = _clusterDesc.Pos - worldFragment.Position;
            float cx = c.X;
            float cz = c.Z;

            /*
             * We don't apply the operator if the fragment completely is
             * outside our boundary box (the cluster)
             */
            {
                {
                    float csh = _clusterDesc.Size / 2.0f;
                    float fsh = world.MetaGen.FragmentSize / 2.0f;
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

            if (_traceQuarters) Trace( $"Cluster '{_clusterDesc.Name}' ({_clusterDesc.Id}) in range");
#if false
                try
            {
                worldFragment.AddMaterialFactory(
                    "GenerateClusterQuartersOperator._matQuarter", function() {
                    var mat = new engine.Material("");
                    mat.ambientColor = 0x441144;
                    mat.ambient = 0.5;
                    mat.specular = 0.0;
                    return mat;
                }
                    );
            }
            catch (unknown: Dynamic ) {
                trace("Unknown exception: " + Std.string(unknown) + "\n"
                    + haxe.CallStack.toString(haxe.CallStack.callStack())
                    + haxe.CallStack.toString(haxe.CallStack.exceptionStack()));
            }
#endif

            var g = engine.joyce.Mesh.CreateListInstance();

            /*
             * Now iterate through all quarters of this cluster.
             * We only generate quarters that have their centers within this 
             * fragment.
             */
            var quarterStore = _clusterDesc.QuarterStore();
            foreach(var quarter in quarterStore.GetQuarters())
            {
                try
                {
                    /*
                     * Is the quarter part of this fragment?
                     */
                    Vector2 center = quarter.GetCenterPoint();
                    center += new Vector2( _clusterDesc.Pos.X, _clusterDesc.Pos.Z );
                    if (!worldFragment.IsInside(center))
                    {
                        // This is outside, continue;
                        continue;
                    }
                }
                catch (Exception e) {
                    Warning($"Unknown exception: {e}");
                }
                _generateQuarterFloor(worldFragment, quarter, cx, cz, g);
            }

            if (g.IsEmpty())
            {
                if (_traceQuarters) Trace($"Nothing to add at all.");
                return;
            }


            try
            {
                // var mol = new engine.SimpleMolecule( [g] );
                // TXWTODO: This is too inefficient. We should also use a factory here.
                engine.joyce.InstanceDesc instanceDesc = new();
                instanceDesc.Meshes.Add(g);
                instanceDesc.MeshMaterials.Add(0);
                instanceDesc.Materials.Add(_getQuarterMaterial());
                worldFragment.AddStaticMolecule("engine.streets.quarters", instanceDesc);
            }
            catch (Exception e) {
                Warning($"Unknown exception: {e}");
            }

        }


        public GenerateClusterQuartersOperator(
            in world.ClusterDesc clusterDesc,
            string strKey
        )
        {
            _clusterDesc = clusterDesc;
            _myKey = strKey;
            _rnd = new engine.RandomSource(strKey);
        }
    }
}
