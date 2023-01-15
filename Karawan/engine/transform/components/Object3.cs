using System;
using System.Numerics;

namespace Karawan.engine.transform.components
{
    struct Object3
    {
        public bool IsVisible;
        public Quaternion Rotation;
        public Vector3 Position;

        public Object3( bool isVisible, in Quaternion rotation, in Vector3 position)
        {
            IsVisible = isVisible;
            Rotation = rotation;
            Position = position;
        }
    }
}
