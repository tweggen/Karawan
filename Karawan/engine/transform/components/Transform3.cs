using System;
using System.Numerics;

namespace Karawan.engine.transform.components
{
    public struct Transform3
    {
        public bool IsVisible;
        public Quaternion Rotation;
        public Vector3 Position;

        public Transform3( bool isVisible, in Quaternion rotation, in Vector3 position)
        {
            IsVisible = isVisible;
            Rotation = rotation;
            Position = position;
        }
    }
}
