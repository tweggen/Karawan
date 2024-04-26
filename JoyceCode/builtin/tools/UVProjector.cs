using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;

namespace builtin.tools
{
    /**
     * Tool to compute u and v coordinates for arbitrary points.
     */
    public class UVProjector
    {
        //public Vector2 TextureResolution = new(512f, 512f);
        
        private Vector2 _uvOffset;
        private Vector2 _uvSize;
        
        /**
         * The reference point for all textures in space. 
         */
        private Vector3 _o;
        
        /**
         * Where does the ui axis go to? 
         */
        private Vector3 _u;
        
        /**
         * Where does the V axis go to?
         */
        private Vector3 _v;

        /**
         * Precomputed factors for the projection because we use them all the time.
         * this is (u.Length() * uvSize.X)^2 (that is , the inverse of it)
         */
        private Vector2 _v2ProjectionFactors;

        public Vector2 GetUV(in Vector3 point)
        {
            Vector3 p = point - _o;
            return new Vector2(
                Vector3.Dot(p, _u) * _v2ProjectionFactors.X,
                Vector3.Dot(p, _v) * _v2ProjectionFactors.Y
                );
        }

        public Vector2 getUVOfs(in Vector3 point, float uStart, float vStart)
        {
            Vector2 uv = GetUV(point);
            uv += _uvOffset;
            uv.X = uv.X - uStart;
            uv.Y = uv.Y - vStart;

            uv.X = (float)(Math.Round(uv.X * 131072) / 131072);
            uv.Y = (float)(Math.Round(uv.Y * 131072) / 131072);

            uv.X = Single.Clamp(uv.X, 0f, 1f);
            uv.Y = Single.Clamp(uv.Y, 0f, 1f);

            return uv;
        }


        private void _computeFactors()
        {
            _v2ProjectionFactors = new(
                (_uvSize.X * _uvSize.X) / _u.LengthSquared(),
                (_uvSize.Y * _uvSize.Y) / _v.LengthSquared()
            );
        }

        
        /**
         * Initialize an instance. This will project any point on the UV plane, returning a
         * valid UV coordinate.
         *
         * @param o
         *     The origin of the UV plane.
         * @param uAxis
         *     The u axis
         * @param vAxis
         *     The v axis
         */
        public UVProjector(in Vector3 o, in Vector3 uAxis, in Vector3 vAxis)
        {
            _o = o;
            _u = uAxis;
            _v = vAxis;
            _uvOffset = new(0f, 0f);
            _uvSize = new(1f, 1f);
            _computeFactors();
        }
        
        
        /**
         * Initialize an instance. This will project any point on the UV plane, returning a
         * valid UV coordinate.
         *
         * @param o
         *     The origin of the UV plane.
         * @param uAxis
         *     The u axis
         * @param vAxis
         *     The v axis
         * @param uvOffset
         *     The offset within the texture
         * @param uvSize
         *     The size of the area in the texture
         */
        public UVProjector(in Vector3 o, in Vector3 uAxis, in Vector3 vAxis, in Vector2 uvOffset, in Vector2 uvSize)
        {
            _o = o;
            _u = uAxis;
            _v = vAxis;
            _uvOffset = uvOffset;
            _uvSize = uvSize;
            _computeFactors();
        }
    }    
}
