
using BepuPhysics.Collidables;
using BepuUtilities.Collections;
using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Numerics;


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

            float it;
            float ot;
            if( _inverseTexture )
            {
                it = -1f;
                ot = 1f;
            } else
            {
                it = 1f;
                ot = 0f;
            }

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
            for (int j=0; j<p.Count+1; j++ )
            {
                int i = j % p.Count;
                Vector3 vc = p[i];

                /*
                 * Bottom ring
                 */
                g.p(vc); g.UV(ot+it*currU/du, ot+it*1f);

                /*
                 * Complete rings
                 */

                /*
                * This is the current position along the path.
                */
                for (int row=0; row<nCompleteRows; row++)
                {
                    vc += vrh;

                    /*
                     * First, the "top" vertex of the previous layer, then the "bottom" vertex 
                     * of the next layer.
                     * 
                     * TXWTODO: Replace 0.;1. with the corresponding segment of the texture.
                     */
                    g.p(vc); g.UV(ot+it*currU/du, ot+it*0f);
                    g.p(vc); g.UV(ot+it*currU/du, ot+it*1f);
                }

                /*
                 * Ceiling.
                 */
                vc += lrh;
                g.p(vc); g.UV(ot+it*currU/du, ot+it*(1f-lastRowHeight/_mpt));
                // g.p1(vc); g.uv(currU/du, 1. );

                /*
                 * Compute the "width" of this facade to get the 
                 * texture right.
                 */
                var uDiff = p[(i + 1) % p.Count];
                uDiff -=  p[i];
                var l = uDiff.Length();
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
                for (int row=0; row<rows; ++row )
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
                //i0 = g.GetNextVertexIndex();
#if true
                List<Vector3> topPlane = new();
                foreach(var op in p)
                {
                    Vector3 tp = op + vh;
                    topPlane.Add(tp);
                }
                // Why? IDK, was wrong.
                topPlane.Reverse();
                builtin.tools.Triangulate.ToMesh(topPlane, g);
#else
                for (int j=0; j<p.Count; j++)
                {
                    /*
                     * Take the original point, add the total height.
                     */
                    Vector3 vc = p[j];
                    vc += vh;
                    g.p(vc); g.UV(ot+it*0f, ot+it*0f);
                }


                var indices = builtin.tools.triangulateConcave(p);
                var k = 0;
                while (k < indices.Count)
                {
                    // trace( 'ExtrudePoly.buildGeom(): Adding ceiling ${indices[k+2]}, ${indices[k+1]}, ${indices[k]}');
                    g.Idx(i0 + indices[k + 2], i0 + indices[k + 1], i0 + indices[k]);
                    k += 3;
                }
#endif
            }
        }

#if true
        public void BuildPhys( in engine.world.Fragment worldFragment )
        {
            var vh = _path[0];
            if (null == _poly)
            {
                trace($"ExtrudePoly.buildPhys(): Got a null polygon.");
                throw new InvalidOperationException( $"ExtrudePoly.buildPhys(): Got a null polygon." );
            }


            var bufferPool = worldFragment.Engine.BufferPool;
            var simulation = worldFragment.Engine.Simulation;

            IList<IList<Vector3>> listConvexPolys;
            builtin.tools.Triangulate.ToConvexArrays(_poly, out listConvexPolys);
            // BepuPhysics.Collidables.CompoundBuilder builder = new BepuPhysics.Collidables.CompoundBuilder(
            //    bufferPool, worldFragment.Engine.Simulation.Shapes, 20);
            //var identityPose = new BepuPhysics.RigidPose { Position = new Vector3(0f, 0f, 0f), Orientation = Quaternion.Identity };
            //var fragmentPose = new BepuPhysics.RigidPose { Position = worldFragment.Position, Orientation = Quaternion.Identity };

            /*
             * for each of the convex polys, build a convex hull from it.
             */
            foreach( var convexPoly in listConvexPolys )
            {
                int nPoints = 2 * convexPoly.Count;
                QuickList<Vector3> pointsConvexHull = new QuickList<Vector3>(nPoints, bufferPool);
                //Console.WriteLine("New hull:");
                foreach (var p3 in convexPoly)
                {
                    var pBottom = worldFragment.Position + p3;
                    var pTop = worldFragment.Position + p3 + vh;
                    pointsConvexHull.AllocateUnsafely() = pBottom;
                    pointsConvexHull.AllocateUnsafely() = pTop;
                    //Console.WriteLine($"Added {pBottom} {pTop}");
                }
                var pointsBuffer = pointsConvexHull.Span.Slice(pointsConvexHull.Count);
                ConvexHullHelper.CreateShape(pointsBuffer, bufferPool, out var vCenter, out var pshapeConvexHull);
                //Console.WriteLine($"Center is {vCenter}");
                simulation.Statics.Add(new StaticDescription(
                        vCenter, 
                        new CollidableDescription(
                            simulation.Shapes.Add(pshapeConvexHull),
                            0.1f
                        )
                    )
                );
            }
        }
    
#endif

        /**
         * @param poly
         *     The polygon to be used for extrusion. The polygon needs to be counterclockwise.
         */
        public ExtrudePoly(
            IList<Vector3> poly,
            IList<Vector3> path,
            int physIndex,
            float metersPerTexture,
            bool inverseTexture,
            bool addFloor,
            bool addCeiling
        ) {
            if( null==poly ) {
                trace( "ExtrudePoly(): Got a null polygon." );
                throw new ArgumentNullException( "ExtrudePoly(): Got a null polygon." );
            }
            _poly = new List<Vector3>( poly );
            _path = new List<Vector3>( path );
            _physIndex = physIndex;
            _mpt = metersPerTexture;
            _inverseTexture = inverseTexture;
            _addFloor = addFloor;
            _addCeiling = addCeiling;
        }
    }
}
