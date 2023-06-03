using System;
using System.Numerics;

namespace engine.transform.components
{
    public struct Transform3
    {
        public bool IsVisible;
        public uint CameraMask;
        public Quaternion Rotation;
        public Vector3 Position;

        public override string ToString()
        {
            return $"{base.ToString()}, IsVisible={IsVisible}, CameraMask={CameraMask:X}, Rotation={Rotation}, Position={Position}";
        }
        
        public Transform3(bool isVisible, uint cameraMask, in Quaternion rotation, in Vector3 position)
        {
            IsVisible = isVisible;
            CameraMask = cameraMask;
            Rotation = rotation;
            Position = position;
        }
    }
}
