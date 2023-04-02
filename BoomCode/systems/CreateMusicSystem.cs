using System;
using Boom;
using DefaultEcs.Resource;

namespace BoomCode.systems
{
    [DefaultEcs.System.With(typeof(engine.audio.components.Music))]
    sealed public class CreateMusicSystem : DefaultEcs.System.AEntitySetSystem<float>
    {
        private engine.Engine _engine;


        protected override void PreUpdate(float dt)
        {
        }

        protected override void PostUpdate(float dt)
        {
        }


        protected override void Update(float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            Span<DefaultEcs.Entity> copiedEntites = stackalloc DefaultEcs.Entity[entities.Length];
            entities.CopyTo(copiedEntites);
            foreach (var entity in entities)
            {
                /*
                 * We start everything that is not started.
                 */
                var cMusic = entity.Get<engine.audio.components.Music>();
                if (cMusic.IsPlaying) continue;
                string resourcePath = engine.GlobalSettings.Get("Engine.ResourcePath");
                CachedSound sound = new(resourcePath + cMusic.Url);
                AudioPlaybackEngine.Instance.PlaySound(sound);
                cMusic.IsPlaying = true;
                entity.Set(cMusic);
            }
        }


        public CreateMusicSystem(engine.Engine engine)
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}

