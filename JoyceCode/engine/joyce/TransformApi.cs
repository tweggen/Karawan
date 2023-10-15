using DefaultEcs;
using System;
using System.Numerics;
using engine.joyce.components;

namespace engine.joyce
{
    public class TransformApi
    {
        private engine.Engine _engine;
        private systems.PropagateTranslationSystem _propagateTranslationSystem;
        private bool _isDirty;


        static public void CreateTransform3ToParent(in Transform3 cTransform3, out Matrix4x4 mat)
        {
            var mRotate = Matrix4x4.CreateFromQuaternion(cTransform3.Rotation);
            var mTranslate = Matrix4x4.CreateTranslation(cTransform3.Position);
            mat = mRotate * mTranslate;
        }

        public void SetTransforms(
            DefaultEcs.Entity entity,
            bool isVisible,
            uint cameraMask,
            in Quaternion rotation,
            in Vector3 position)
        {
            SetTransforms(entity, isVisible, cameraMask, rotation, position, Vector3.One);
        }
        

        public void SetTransforms(
            DefaultEcs.Entity entity,
            bool isVisible,
            uint cameraMask,
            in Quaternion rotation,
            in Vector3 position,
            in Vector3 scale)
        {
            entity.Set(new Transform3(isVisible, cameraMask, rotation, position, scale));

            /*
             * Write it back to parent transformation
             * TXWTODO: IN a system only for the changed ones.
             */
            {
                var mScale = Matrix4x4.CreateScale(scale);
                var mRotate = Matrix4x4.CreateFromQuaternion(rotation);
                var mTranslate = Matrix4x4.CreateTranslation(position);
                var mToParent = mScale * mRotate * mTranslate;
                entity.Set(new Transform3ToParent(isVisible, cameraMask, mToParent) );
                _isDirty = true;
            }
        }


        public void SetTransform(
            DefaultEcs.Entity entity,
            in Quaternion rotation,
            in Vector3 position)
        {
            SetTransform(entity, rotation, position, Vector3.One);
        }
        
        
        public void SetTransform(
            DefaultEcs.Entity entity,
            in Quaternion rotation,
            in Vector3 position,
            in Vector3 scale)
        {
            components.Transform3 transform3;
            GetTransform(entity, out transform3);
            entity.Set(new Transform3(transform3.IsVisible, transform3.CameraMask, rotation, position, scale));
            {
                var mScale = Matrix4x4.CreateScale(scale);
                var mRotate = Matrix4x4.CreateFromQuaternion(rotation);
                var mTranslate = Matrix4x4.CreateTranslation(position);
                var mToParent = mScale * mRotate * mTranslate;
                entity.Set(new Transform3ToParent(transform3.IsVisible, transform3.CameraMask, mToParent));
                _isDirty = true;
            }
        }


        public void GetTransform(DefaultEcs.Entity entity, out Transform3 transform3)
        {
            if( entity.Has<Transform3>() )
            {
                transform3 = entity.Get<Transform3>();
            } else
            {
                transform3 = new Transform3(
                    false, 0,
                    Quaternion.Identity, 
                    new Vector3(99999f,99999f,99999f),
                    Vector3.One
                ); 
            }
        }


        public void SetVisible(DefaultEcs.Entity entity, bool isVisible)
        {
            components.Transform3 object3; GetTransform(entity, out object3);
            if (object3.IsVisible != isVisible)
            {
                SetTransforms(entity, isVisible, object3.CameraMask, object3.Rotation, object3.Position, object3.Scale);
            }
        }

        public void SetCameraMask(DefaultEcs.Entity entity, uint cameraMask) 
        {
            components.Transform3 object3; GetTransform(entity, out object3);
            if (object3.CameraMask != cameraMask)
            {
                SetTransforms(entity, object3.IsVisible, cameraMask, object3.Rotation, object3.Position, object3.Scale);
            }
        }


        public void SetRotation(DefaultEcs.Entity entity, in Quaternion rotation)
        {
            components.Transform3 object3; GetTransform(entity, out object3);
            if (object3.Rotation != rotation)
            {
                SetTransforms(entity, object3.IsVisible, object3.CameraMask, rotation, object3.Position, object3.Scale);
            }
        }


        public void AppendRotation(DefaultEcs.Entity entity, in Quaternion rotation)
        {
            components.Transform3 object3; GetTransform(entity, out object3);

            SetTransforms(entity, 
                object3.IsVisible,
                object3.CameraMask,
                // rotation, 
                Quaternion.Concatenate( object3.Rotation, rotation ), 
                object3.Position,
                object3.Scale);
        }


        public void SetPosition(DefaultEcs.Entity entity, in Vector3 position)
        {
            components.Transform3 object3; GetTransform(entity, out object3);
            if (object3.Position != position)
            {
                SetTransforms(entity, object3.IsVisible, object3.CameraMask, object3.Rotation, position, object3.Scale);
            }
        }


        public void Update()
        {
            // if(_isDirty)
            {
                _propagateTranslationSystem.Update(_engine);
                _isDirty = false;
            }
        }


        public TransformApi(engine.Engine engine)
        {
            _engine = engine;
            _propagateTranslationSystem = new systems.PropagateTranslationSystem(_engine);
        }
    }
}
