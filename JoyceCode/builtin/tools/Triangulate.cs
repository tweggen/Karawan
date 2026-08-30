using LibTessDotNet;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.ExceptionServices;

namespace builtin.tools
{

    public class Triangulate
    {
        /**
         * Triangulate a polygon into a mesh, in a plane the caller names.
         *
         * **The plane is an argument because LibTess guesses it otherwise, and the guess
         * is what decides which way the face points.** With a zero normal the tessellator
         * derives a projection plane from the polygon itself; for a ring that is not
         * planar - a city block traced across a hillside, say - that derivation flips, and
         * a flipped projection emits the triangles wound the other way round. The mesh is
         * complete, every vertex is where it should be, and the face is back-facing, so
         * with GL_CULL_FACE on it simply is not there. Measured over the generated cities:
         * a block floor's top face came out downward for 8 of 445 blocks on flat ground
         * and for 211 of 445 on a 5.8 % slope; naming the plane makes it 445 of 445 upward
         * in both.
         *
         * The output faces are always wound counterclockwise about v3Plane (or clockwise,
         * with `clockwise` set), whatever direction the input contour runs in - that is a
         * property of the tessellator's sweep, and it is why naming the plane is enough
         * and the contour does not also have to be pre-oriented.
         *
         * @param v3Plane
         *     Normal of the plane to tessellate in, and the direction the emitted faces
         *     will point. Need not be normalised, must not be zero.
         * @param v3VertexNormal
         *     Per vertex normal to write, or Vector3.Zero to write none. SEPARATE from
         *     v3Plane on purpose: these were one parameter until 2026-08-30, so the only
         *     caller that wanted no vertex normals was also the only caller that let the
         *     tessellator guess its plane, and that is the whole of the bug above. Note
         *     the mesh must already carry a Normals list if this is not zero.
         */
        static public void ToMesh(
            in IList<Vector3> inPolyPoints,
            in Vector3 v3Plane,
            in Vector3 v3VertexNormal,
            in Vector2 v2UV,
            in engine.joyce.Mesh mesh,
            bool clockwise = false)
        {
            if (Vector3.Zero == v3Plane)
            {
                engine.Logger.ErrorThrow(
                    "Triangulate.ToMesh needs the plane to tessellate in; "
                    + "a zero normal leaves LibTess to guess it, and the guess flips.",
                    le => new System.ArgumentException(le));
            }

            LibTessDotNet.Tess tess = new LibTessDotNet.Tess();

            var nPoints = inPolyPoints.Count;
            var contour = new LibTessDotNet.ContourVertex[nPoints];
            for(int i=0; i<nPoints; i++)
            {
                contour[i].Position = new LibTessDotNet.Vec3(
                    inPolyPoints[i].X, inPolyPoints[i].Y, inPolyPoints[i].Z);
            }
            tess.AddContour(contour, clockwise?LibTessDotNet.ContourOrientation.Clockwise:LibTessDotNet.ContourOrientation.CounterClockwise);
            tess.Tessellate(LibTessDotNet.WindingRule.EvenOdd, LibTessDotNet.ElementType.Polygons, 3, null, new LibTessDotNet.Vec3(v3Plane.X, v3Plane.Y, v3Plane.Z));
            int outTriangles = tess.ElementCount;
            uint maxIndex = 0;
            uint ia = (uint)mesh.GetNextVertexIndex();
            for( uint i=0; i<outTriangles; i++ )
            {
                uint i0 = (uint)tess.Elements[i * 3 + 0];
                uint i1 = (uint)tess.Elements[i * 3 + 1];
                uint i2 = (uint)tess.Elements[i * 3 + 2];
                if (i0 > maxIndex) maxIndex = i0;
                if (i1 > maxIndex) maxIndex = i1;
                if (i2 > maxIndex) maxIndex = i2;
                if (clockwise)
                {
                    mesh.Idx(ia + i0, ia + i2, ia + i1);

                }
                else
                {
                    mesh.Idx(ia + i0, ia + i1, ia + i2);
                }
            }
            for( int i=0; i<=maxIndex; i++)
            {
                mesh.p(tess.Vertices[i].Position.X, tess.Vertices[i].Position.Y, tess.Vertices[i].Position.Z);
                mesh.UV(v2UV);
                if (v3VertexNormal != Vector3.Zero)
                {
                    mesh.N(v3VertexNormal);
                }
            }
        }

        
        static public void ToConvexArrays(in IList<Vector3> inPolyPoints, out IList<IList<Vector3>> outPolygons)
        {
            LibTessDotNet.Tess tess = new LibTessDotNet.Tess();

            var nPoints = inPolyPoints.Count;
            /* var inputData = new float[nPoints * 2];
            for (int i = 0; i < nPoints; i++)
            {
                inputData[i * 2] = inPolyPoints[i].X;
                inputData[i * 2 + 1] = inPolyPoints[i].Y;
            }*/
            var contour = new LibTessDotNet.ContourVertex[nPoints];
            for (int i = 0; i < nPoints; i++)
            {
                contour[i].Position = new LibTessDotNet.Vec3(inPolyPoints[i].X, inPolyPoints[i].Y, inPolyPoints[i].Z);
            }
            const int polySize = 20;
            tess.AddContour(contour, LibTessDotNet.ContourOrientation.Clockwise);
            tess.Tessellate(LibTessDotNet.WindingRule.EvenOdd, LibTessDotNet.ElementType.Polygons, polySize, null);
            int outPolys = tess.ElementCount;
            outPolygons = new List<IList<Vector3>>();
            for (int i = 0; i < outPolys; i++)
            {
                var poly = new List<Vector3>();
                // Backwards due to orientation.
                for(int j=polySize-1; j>=0; j--) {
                    int k = tess.Elements[i * polySize + j];
                    if (k == Tess.Undef) continue;
                    var pos = tess.Vertices[k].Position;
                    poly.Add(new Vector3(pos.X, pos.Y, pos.Z));
                }
                outPolygons.Add(poly);
            }
        }
    }
}
