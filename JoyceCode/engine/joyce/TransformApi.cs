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

        
        private static Transform3 _cT3Default = new(
            false, 0,
            Quaternion.Identity,    
            new Vector3(99999f,99999f,99999f),
            Vector3.One
        );

        
        static public void CreateTransform3ToParent(in Transform3 cTransform3, out Matrix4x4 mat)
        {
            var mRotate = Matrix4x4.CreateFromQuaternion(cTransform3.Rotation);
            var mTranslate = Matrix4x4.CreateTranslation(cTransform3.Position);
            mat = mRotate * mTranslate;
        }


        public void SetTransforms(
            Entity entity,
            bool isVisible,
            uint cameraMask,
            in Quaternion rotation,
            in Vector3 position)
        {
            ref var cT3 = ref _findT3(entity);
            cT3.IsVisible = isVisible;
            cT3.CameraMask = cameraMask;
            cT3.Rotation = rotation;
            cT3.Position = position;
            _writeTransformToParent(entity, cT3);
        }


        private void _writeTransformToParent(in Entity entity, in Transform3 cT3)
        {
            var mScale = Matrix4x4.CreateScale(cT3.Scale);
            var mRotate = Matrix4x4.CreateFromQuaternion(cT3.Rotation);
            var mTranslate = Matrix4x4.CreateTranslation(cT3.Position);
            var mToParent = mScale * mRotate * mTranslate;
            entity.Set(new Transform3ToParent(cT3.IsVisible, cT3.CameraMask, mToParent) );
            _isDirty = true;
        }
        

        public void SetTransforms(
            Entity entity,
            bool isVisible,
            uint cameraMask,
            in Quaternion rotation,
            in Vector3 position,
            in Vector3 scale)
        {
            ref var cT3 = ref _findT3(entity);
            cT3.IsVisible = isVisible;
            cT3.CameraMask = cameraMask;
            cT3.Rotation = rotation;
            cT3.Position = position;
            cT3.Scale = scale;
            _writeTransformToParent(entity, cT3);
        }


        public void SetTransform(
            Entity entity,
            in Quaternion rotation,
            in Vector3 position)
        {
            ref var cT3 = ref _findT3(entity);
            cT3.Rotation = rotation;
            cT3.Position = position;
            _writeTransformToParent(entity, cT3);
        }
        
        
        public void SetTransform(
            Entity entity,
            in Quaternion rotation,
            in Vector3 position,
            in Vector3 scale)
        {
            ref var cT3 = ref _findT3(entity);
            cT3.Rotation = rotation;
            cT3.Position = position;
            cT3.Scale = scale;
            _writeTransformToParent(entity, cT3);
        }


        private ref Transform3ToParent _findP3(in Entity entity, in Transform3 cT3)
        {
            if (!entity.Has<Transform3ToParent>())
            {
                _writeTransformToParent(entity, cT3);
            }

            return ref entity.Get<Transform3ToParent>();
        }


        private ref Transform3 _findT3(in Entity entity)
        {
            if (!entity.Has<Transform3>())
            {
                entity.Set(_cT3Default);
            }
            return ref entity.Get<Transform3>();
        }


        public ref Transform3 GetTransform(in Entity entity)
        {
            return ref _findT3(entity);
        }
        

        public void SetVisible(Entity entity, bool isVisible)
        {
            ref Transform3 cT3 = ref _findT3(entity);
            if (cT3.IsVisible != isVisible)
            {
                /*
                 * No need to recompute transform matrix.
                 */
                cT3.IsVisible = isVisible;
                _findP3(entity, cT3).IsVisible = isVisible;
            }
        }

        
        public void SetCameraMask(Entity entity, uint cameraMask) 
        {
            ref Transform3 cT3 = ref _findT3(entity);
            if (cT3.CameraMask != cameraMask)
            {
                /*
                 * No need to recompute transform matrix.
                 */
                cT3.CameraMask = cameraMask;
                _findP3(entity, cT3).CameraMask = cameraMask;
            }
        }


        public void SetRotation(in Entity entity, in Quaternion rotation)
        {
            ref var cT3 = ref _findT3(entity);
            if (cT3.Rotation != rotation)
            {
                cT3.Rotation = rotation;
                _writeTransformToParent(entity, cT3);
            }
        }


        public void AppendRotation(Entity entity, in Quaternion qRotateBy)
        {
            ref var cT3 = ref entity.Get<Transform3>();
            cT3.Rotation = Quaternion.Concatenate(cT3.Rotation, qRotateBy);
            _writeTransformToParent(entity, cT3);
        }


        public void SetPosition(Entity entity, in Vector3 position)
        {
            ref var cT3 = ref _findT3(entity);
            if (cT3.Position != position)
            {
                cT3.Position = position;
                _writeTransformToParent(entity, cT3);
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


        public TransformApi()
        {
            _engine = I.Get<Engine>();
            _propagateTranslationSystem = new systems.PropagateTranslationSystem();
        }
    }
}
