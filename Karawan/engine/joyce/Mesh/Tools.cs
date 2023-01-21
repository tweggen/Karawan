using System.Numerics;
using System.Collections;
using System.Collections.Generic;

namespace Karawan.engine.joyce.mesh
{
    class Tools
    {
        private static void _addQuadIndicesXY(
            in components.Mesh m,
            int i0, int i1, int i2, int i3
        )
        {
            m.Indices.Add(i0); m.Indices.Add(i1); m.Indices.Add(i2);
            m.Indices.Add(i2); m.Indices.Add(i1); m.Indices.Add(i3);
        }

        private static void _addQuadXYUV(
            in components.Mesh m,
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
            in components.Mesh m, in Vector3 v0, in Vector3 vx, in Vector3 vy )
        {
            _addQuadXYUV( m, v0, vx, vy, new Vector2( 0f, 0f ), new Vector2( 1f, 0f ), new Vector2( 0f, 1f ) );
        }

        public static components.Mesh CreateCubeMesh()
        {
            var m = components.Mesh.CreateArrayListInstance();

            // Back
            _addQuadXY( m, new Vector3(.5f, -.5f, -.5f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 1f, 0f) );
            // Front
            _addQuadXY( m, new Vector3(-.5f, -.5f, .5f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f) );
            // Top
            _addQuadXY(m, new Vector3(-.5f, .5f, .5f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, -1f) );
            // Bottom
            _addQuadXY(m, new Vector3(-.5f, -.5f, -.5f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f));
            // Right
            _addQuadXY(m, new Vector3(.5f, -.5f, .5f), new Vector3(0f, 0f, -1f), new Vector3(0f, 1f, 0f));
            // Left
            _addQuadXY(m, new Vector3(-.5f, -.5f, -.5f), new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f));

            return m;
        }
    }
}
