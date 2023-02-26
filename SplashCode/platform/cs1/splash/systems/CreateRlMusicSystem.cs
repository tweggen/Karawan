
using DefaultEcs.Resource;
using System;


namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.audio.components.Music))]
    //[DefaultEcs.System.Without(typeof(splash.components.RlMusic))]

    sealed public class CreateRlMusicSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
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
            foreach (var entity in entities)
            {
                if (entity.Has<splash.components.RlMusic>())
                {
                    var cRlMusic = entity.Get<splash.components.RlMusic>();
                    Raylib_CsLo.Raylib.UpdateMusicStream(cRlMusic.MusicEntry.RlMusic);
                }
                else
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
