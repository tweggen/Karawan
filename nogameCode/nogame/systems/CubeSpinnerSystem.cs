using System;


namespace nogame.systems
{
    [DefaultEcs.System.With(typeof(components.CubeSpinner))]
    [DefaultEcs.System.With(typeof(engine.transform.components.Transform3))]
    sealed class CubeSpinnerSystem : DefaultEcs.System.AEntitySetSystem<float>
    {
        engine.transform.API _aTransform;

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

        public CubeSpinnerSystem(engine.Engine engine0)
            : base(engine0.GetEcsWorld())
        {
            _aTransform = engine.I.Get<engine.transform.API>();
        }
    }
}
