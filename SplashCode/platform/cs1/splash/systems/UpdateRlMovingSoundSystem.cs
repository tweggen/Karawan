#if false
using DefaultEcs.Resource;
using System;

namespace Karawan.platform.cs1.splash.systems
{

    [DefaultEcs.System.With(typeof(engine.audio.components.MovingSound))]
    sealed public class UpdateRlMovingSoundSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
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
            Span<DefaultEcs.Entity> copiedEntites = stackalloc DefaultEcs.Entity[entities.Length];
            entities.CopyTo(copiedEntites);
            foreach (var entity in entities)
            {
                /*
                 * We need to iterate through all moving sounds, finally creating
                 * or updating the sounds depending on distance
                 */
                /*
                 * We allocate music entries only as soon we find them,
                 */
                if (entity.Has<engine.audio.components.Music>())
                {
                    var cMusic = entity.Get<engine.audio.components.Music>();
                    var jMusic = new engine.audio.Music(cMusic.Url);
                    entity.Set(new ManagedResource<engine.audio.Music, RlMusicEntry>(jMusic));
                }
            }
        }


        public unsafe CreateRlMusicSystem(
            engine.Engine engine
        )
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}

}
#endif