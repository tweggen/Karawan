using System;

namespace engine.geom
{
    public class Angles
    {
        static public float Snorm(float a)
        {
            while (a < -(float)Math.PI) {
                a += (float)Math.PI * 2f;
            }
            while (a > (float)Math.PI) {
                a -= (float)Math.PI * 2f;
            }
            return a;
        }


        static public float Unorm(float a)
        {
            while (a < 0f)
            {
                a += (float)Math.PI * 2f;
            }
            while (a > 2f * (float)Math.PI)
            {
                a -= (float) Math.PI * 2f;
            }
            return a;
        }
    }
}
