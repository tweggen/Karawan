﻿using System;
using engine;


namespace nogame.systems
{
    [DefaultEcs.System.With(typeof(components.CubeSpinner))]
    [DefaultEcs.System.With(typeof(engine.joyce.components.Transform3))]
    sealed class CubeSpinnerSystem : DefaultEcs.System.AEntitySetSystem<float>
    {
        engine.joyce.TransformApi _aTransform;

        /**
         * Rotate each 
         */
        protected override void Update(float dt, ReadOnlySpan<DefaultEcs.Entity> entities )
        {
            foreach(var entity in entities)
            {
                _aTransform.AppendRotation(entity, entity.Get<components.CubeSpinner>().Spin);
            }
        }

        public CubeSpinnerSystem()
            : base(I.Get<Engine>().GetEcsWorldNoAssert())
        {
            _aTransform = engine.I.Get<engine.joyce.TransformApi>();
        }
    }
}
