using System;
using System.Numerics;

namespace engine.transform
{
    public class API
    {
        private engine.Engine _engine;
        private systems.PropagateTranslationSystem _propagateTranslationSystem;
        private bool _isDirty;

        public void SetTransforms(
            DefaultEcs.Entity entity,
            bool isVisible,
            uint cameraMask,
            in Quaternion rotation,
            in Vector3 position)
        {
            entity.Set<transform.components.Transform3>(
                new transform.components.Transform3(isVisible, cameraMask, rotation, position));

            /*
             * Write it back to parent transformation
             * TXWTODO: IN a system only for the changed ones.
             */
            {
                var mToParent = Matrix4x4.CreateFromQuaternion(rotation);
                var mTranslate = Matrix4x4.CreateTranslation(position);
                 mToParent = mToParent * mTranslate;
                entity.Set<transform.components.Transform3ToParent>(
                    new transform.components.Transform3ToParent(isVisible, cameraMask, mToParent) );
                _isDirty = true;
            }
        }


        public void GetTransform(DefaultEcs.Entity entity, out transform.components.Transform3 transform3)
        {
            if( entity.Has<transform.components.Transform3>() )
            {
                transform3 = entity.Get<transform.components.Transform3>();
            } else
            {
                transform3 = new transform.components.Transform3(
                    false, 0xffffffff, new Quaternion(), new Vector3()
                ); 
            }
        }


        public void SetVisible(DefaultEcs.Entity entity, bool isVisible)
        {
            components.Transform3 object3; GetTransform(entity, out object3);
            if (object3.IsVisible != isVisible)
            {
                SetTransforms(entity, isVisible, object3.CameraMask, object3.Rotation, object3.Position);
            }
        }


        public void SetRotation(DefaultEcs.Entity entity, in Quaternion rotation)
        {
            components.Transform3 object3; GetTransform(entity, out object3);
            if (object3.Rotation != rotation)
            {
                SetTransforms(entity, object3.IsVisible, object3.CameraMask, rotation, object3.Position);
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
                object3.Position);
        }


        public void SetPosition(DefaultEcs.Entity entity, in Vector3 position)
        {
            components.Transform3 object3; GetTransform(entity, out object3);
            if (object3.Position != position)
            {
                SetTransforms(entity, object3.IsVisible, object3.CameraMask, object3.Rotation, position);
            }
        }


        public void Update()
        {
            if(_isDirty)
            {
                _propagateTranslationSystem.Update(_engine);
                _isDirty = false;
            }
        }


        public API(engine.Engine engine)
        {
            _engine = engine;
            _propagateTranslationSystem = new systems.PropagateTranslationSystem(_engine);
        }
    }
}
