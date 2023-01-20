using System;
using System.Numerics;

namespace Karawan.engine.transform
{
    class API
    {
        private engine.Engine _engine;
        private systems.PropagateTranslationSystem _propagateTranslationSystem;
        private bool _isDirty;

        public void SetTransforms(DefaultEcs.Entity entity, 
            bool isVisible,
            in Quaternion rotation,
            in Vector3 position)
        {
            entity.Set<transform.components.Object3>(
                new transform.components.Object3(
                    isVisible, rotation, position));
            {
                var mToParent = Matrix4x4.CreateFromQuaternion(rotation);
                var mTranslate = Matrix4x4.CreateTranslation(position);
                mToParent = mTranslate * mToParent;
                entity.Set<transform.components.Object3ToParentMatrix>(
                    new transform.components.Object3ToParentMatrix(mToParent) );
                _isDirty = true;
            }
        }


        public transform.components.Object3 GetTransform(DefaultEcs.Entity entity)
        {
            if( entity.Has<transform.components.Object3>() )
            {
                return entity.Get<transform.components.Object3>();
            } else
            {
                return new transform.components.Object3();
            }
        }


        public void SetVisible(DefaultEcs.Entity entity, bool isVisible)
        {
            var object3 = GetTransform(entity);
            SetTransforms(entity, isVisible, object3.Rotation, object3.Position);
        }


        public void SetRotation(DefaultEcs.Entity entity, Quaternion rotation)
        {
            var object3 = GetTransform(entity);
            SetTransforms(entity, object3.IsVisible, rotation, object3.Position);
        }


        public void SetPosition(DefaultEcs.Entity entity, Vector3 position)
        {
            var object3 = GetTransform(entity);
            SetTransforms(entity, object3.IsVisible, object3.Rotation, position);
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
