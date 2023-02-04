using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;

namespace builtin.tools
{
    public class UVProjector
    {
        private Vector3 _o;
        private Vector3 _u;
        private Vector3 _v;

        public Vector2 GetUV(in Vector3 point)
        {
            Vector3 p = point - _o;
            // TXWTODO: Why do we divide by _u or _v twice?
            float u = (float)(Vector3.Dot(p, _u) / _u.Length() / _u.Length());
            float v = (float)(Vector3.Dot(p, _v) / _v.Length() / _v.Length());
            // This was the original code.
            // float u = p.prject(_u) / _u.length();
            // var v = p.project(_v) / _v.length();
            return new Vector2(u, v);
        }

        public Vector2 getUVOfs(in Vector3 point, float uStart, float vStart)
        {
            Vector2 uv = GetUV(point);
            uv.X = uv.X - uStart;
            uv.Y = uv.Y - vStart;

            uv.X = (float)(Math.Round(uv.X * 131072) / 131072);
            uv.Y = (float)(Math.Round(uv.Y * 131072) / 131072);

            if(uv.X<0f)
            {
                uv.X = 0f;
            } 
            else if (uv.X > 1f)
            {
                uv.X = 1f;
            }
            if (uv.Y < 0f)
            {
                uv.Y = 0f;
            }
            else if (uv.Y > 1f)
            {
                uv.Y = 1f;
            }
            return uv;
        }

        public UVProjector(in Vector3 o, in Vector3 uAxis, in Vector3 vAxis)
        {
            _o = o;
            _u = uAxis;
            _v = vAxis;
        }

    }    
}
