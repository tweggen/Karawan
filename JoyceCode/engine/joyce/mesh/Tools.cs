using System.Numerics;
using System.Collections;
using System.Collections.Generic;

namespace engine.joyce.mesh
{
    public class Tools
    {
        private static void _addQuadIndicesXY(
            in joyce.Mesh m,
            uint i0, uint i1, uint i2, uint i3
        )
        {
            m.Indices.Add(i0); m.Indices.Add(i1); m.Indices.Add(i2);
            m.Indices.Add(i2); m.Indices.Add(i1); m.Indices.Add(i3);
        }

        public static void AddQuadXYUV(
            in joyce.Mesh m,
            in Vector3 v0,
            in Vector3 vx,
            in Vector3 vy,
            in Vector2 u0,
            in Vector2 ux,
            in Vector2 uy
        )
        {
            uint idx = (uint) m.Vertices.Count;
            m.Vertices.Add(v0); m.Vertices.Add(v0 + vx); m.Vertices.Add(v0 + vy); m.Vertices.Add(v0 + vx + vy);
            m.UVs.Add(u0); m.UVs.Add(u0 + ux); m.UVs.Add(u0 + uy); m.UVs.Add(u0 + ux + uy);
            _addQuadIndicesXY(m, idx+0, idx+1, idx+2, idx+3);
        }

        private static void _addQuadXY(
            in joyce.Mesh m, in Vector3 v0, in Vector3 vx, in Vector3 vy )
        {
            AddQuadXYUV( m, v0, vx, vy, new Vector2( 0f, 0f ), new Vector2( 1f, 0f ), new Vector2( 0f, 1f ) );
        }


        public static joyce.Mesh CreatePlaneMesh(string name, in Vector2 vSize,
            in Vector2 vUV0, in Vector2 vUVX, in Vector2 vUVY)
        {
            var m = joyce.Mesh.CreateArrayListInstance(name);

            AddQuadXYUV(m, 
                new Vector3(-vSize.X / 2f, -vSize.Y / 2f, 0f),
                new Vector3(vSize.X, 0f, 0f),
                new Vector3(0f, vSize.Y, 0f),
                vUV0, vUVX, vUVY);

            return m;
        }

        public static joyce.Mesh CreatePlaneMesh(in string name, Vector2 vSize)
        {
            var m = joyce.Mesh.CreateArrayListInstance(name);

            AddQuadXYUV(m, new Vector3(-vSize.X / 2f, -vSize.Y / 2f, 0f),
                new Vector3(vSize.X, 0f, 0f),
                new Vector3(0f, vSize.Y, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 0f),
                new Vector2(0f, -1f));

            return m;
        }

        public static joyce.Mesh CreateCubeMesh(string name, float size)
        {
            float h /* half */ = size / 2;

            var m = joyce.Mesh.CreateArrayListInstance(name);

            // Back (-Z)
            _addQuadXY( m, new Vector3(h, -h, -h), new Vector3(-size, 0f, 0f), new Vector3(0f, size, 0f) );
            // Front (positive Z)
            _addQuadXY( m, new Vector3(-h, -h, h), new Vector3(size, 0f, 0f), new Vector3(0f, size, 0f) );
            // Top (positive Y)
            _addQuadXY(m, new Vector3(-h, h, h), new Vector3(size, 0f, 0f), new Vector3(0f, 0f, -size) );
            // Bottom (-Y)
            _addQuadXY(m, new Vector3(-h, -h, -h), new Vector3(size, 0f, 0f), new Vector3(0f, 0f, size));
            // Right (positive X)
            _addQuadXY(m, new Vector3(h, -h, h), new Vector3(0f, 0f, -size), new Vector3(0f, size, 0f));
            // Left (-X)
            _addQuadXY(m, new Vector3(-h, -h, -h), new Vector3(0f, 0f, size), new Vector3(0f, size, 0f));

            return m;
        }

        /**
         * Create a skybox texture cube with inside-faced rectangles.
         * Texture is assumed to be cross shaped, the middle row being the horizon on the xz plane,
         * attached at the second of 4 rectangles are the sky the hell, the sky being "above" the x -z view,
         * the hell right below it.
         */
        public static joyce.Mesh CreateSkyboxMesh(string name, float size,
            in Vector2 vUVOrigin, in Vector2 vUVSize )
        {
            float h /* half */ = size / 2f;

            var m = joyce.Mesh.CreateArrayListInstance(name);

            /*
             *  Note that the direction of the surfaces is mirrored due to the inbound direction of the normals.
             */
            // -Z plane (in front of us)
            AddQuadXYUV(m, new Vector3(-h, -h, -h), new Vector3(size, 0f, 0f), new Vector3(0f, size, 0f),
                vUVOrigin + new Vector2(vUVSize.X * 0.25f, vUVSize.Y * 0.5f), 
                new Vector2(vUVSize.X * 0.25f, vUVSize.Y * 0f),
                new Vector2(vUVSize.X * 0f, vUVSize.Y * (-0.25f)));
            // +Z plane (behind us)
            AddQuadXYUV(m, new Vector3(h, -h, h), new Vector3(-size, 0f, 0f), new Vector3(0f, size, 0f),
                vUVOrigin + new Vector2(vUVSize.X * 0.75f, vUVSize.Y * 0.5f),
                new Vector2(vUVSize.X * 0.25f, vUVSize.Y * 0f),
                new Vector2(vUVSize.X * 0f, vUVSize.Y * (-0.25f)));
            // +Y plane (above us)
            AddQuadXYUV(m, new Vector3(-h, h, -h), new Vector3(size, 0f, 0f), new Vector3(0f, 0f, size),
                vUVOrigin + new Vector2(vUVSize.X * 0.25f, vUVSize.Y * 0.25f),
                    new Vector2(vUVSize.X * 0.25f, vUVSize.Y * 0f),
                    new Vector2(vUVSize.X * 0f, vUVSize.Y * (-0.25f)));
            // Bottom
            AddQuadXYUV(m, new Vector3(-h, -h, h), new Vector3(size, 0f, 0f), new Vector3(0f, 0f, -size),
                vUVOrigin + new Vector2(vUVSize.X * 0.25f, vUVSize.Y * 0.75f),
                        new Vector2(vUVSize.X * 0.25f, vUVSize.Y * 0f),
                        new Vector2(vUVSize.X * 0f, vUVSize.Y * (-0.25f)));
            // Right
            AddQuadXYUV(m, new Vector3(h, -h, -h), new Vector3(0f, 0f, size), new Vector3(0f, size, 0f),
                vUVOrigin + new Vector2(vUVSize.X * 0.5f, vUVSize.Y * 0.5f),
                        new Vector2(vUVSize.X * 0.25f, vUVSize.Y * 0f),
                        new Vector2(vUVSize.X * 0f, vUVSize.Y * (-0.25f)));
            // Left
            AddQuadXYUV(m, new Vector3(-h, -h, h), new Vector3(0f, 0f, -size), new Vector3(0f, size, 0f),
                vUVOrigin + new Vector2(vUVSize.X * 0.0f, vUVSize.Y * 0.5f),
                            new Vector2(vUVSize.X * 0.25f, vUVSize.Y * 0f),
                            new Vector2(vUVSize.X * 0f, vUVSize.Y * (-0.25f)));

            return m;
        }
    }
}
