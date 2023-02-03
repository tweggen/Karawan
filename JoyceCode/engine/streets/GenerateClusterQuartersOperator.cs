using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace engine.streets
{
    public class GenerateClusterQuartersOperator
    {
        private static void trace(string message) { Console.WriteLine(message);  }
        private world.ClusterDesc _clusterDesc;
        private engine.RandomSource _rnd;
        private string _myKey;
        private bool _traceStreets = false;


        public string FragmentOperatorGetPath()
        {
            return $"5010/GenerateClusterQuartersOperator/{_myKey}/";
        }


        private void _generateQuarterFloor(
            world.Fragment worldFragment,
            streets.Quarter quarter,
            float cx,
            float cy,
            engine.joyce.Mesh g
        )
        {
            // trace('GenerateClusterQuartersOperator.generateQuarterFloor(): ${worldFragment.getId()}');
#if false
            var triPoints: Array<org.poly2tri.Point> = null;
            try {
                /*
                * Now generate the polygon array for triangulation.
                */
                var delimList = quarter.getDelims();
        triPoints = new Array<org.poly2tri.Point>();
                var n = 0;
                for(delim in delimList ) {
                    ++n;
                    triPoints.push( new org.poly2tri.Point(cx+delim.startPoint.x, cy+delim.startPoint.y ) );
                }
if (n < 3)
{
    trace('No delims found');
    return false;
}
        } catch (unknown: Dynamic ) {
    trace("Unknown exception: " + Std.string(unknown) + "\n"
        + haxe.CallStack.toString(haxe.CallStack.callStack())
        + haxe.CallStack.toString(haxe.CallStack.exceptionStack()));
}

try
{
    var vp = new org.poly2tri.VisiblePolygon();
    if (null == vp) trace('vp is null');
    if (null == triPoints) trace('triPoints is null');
    vp.addPolyline(triPoints);
    vp.performTriangulationOnce();

    /*
    * Now generate the actual triangles.
    */
    var i0: Int = g.getNextVertexIndex();
    var vt = vp.getVerticesAndTriangles();
    if (null == vt) trace('vt is null');
    if (_traceStreets) trace(vt);
    var h = _clusterDesc.averageHeight + 2.15;
    if (vt.vertices == null) trace('vt.vertices is null');
    for (i in 0...Std.int(vt.vertices.length / 3) )
    {
        g.p(vt.vertices[i * 3 + 0], h, vt.vertices[i * 3 + 1]);
        g.uv(0., 0. );
    }
    for (i in 0...Std.int(vt.triangles.length / 3) )
    {
        g.idx(i0 + vt.triangles[i * 3 + 1], i0 + vt.triangles[i * 3 + 0], i0 + vt.triangles[i * 3 + 2]);
    }
}
catch (unknown: Dynamic ) {
    trace("Unknown exception: " + Std.string(unknown) + "\n"
        + haxe.CallStack.toString(haxe.CallStack.callStack())
        + haxe.CallStack.toString(haxe.CallStack.exceptionStack()));
    trace(triPoints);
    trace(triPoints.length);
}
#endif
            return;
        }


        /**
         * Create meshes for all street strokes with their "A" StreetPoint in this fragment.
         */
        public void fragmentOperatorApply(
            in All all,
            in world.Fragment worldFragment)
        {
            // Perform clipping until we have bounding boxes

            /*
             * cx/cz is the position of the cluster relative to the fragment.
             * The geometry is generated relative to the fragment.
             */
            Vector3 c = _clusterDesc.Pos - worldFragment.position;
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

            trace( $"GenerateClusterQuartersOperator(): cluster '{_clusterDesc.Name}' ({_clusterDesc.Id}) in range");
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
            var quarterStore = _clusterDesc.quarterStore();
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
                trace($"Unknown exception: {e}");
            }
            _generateQuarterFloor(worldFragment, quarter, cx, cz, g);
            }

            if (g.IsEmpty())
            {
                if (_traceStreets) trace($"GenerateClusterStreetsOperator(): Nothing to add at all.");
                return;
            }


            try
            {
                // var mol = new engine.SimpleMolecule( [g] );
                worldFragment.AddStaticMolecule(g);
            }
            catch (Exception e) {
                trace($"Unknown exception: {e}");
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
