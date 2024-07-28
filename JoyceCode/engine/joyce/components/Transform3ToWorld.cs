using System;
using System.Numerics;

namespace engine.joyce.components
{
    public struct Transform3ToWorld
    {
        public static readonly uint Visible = 1;
        public uint CameraMask;
        public uint Flags;
        
        public Matrix4x4 Matrix;

        public bool IsVisible
        {
            get => 0 != (Flags & Visible);
            set {
                if (value)
                {
                    Flags |= Visible;
                }
                else
                {
                    Flags &= ~Visible;
                }
            }
        } 

        public override string ToString()
        {
            return $"CameraMask={CameraMask:X}, Matrix={Matrix}";
        }

        public Transform3ToWorld( 
            uint cameraMask,
            uint flags,
            in Matrix4x4 matrix 
            )
        {
            CameraMask = cameraMask;
            Flags = flags;
            Matrix = matrix;
        }
    }
}
