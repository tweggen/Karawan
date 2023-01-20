using Karawan.engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.joyce.components.Mesh))]
    [DefaultEcs.System.With(typeof(engine.transform.components.Object3ToWorldMatrix))]
    [DefaultEcs.System.Without(typeof(splash.components.RlMesh))]
    /**
     * Create a raylib mesh for every mesh defined.
     * Totally unoptimized.
     */
    sealed class CreateRlMeshesSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;

        protected override void PreUpdate(engine.Engine state)
        {
        }

        protected override void PostUpdate(engine.Engine state)
        {
        }

        protected override void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            foreach(var entity in entities)
            {
                var cMesh = entity.Get<engine.joyce.components.Mesh>();
                var rMesh = MeshGenerator.CreateRaylibMesh(cMesh);
                entity.Set<splash.components.RlMesh>(new splash.components.RlMesh(rMesh));
            }
        }

        public CreateRlMeshesSystem(engine.Engine engine)
            : base( engine.GetEcsWorld() )
        {
            _engine = engine;
        }
    }
}
