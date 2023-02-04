using System.Collections.Generic;
using System.Linq;
using System.Numerics;


namespace builtin.tools
{

    public class Triangulate
    {
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
            foreach(var dpoint in poly.Points)
            {
                mesh.Vertices.Add(new Vector3((float)dpoint.X, (float)dpoint.Y, 0f));
            }
            foreach(var dtriangle in poly.Triangles)
            {
                mesh.Indices.Add(dtriangle.Points[0]);
                mesh.Indices.Add(dtriangle.Points[1]);
                mesh.Indices.Add(dtriangle.Points[2]);
            }
        }
    }
}
