using System;
using System.Numerics;

namespace engine.joyce.components
{
    public struct Transform3
    {
        public bool IsVisible;
        public uint CameraMask;
        public Quaternion Rotation;
        public Vector3 Position;
        public Vector3 Scale;

        
        public override string ToString()
        {
            return $"IsVisible={IsVisible}, CameraMask={CameraMask:X}, Rotation={Rotation}, Scale={Scale}, Position={Position}";
        }
        
        
        public Transform3(bool isVisible, uint cameraMask, in Quaternion rotation, in Vector3 position)
        {
            IsVisible = isVisible;
            CameraMask = cameraMask;
            Rotation = rotation;
            Position = position;
            Scale = Vector3.One;
        }

        
        public Transform3(bool isVisible, uint cameraMask, in Quaternion rotation, in Vector3 position, in Vector3 scale)
        {
            IsVisible = isVisible;
            CameraMask = cameraMask;
            Rotation = rotation;
            Position = position;
            Scale = scale;
        }
    }
}
