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
#if false
                string resourcePath = _engine.GetConfigParam("Engine.ResourcePath");
                using (var vorbis = new NVorbis.VorbisReader(resourcePath + cMusic.Url))
                {
                    var sound = vorbis.Wave.WaveOut();
                    AudioPlaybackEngine.Instance.PlaySound(sound);
                }
#endif
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

