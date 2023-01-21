using System.Numerics;
using System.Collections;
using System.Collections.Generic;

namespace Karawan.engine.joyce.mesh
{
    class Tools
    {
        private static void _addQuadIndicesCW(
            in IList indices,
            int i0, int i1, int i2, int i3
        )
        {
            indices.Add(i0); indices.Add(i1); indices.Add(i2);
            indices.Add(i2); indices.Add(i3); indices.Add(i0);
        }

        public static components.Mesh CreateCubeMesh()
        {
            var m = components.Mesh.CreateArrayListInstance();

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
            _addQuadIndicesCW(m.Indices, 1, 0, 2, 3);
            // Front
            _addQuadIndicesCW(m.Indices, 4, 5, 7, 6);
            // Top
            _addQuadIndicesCW(m.Indices, 6, 7, 3, 2);
            // Bottom
            _addQuadIndicesCW(m.Indices, 5, 4, 0, 1);
            // Right
            _addQuadIndicesCW(m.Indices, 5, 1, 3, 7);
            // Left
            _addQuadIndicesCW(m.Indices, 4, 6, 2, 0);
            
            return m;
        }
    }
}
