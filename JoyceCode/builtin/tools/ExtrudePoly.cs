
using System;
using System.Collections.Generic;
using System.Numerics;

#if false
namespace builtin.tools
{
    public class ExtrudePoly
    {
        private static void trace( string message )
        {
            Console.WriteLine( message );
        }

        private List<Vector3> _poly;
        private List<Vector3> _path;
        private int _physIndex;
        private float _mpt;
        private bool _inverseTexture;
        private bool _addFloor;
        private bool _addCeiling;
    
        public void BuildGeom(
            in engine.world.Fragment worldFragment,
            in engine.joyce.Mesh g)
        {
            var vh = _path[0];
            var p = _poly;

            /*
             * Local uv, including inversion
             */
            Action<float, float> luv = (float u, float v) => {
                if (_inverseTexture) {
                    g.UV(1f-u, 1f-v);
                } else {
                    g.UV(u, v);
                }
            };

            var q = _inverseTexture;

            /*
             * We need the length of the path vector ("h") to correctly assign the
             * textures.
             */
            var h = vh.Length();

            /*
             * Plus we need the unit vector to properly move along the path.
             */
            var vu = Vector3.Normalize(vh);

            /*
            * We need to compute the number of mesh "rows" to compose the building from.
            * 
            * We need to do one mesh row every _mpt plus one
            * last row of the remaining meters.
            * 
            * That means: we generate one bottom vertice ring, nCompleteRows vertice rings
            * inbetween and one top Ring.
            */
            var nCompleteRows = (int)( h / _mpt );

            /* 
             * (Note, if lastRowHeight is less than 1cm and nCompleteRows>=1 , we do one complete
             * row less and do a proper last row with that cm included.)
             */
            float lastRowHeight = h - nCompleteRows * _mpt;

            if (lastRowHeight < 0f)
            {
                trace("ExtrudePoly.buildGeom(): lastRowHeight is < 0.");
                throw new InvalidOperationException( "ExtrudePoly.buildGeom(): lastRowHeight is < 0." );
            }

            /* 
             * (Note, if lastRowHeight is less than 1cm and nCompleteRows>=1 , we do one complete
             * row less and do a proper last row with that cm included.)
             */
            if (nCompleteRows >= 1)
            {
                if (lastRowHeight <= 0.01f)
                {
                    --nCompleteRows;
                    lastRowHeight += _mpt;
                }
            }

            var du = _mpt;
            var dv = _mpt;

            /*
             * Standard row height.
             */
            var vrh = vu;
            vrh *= _mpt;

            /*
             * Last row height.
             */
            var lrh = vu;
            lrh *= lastRowHeight;

            int i0 = g.GetNextVertexIndex();

            /*
             * Define vertices of the house.
             * 
             * Vertices are:
             * 0 1 1 2 ..<nCompleteRows> <nCompleteRows+1> First Column
             * 
             * <nCompleteRows>..<1*(nCompleteRows+2>+nCompleteRows+1>)
             * ... etc
             * for p.length+1 columns.
             */
            float currU = 0f;
            for (int j=0; j<p.Count; j++ )
            {
                int i = j % p.Count;
                Vector3 vc = p[i];

                /*
                 * Bottom ring
                 */
                g.p(vc); luv(currU / du, 1f);

                /*
                 * Complete rings
                 */

                /*
                * This is the current position along the path.
                */
                for (int i=0; i<nCompleteRows; i++)
                {
                    vc += vrh;

                    /*
                     * First, the "top" vertex of the previous layer, then the "bottom" vertex 
                     * of the next layer.
                     * 
                     * TXWTODO: Replace 0.;1. with the corresponding segment of the texture.
                     */
                    g.p(vc); luv(currU / du, 0f);
                    g.p(vc); luv(currU / du, 1f);
                }

                /*
                 * Ceiling.
                 */
                vc += lrh;
                g.p(vc); luv(currU / du, 1f-lastRowHeight / _mpt);
                // g.p1(vc); g.uv(currU/du, 1. );

                /*
                 * Compute the "width" of this facade to get the 
                 * texture right.
                 */
                var uDiff = p[(i + 1) % p.Count].clone();
                uDiff.subtract(p[i]);
                var l = uDiff.length();
                currU += l;
            }

            /*
             * How many vertices are in one column?
             */
            var columnHeight = (nCompleteRows + 1) * 2;

            for (int side=0; side<p.Count; ++side)
            {
                /*
                 * We need to generate 2 triangles for every complete row
                 * plus one for the extra row.
                 */
                var i = side * columnHeight + i0;
                var rows = nCompleteRows + 1;
                int nx = columnHeight;  // Offset to the next vertex in the ring on the same level
                int ny = 1; // Offset to the vertex in the same column on the next higher level.
                for (int row=0; row<rows; ++rows )
                {
                    g.Idx(i + 0, i + nx, i + ny);
                    g.Idx(i + ny, i + nx, i + nx + ny);
                    /*
                     * Advance to the next row. One row consists of two vertices.
                     */
                    i += 2;
                }
            }

            /*
             * Do we want to have a ceiling?
             * TXWTODO: Different material?
             * 
             * We add the points again to have plain uv values.
             */
            if (_addCeiling)
            {
                // trace( 'ExtrudePoly.buildGeom(): Adding ceiling.' );

                /*
                 * First, push the vertices.
                 * Then we create triangulation indices and add them.
                 */
                i0 = g.GetNextVertexIndex();
                for (int j=0; j<p.Count; j++)
                {
                    /*
                     * Take the original point, add the total height.
                     */
                    Vector3 vc = p[j];
                    vc += vh;
                    g.p(vc); luv(0, 0);
                }
                var indices = builtin.tools.triangulateConcave(p);
                var k = 0;
                while (k < indices.Count)
                {
                    // trace( 'ExtrudePoly.buildGeom(): Adding ceiling ${indices[k+2]}, ${indices[k+1]}, ${indices[k]}');
                    g.Idx(i0 + indices[k + 2], i0 + indices[k + 1], i0 + indices[k]);
                    k += 3;
                }
            }
        }


        public function buildPhys(
            worldFragment: WorldFragment,
            mol: engine.SimpleMolecule
        ): Void
    {
        var vh = _path[0];
        var p = _poly;
        if (null == p)
        {
            trace('ExtrudePoly.buildPhys(): Got a null polygon.');
            throw 'ExtrudePoly.buildPhys(): Got a null polygon.';
        }

        /*
         * Create a list of convex polygons from the possibly concave poly we have 
         * right now.
         */
        var pp: Array < Array < geom.Vector3D >> = null;

        try
        {
            pp = geom.Tools.concaveToConvex(p);
        }
        catch (unknown: Dynamic ) {
            trace('ExtrudePoly.buildPhys(): concaveToConvex(): Unknown exception: '
                + Std.string(unknown) + "\n"
                + haxe.CallStack.toString(haxe.CallStack.callStack()));
        }

        if (null == pp)
        {
            return;
        }

        /*
     * For each of the convex polygons, we now add a convex hull.
     */
    for (convexPoly in pp )
    {
        var wx = worldFragment.x;
        var wy = worldFragment.y;
        var wz = worldFragment.z;
        var physics: engine.Physics = null;
        try
        {
            physics = worldFragment.allEnv.worldLoader.getPhysics();
        }
        catch (unknown: Dynamic ) {
        trace('ExtrudePoly.buildPhys(): getPhysics(): Unknown exception: '
            + Std.string(unknown) + "\n"
            + haxe.CallStack.toString(haxe.CallStack.callStack()));
        return;
    }
    var allPoints = new Array<Vector3D>();
    for (i in 0... convexPoly.length )
    {
        allPoints.push(new geom.Vector3D(wx + convexPoly[i].x, wy + convexPoly[i].y, wz + convexPoly[i].z));
        allPoints.push(new geom.Vector3D(wx + convexPoly[i].x + vh.x, wy + convexPoly[i].y + vh.y, wz + convexPoly[i].z + vh.z));
    }
    var physHull: haxebullet.Bullet.BtCollisionShapePointer = null;
    try
    {
        physHull = physics.createConvexHull(allPoints);
    }
    catch (unknown: Dynamic ) {
        trace('ExtrudePoly.buildPhys(): createConvexHull(): Unknown exception: '
        + Std.string(unknown) + "\n"
            + haxe.CallStack.toString(haxe.CallStack.callStack()));
    }
    var physAtom: engine.IPhysAtom = null;
    try
    {
        physAtom = physics.createRigidBody(0., 0.5, _physIndex, physHull);
    }
    catch (unknown: Dynamic ) {
        trace('ExtrudePoly.buildPhys(): createRigidBody(): Unknown exception: '
            + Std.string(unknown) + "\n"
            + haxe.CallStack.toString(haxe.CallStack.callStack()));
    }

    mol.moleculeAddPhysAtom(physAtom);
    }
    }


    /**
     * @param poly
     *     The polygon to be used for extrusion. The polygon needs to be counterclockwise.
     */
    public function new(
        poly: Array<Vector3D>,
        path: Array<Vector3D>,
        physIndex: Int,
        metersPerTexture: Float,
        inverseTexture: Bool,
        addFloor: Bool,
        addCeiling: Bool
    ) {
        if( null==poly ) {
            trace( 'ExtrudePoly(): Got a null polygon.' );
            throw 'ExtrudePoly(): Got a null polygon.';
        }
       _poly = poly;
        _path = path;
        _physIndex = physIndex;
        _mpt = metersPerTexture;
        _inverseTexture = inverseTexture;
        _addFloor = addFloor;
        _addCeiling = addCeiling;
    }
}    }
}
#endif