using System;
using System.Numerics;

namespace engine.transform.components
{
    public struct Transform3ToWorld
    {
        public uint CameraMask;
        public Matrix4x4 Matrix;

        public override string ToString()
        {
            return $"CameraMask={CameraMask:X}, Matrix={Matrix}";
        }

        public Transform3ToWorld( 
            uint cameraMask,
            in Matrix4x4 matrix 
            )
        {
            CameraMask = cameraMask;
            Matrix = matrix;
        }
    }
}
