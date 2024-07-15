using static engine.Logger;
using BepuPhysics.Collidables;
using BepuUtilities.Collections;
using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using engine;
using engine.geom;

namespace builtin.tools
{
    public class ExtrudePoly
    {
        private readonly List<Vector3> _poly;
        private readonly List<Vector3> _path;
        private readonly int _physIndex;
        private readonly float _mpt;
        private readonly bool _inverseTexture;
        private readonly bool _addFloor;
        private readonly bool _addCeiling;
        public bool PairedNormals { get; set; } = false;
        public bool TileToTexture { get; set; } = false;

        public bool SkipSmall { get; set; } = false;

        private engine.physics.API _aPhysics;
    
        public void BuildGeom(
            in engine.joyce.Mesh g)
        {

            if (PairedNormals)
            {
                if (null == g.Normals)
                {
                    g.Normals = new List<Vector3>();
                }
            }
            var vh = _path[0];
            
            /*
             * Unfortunately, we cannot directly build the poly, we need to take care
             * no wall has a length longer than texture length (_mpt)
             */
            List<Vector3> p = new();
            List<float> listU = new();
            if (TileToTexture) {
                int n = _poly.Count;
                
                float uCurr = 0f;

                for (int i = 0; i < n; ++i)
                {
                    /*
                     * First, add the current point and the u value we currently are using.
                     */
                    Vector3 v3Curr = _poly[i];
                    p.Add(v3Curr);
                    listU.Add(uCurr);

                    /*
                     * The next one (or the first) obviously is the target.
                     */
                    var v3Target = _poly[(i+1)%n];

                    /*
                     * Now add inbetween points of the wall is longer than the texture.
                     */
                    while (true)
                    {
                        Vector3 v3Delta = v3Target - v3Curr;
                        float lDelta = v3Delta.Length();

                        /*
                         * Where would the end of the texture be?
                         */
                        float uDelta = lDelta / _mpt;
                        float uThen = uCurr + uDelta;

                        /*
                         * Figure out, if we want to use the poly as is
                         * or want to stretch the texture a little bit
                         * to avoid miniature stitches.
                         */
                        if (uThen > 1.1f)
                        {
                            /*
                             * OK, the extent we are facing would be too long.
                             * So add an additional set of points
                             */
                            v3Curr = v3Curr + (v3Target - v3Curr) * 1f / uDelta;
                            uCurr = 1f;

                            p.Add(v3Curr);
                            listU.Add(uCurr);

                            uCurr = 0f;
                            if (PairedNormals)
                            {
                                p.Add(v3Curr);
                                listU.Add(uCurr);
                            }
                        }
                        else
                        {
                            /*
                             * This still might be a tiny bit too long, however, we just stretch
                             * the texture a bit. This avoids small irritating stripes of color.
                             */
                            if (uThen >= 1f)
                            {
                                uThen = 1f;
                                uCurr = 0f;
                            }
                            
                            /*
                             * This is not too long, so we can finally add the target point if required.
                             */
                            if (PairedNormals)
                            {
                                v3Curr = v3Target;
                                listU.Add(uThen);
                                p.Add(v3Curr);
                                
                            }
                            
                            /*
                             * We now can leave the while look, there is no more point to emit.
                             */
                            break;
                        }
                    }
                }
            }
            else
            {
                if (PairedNormals)
                {
                    for (int i = 0; i < p.Count; ++i)
                    {
                        p.Add(_poly[i]);
                        listU.Add(0f);
                        p.Add(_poly[i]);
                        listU.Add(0f);
                    }
                }
                else
                {
                    p = _poly;
                    for (int i = 0; i < p.Count; ++i)
                    {
                        listU.Add(0f);
                    }
                }
            }

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
            uint nCompleteRows = (uint)( h / _mpt );

            /* 
             * (Note, if lastRowHeight is less than 1cm and nCompleteRows>=1 , we do one complete
             * row less and do a proper last row with that cm included.)
             */
            float lastRowHeight = h - nCompleteRows * _mpt;

            if (lastRowHeight < 0f)
            {
                ErrorThrow("lastRowHeight is < 0.", le => new InvalidOperationException( le ));
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

            uint i0 = g.GetNextVertexIndex();

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
            int polyLen = p.Count;
            
            /*
             * When we are in paired normals mode, we already have a point that
             * is closing to the opening one.
             *
             * However, when we just are connecting, we do not.
             */
            int loopLen = polyLen + (PairedNormals ? 0 : 1);
            for (int j=0; j<loopLen; j++ )
            {
                int i = j % polyLen;
                Vector3 vc = p[i];

                Vector3 vnPairNormal = default;
                if (PairedNormals)
                {
                    /*
                     * The pair normal is the cross product between the difference between
                     * this point of pairs and the up vector.
                     */
                    Vector3 vuTangent = p[((j%polyLen) & (~1)) + 1] - p[((j%polyLen) & (~1)) + 0];
                    vuTangent /= vuTangent.Length();
                    // This is minus cross product because gl operates counterclockwise
                    vnPairNormal = -Vector3.Cross(vu, vuTangent);
                }

                /*
                 * Bottom ring
                 */
                g.p(vc); g.UV(ot+it*listU[i], ot+it*1f);
                if (PairedNormals) g.N(vnPairNormal);

                
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
                    g.p(vc); g.UV(ot+it*listU[i], ot+it*0f);
                    if (PairedNormals) g.N(vnPairNormal);
                    g.p(vc); g.UV(ot+it*listU[i], ot+it*1f);
                    if (PairedNormals) g.N(vnPairNormal);
                }

                /*
                 * Ceiling.
                 */
                vc += lrh;
                g.p(vc); g.UV(ot+it*listU[i], ot+it*(1f-lastRowHeight/_mpt));
                if (PairedNormals) g.N(vnPairNormal);
            }

            /*
             * How many vertices are in one column?
             */
            uint columnHeight = (nCompleteRows + 1) * 2;

            /*
             * If we are working in paired normals mode, we create the columns per pair of vertices.
             */
            uint sideIncrement = PairedNormals ? 2u : 1u;
            for (uint side=0; side<polyLen; side+=sideIncrement)
            {
                /*
                 * We need to generate 2 triangles for every complete row
                 * plus one for the extra row.
                 */
                uint i = side * columnHeight + i0;
                var rows = nCompleteRows + 1;
                uint nx = columnHeight;  // Offset to the next vertex in the ring on the same level
                uint ny = 1; // Offset to the vertex in the same column on the next higher level.
                for (uint row=0; row<rows; ++row )
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
                /*
                 * First, push the vertices.
                 * Then we create triangulation indices and add them.
                 */
                List<Vector3> topPlane = new();
                foreach(var op in p)
                {
                    Vector3 tp = op + vh;
                    topPlane.Add(tp);
                }
                // Why? IDK, was wrong.
                topPlane.Reverse();
                /*
                 * We hard code the UV to be a bit next to zero to make up for any range problems
                 */
                builtin.tools.Triangulate.ToMesh(topPlane, PairedNormals?Vector3.Normalize(_path[0]):Vector3.Zero, Vector2.One/64f, g);
            }

            if (_addFloor)
            {
                /*
                 * First, push the vertices.
                 * Then we create triangulation indices and add them.
                 */
                List<Vector3> bottomPlane = new();
                foreach(var op in p)
                {
                    Vector3 tp = op;
                    bottomPlane.Add(tp);
                }
                // Why? IDK, was wrong.
                bottomPlane.Reverse();
                /*
                 * We hard code the UV to be a bit next to zero to make up for any range problems
                 */
                builtin.tools.Triangulate.ToMesh(bottomPlane, PairedNormals?Vector3.Normalize(_path[0]):Vector3.Zero, Vector2.One/64f, g, true);
            }
        }


        private static void _emptyFree()
        {
        }

        private static Action _emptyCreate(IList<StaticHandle> staticHandles)
        {
            return () => { };
        }

        /**
         * Return a StaticDescription and a dtor for the shapes used within.
         */
        public Func<IList<StaticHandle>, Action> BuildStaticPhys(
            engine.world.Fragment worldFragment,
            engine.physics.CollisionProperties? collisionProperties = null)
        {
            var vh = _path[0];

            float vhLength2 = vh.LengthSquared();
            if (SkipSmall && vh.LengthSquared() < 0.01f)
            {
                Warning($"Very small polygon.");
                return _emptyCreate;
            }
            
            if (null == _poly)
            {
                ErrorThrow( "Got a null polygon.", le => new ArgumentNullException(le) );
            }


            Func<IList<StaticHandle>, Action> fCreatePhys = new((IList<StaticHandle> staticHandles) =>
            {
                lock (worldFragment.Engine.Simulation)
                {
                    var bufferPool = _aPhysics.BufferPool;
                    var simulation = _aPhysics.Simulation;

                    List<ConvexHull> convexHullsToRelease = new();

                    IList<IList<Vector3>> listConvexPolys;
                    builtin.tools.Triangulate.ToConvexArrays(_poly, out listConvexPolys);
                    BepuPhysics.Collidables.CompoundBuilder builder = new BepuPhysics.Collidables.CompoundBuilder(
                        bufferPool, worldFragment.Engine.Simulation.Shapes, 8);
                    var identityPose = new BepuPhysics.RigidPose
                        { Position = new Vector3(0f, 0f, 0f), Orientation = Quaternion.Identity };

                    /*
                    * for each of the convex polys, build a convex hull from it.
                    */
                    int nConvex = 0;
                    foreach (var convexPoly in listConvexPolys)
                    {
                        int nPoints = 2 * convexPoly.Count;
                        if (nPoints <= 4)
                        {
                            Error($"Invalid number of points in complex polygon.");
                            /*
                           * Do not add this as a complex hull.
                           */
                            continue;
                        }

                        if (SkipSmall)
                        {
                            Vector3 p0 = convexPoly[0];
                            int l = convexPoly.Count;
                            float area = 0f;
                            for (int i = 1; i < l-1; ++i)
                            {
                                area += Vector3.Cross(convexPoly[i]-p0, convexPoly[i + 1]-p0).Length() / 2f;
                            }

                            if (area < 0.1f)
                            {
                                Warning($"Suspiciosly small area {area}");
                                continue;
                            }
                        }

                        
                        QuickList<Vector3> pointsConvexHull = new QuickList<Vector3>(nPoints, bufferPool);
                        AABB aabbVolumeTest = new();
                        foreach (var p3 in convexPoly)
                        {
                            var pBottom = worldFragment.Position + p3;
                            var pTop = worldFragment.Position + p3 + vh;
                            /*
                           * Just add the bottom because we do not want to consider the height. 
                           */
                            if (SkipSmall) aabbVolumeTest.Add(pBottom);
                            pointsConvexHull.AllocateUnsafely() = pBottom;
                            pointsConvexHull.AllocateUnsafely() = pTop;
                        }

                        if (SkipSmall && aabbVolumeTest.Radius < 0.1f)
                        {
                            Warning($"Warning: Suspiciously small convex hull.");
                            /*
                           * Do not add this as a complex hull. 
                           */
                            continue;
                        }

                        var pointsBuffer = pointsConvexHull.Span.Slice(pointsConvexHull.Count);
                        ConvexHullHelper.CreateShape(pointsBuffer, bufferPool, out var vCenter,
                            out var pshapeConvexHull);
                        convexHullsToRelease.Add(pshapeConvexHull);
                        // builder.Add(pshapeConvexHull, new BepuPhysics.RigidPose { Position = vCenter, Orientation = Quaternion.Identity }, 1f);
                        /*
                         * Assuming, we are adding a static, we add the shape assuming infinite mass.
                         */
                        //builder.Add(pshapeConvexHull, new BepuPhysics.RigidPose { Position = vCenter, Orientation = Quaternion.Identity }, 1f);
                        builder.AddForKinematic(pshapeConvexHull,
                            new BepuPhysics.RigidPose { Position = vCenter, Orientation = Quaternion.Identity }, 1f);
                        nConvex++;
                    }

                    if (nConvex > 0)
                    {
                        builder.BuildKinematicCompound(out var compoundChildren, out var vCompoundCenter);
                        builder.Reset();
                        var pshapeCompound = new Compound(compoundChildren);
                        var shapeIndex = simulation.Shapes.Add(pshapeCompound);
                        var staticDescription = new StaticDescription(vCompoundCenter, shapeIndex);
                        
                        var staticHandle = simulation.Statics.Add(staticDescription);
                        staticHandles.Add(staticHandle);
                        if (null != collisionProperties)
                        {
                            _aPhysics.AddCollisionEntry(staticHandle, collisionProperties);
                        }

                        // Return release shapes function.
                        return new Action(() =>
                        {
                            lock (worldFragment.Engine.Simulation)
                            {
                                /*
                                 * First, release the compound structure.
                                 */
                                pshapeCompound.Dispose(bufferPool);

                                /*
                                 * After that, release the convex shapes contained within.
                                 */
                                foreach (var convexHull in convexHullsToRelease)
                                {
                                    convexHull.Dispose(bufferPool);
                                }

                                _aPhysics.RemoveCollisionEntry(staticHandle);
                            }
                        });
                    }
                    else
                    {
                        return _emptyFree;
                    }
                }
            });

            return fCreatePhys;
        }


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
            if (null==poly) 
            {
                ErrorThrow("Got a null polygon.", le => new ArgumentNullException(le));
            }

            if (null==path)
            {
                ErrorThrow("Got a null path", le => new ArgumentNullException(le));
            }
            _poly = new List<Vector3>( poly );
            _path = new List<Vector3>( path );
            _physIndex = physIndex;
            _mpt = metersPerTexture;
            _inverseTexture = inverseTexture;
            _addFloor = addFloor;
            _addCeiling = addCeiling;
            _aPhysics = I.Get<engine.physics.API>();
        }
    }
}
