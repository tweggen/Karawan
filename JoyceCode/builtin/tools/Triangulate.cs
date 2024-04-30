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
        //static private LibTessDotNet.Tess _tess = new LibTessDotNet.Tess();
#if false
        /*
         * Triangulate the polygon into a list of triangles, represented by an 3d mesh in xy plane.
         */
        static public void ToMesh( in IList<Vector2> inPolyPoints, in engine.joyce.Mesh mesh )
        {
            List<Poly2Tri.PolygonPoint> polyPoints = new();
            foreach(var vector2 in inPolyPoints )
            {
                polyPoints.Add(new Poly2Tri.PolygonPoint(vector2.X,vector2.Y));
            }
            Poly2Tri.Polygon poly = new(polyPoints);
            // poly.AddPoints(polyPoints);
            Poly2Tri.P2T.Triangulate(poly);
            var trianglePoints = poly.Points;
            foreach(var dpoint in poly.Points)
            {
                mesh.Vertices.Add(new Vector3((float)dpoint.X, (float)dpoint.Y, 0f));
                mesh.UVs.Add(new Vector2(0f, 0f));
            }
            foreach(var dtriangle in poly.Triangles)
            {
                mesh.Indices.Add(trianglePoints.IndexOf(dtriangle.Points[1]));
                mesh.Indices.Add(trianglePoints.IndexOf(dtriangle.Points[0]));
                mesh.Indices.Add(trianglePoints.IndexOf(dtriangle.Points[2]));
            }
        }
#endif
        static public void ToMesh(in IList<Vector3> inPolyPoints, in Vector3 v3Normal, in Vector2 v2UV, in engine.joyce.Mesh mesh)
        {
            LibTessDotNet.Tess tess = new LibTessDotNet.Tess();

            var nPoints = inPolyPoints.Count;
            var contour = new LibTessDotNet.ContourVertex[nPoints];
            for(int i=0; i<nPoints; i++)
            {
                contour[i].Position = new LibTessDotNet.Vec3(
                    inPolyPoints[i].X, inPolyPoints[i].Y, inPolyPoints[i].Z);
            }
            tess.AddContour(contour, LibTessDotNet.ContourOrientation.CounterClockwise);
            tess.Tessellate(LibTessDotNet.WindingRule.EvenOdd, LibTessDotNet.ElementType.Polygons, 3, null, new LibTessDotNet.Vec3(v3Normal.X, v3Normal.Y, v3Normal.Z));
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
                mesh.Idx(ia+i0, ia+i1, ia+i2);
            }
            for( int i=0; i<=maxIndex; i++)
            {
                mesh.p(tess.Vertices[i].Position.X, tess.Vertices[i].Position.Y, tess.Vertices[i].Position.Z);
                mesh.UV(v2UV);
                if (v3Normal != Vector3.Zero)
                {
                    mesh.N(v3Normal);
                }
            }
        }

        #if false
        static public void ToMesh( in IList<Vector2> inPoly2Points, in engine.joyce.Mesh mesh)
        {
            List<Vector3> inPolyPoints = new();
            foreach(var p in inPoly2Points)
            {
                inPolyPoints.Add(new Vector3(p.X, p.Y, 0f));
            }
            ToMesh(inPolyPoints, mesh);
        }
        #endif

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
