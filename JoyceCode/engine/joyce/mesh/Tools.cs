using System.Numerics;
using System.Collections;
using System.Collections.Generic;

namespace engine.joyce.mesh
{
    public class Tools
    {
        private static void _addQuadIndicesXY(
            in joyce.Mesh m,
            int i0, int i1, int i2, int i3
        )
        {
            m.Indices.Add(i0); m.Indices.Add(i1); m.Indices.Add(i2);
            m.Indices.Add(i2); m.Indices.Add(i1); m.Indices.Add(i3);
        }

        private static void _addQuadXYUV(
            in joyce.Mesh m,
            in Vector3 v0,
            in Vector3 vx,
            in Vector3 vy,
            in Vector2 u0,
            in Vector2 ux,
            in Vector2 uy
        )
        {
            var idx = m.Vertices.Count;
            m.Vertices.Add(v0); m.Vertices.Add(v0 + vx); m.Vertices.Add(v0 + vy); m.Vertices.Add(v0 + vx + vy);
            m.UVs.Add(u0); m.UVs.Add(u0 + ux); m.UVs.Add(u0 + uy); m.UVs.Add(u0 + ux + uy);
            _addQuadIndicesXY(m, idx+0, idx+1, idx+2, idx+3);
        }

        private static void _addQuadXY(
            in joyce.Mesh m, in Vector3 v0, in Vector3 vx, in Vector3 vy )
        {
            _addQuadXYUV( m, v0, vx, vy, new Vector2( 0f, 0f ), new Vector2( 1f, 0f ), new Vector2( 0f, 1f ) );
        }

        public static joyce.Mesh CreateCubeMesh(float size)
        {
            float h /* half */ = size / 2;

            var m = joyce.Mesh.CreateArrayListInstance();

            // Back
            _addQuadXY( m, new Vector3(h, -h, -h), new Vector3(-size, 0f, 0f), new Vector3(0f, size, 0f) );
            // Front
            _addQuadXY( m, new Vector3(-h, -h, h), new Vector3(size, 0f, 0f), new Vector3(0f, size, 0f) );
            // Top
            _addQuadXY(m, new Vector3(-h, h, h), new Vector3(size, 0f, 0f), new Vector3(0f, 0f, -size) );
            // Bottom
            _addQuadXY(m, new Vector3(-h, -h, -h), new Vector3(size, 0f, 0f), new Vector3(0f, 0f, size));
            // Right
            _addQuadXY(m, new Vector3(h, -h, h), new Vector3(0f, 0f, -size), new Vector3(0f, size, 0f));
            // Left
            _addQuadXY(m, new Vector3(-h, -h, -h), new Vector3(0f, 0f, size), new Vector3(0f, size, 0f));

            return m;
        }
    }
}
