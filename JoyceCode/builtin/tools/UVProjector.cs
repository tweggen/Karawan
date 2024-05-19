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
        private Vector2 _textureSize = new(1024f, 1024f);

        /**
         * The offset to add to have UV begin mid-pixel
         */
        private Vector2 _pixelOffset;
        
        /**
         * The offset to adjust from perfect 0 to perfect 1 texture
         * to a texture that starts and ends in the middle of a pixel.
         */
        private Vector2 _pixelScale;
        
        private Vector2 _uvOffset;
        private Vector2 _uvSize;
        private Vector2 _uvMin;
        private Vector2 _uvMax;
        
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
         *
         * Not considering texture pixel adjustment.
         */
        private Vector2 _v2ProjectionFactorsUncorrected;

        
        public Vector2 GetUV(in Vector3 point, float uStart, float vStart)
        {
            Vector3 p = point - _o;
            Vector2 uv = new Vector2(
                Vector3.Dot(p, _u) * _v2ProjectionFactorsUncorrected.X - uStart,
                Vector3.Dot(p, _v) * _v2ProjectionFactorsUncorrected.Y - vStart
                ) + _uvOffset;

            uv.X = Single.Clamp(uv.X, _uvMin.X, _uvMax.X);
            uv.Y = Single.Clamp(uv.Y, _uvMin.Y, _uvMax.Y);

            uv = uv * _pixelScale + _pixelOffset;
            
            return uv;
        }


        private void _computeFactors()
        {
            _uvMin = _uvOffset;
            _uvMax = _uvOffset + _uvSize;
            _v2ProjectionFactorsUncorrected = new(
                 _uvSize.X/_u.LengthSquared(),
                _uvSize.Y/_v.LengthSquared()
            );
            _pixelScale = new Vector2(1f - 1f / _textureSize.X, 1f - 1f / _textureSize.Y);
            _pixelOffset = new Vector2(0.5f / _textureSize.X, 0.5f / _textureSize.Y);
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
        public UVProjector(in Vector3 o, in Vector3 uAxis, in Vector3 vAxis, in Vector2 uvOffset, in Vector2 uvSize, in Vector2 textureSize)
        {
            _o = o;
            _u = uAxis;
            _v = vAxis;
            _textureSize = textureSize;
            _uvOffset = uvOffset; //+ new Vector2(0.5f/textureSize.X,0.5f/textureSize.Y);
            _uvSize = uvSize; // - new Vector2(1f/textureSize.X, 1f/textureSize.Y);
            _computeFactors();
        }
    }    
}
