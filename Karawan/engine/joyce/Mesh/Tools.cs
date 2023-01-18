using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.engine.joyce.Mesh
{
    class Tools
    {
        public static components.Mesh1 CreateCubeMesh()
        {
            var m = new components.Mesh1();

            m.Vertices.Add(new Vector3(-.5f, -.5f, -.5f));
            m.UVs.Add(new Vector2(0, 0));
            m.Vertices.Add(new Vector3(.5f, -.5f, -.5f));
            m.UVs.Add(new Vector2(0, 0));
            m.Vertices.Add(new Vector3(-.5f, .5f, -.5f));
            m.UVs.Add(new Vector2(0, 0));
            m.Vertices.Add(new Vector3(.5f, .5f, -.5f));
            m.UVs.Add(new Vector2(0, 0));
            m.Vertices.Add(new Vector3(-.5f, -.5f, .5f));
            m.UVs.Add(new Vector2(0, 0));
            m.Vertices.Add(new Vector3(.5f, -.5f, .5f));
            m.UVs.Add(new Vector2(0, 0));
            m.Vertices.Add(new Vector3(-.5f, .5f, .5f));
            m.UVs.Add(new Vector2(0, 0));
            m.Vertices.Add(new Vector3(.5f, .5f, .5f));
            m.UVs.Add(new Vector2(0, 0));

            // Back
            m.Indices.Add(0); m.Indices.Add(1); m.Indices.Add(2);
            m.Indices.Add(1); m.Indices.Add(3); m.Indices.Add(2);
            // Front
            m.Indices.Add(5); m.Indices.Add(4); m.Indices.Add(6);
            m.Indices.Add(5); m.Indices.Add(7); m.Indices.Add(6);
            // Top
            m.Indices.Add(7); m.Indices.Add(6); m.Indices.Add(2);
            m.Indices.Add(7); m.Indices.Add(2); m.Indices.Add(3);
            // Bottom
            m.Indices.Add(1); m.Indices.Add(0); m.Indices.Add(4);
            m.Indices.Add(1); m.Indices.Add(4); m.Indices.Add(5);
            // Right
            m.Indices.Add(1); m.Indices.Add(5); m.Indices.Add(7);
            m.Indices.Add(1); m.Indices.Add(3); m.Indices.Add(7);
            // Left
            m.Indices.Add(4); m.Indices.Add(0); m.Indices.Add(5);
            m.Indices.Add(4); m.Indices.Add(1); m.Indices.Add(5);
            
            return m;
        }
    }
}
