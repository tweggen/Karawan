using System;
using Boom;
using DefaultEcs.Resource;

namespace Boom.OpenAL.systems;

    
[DefaultEcs.System.With(typeof(engine.audio.components.Music))]
sealed public class CreateMusicSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private engine.Engine _engine;
    private API _api;

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
            try
            {
                // TXWTODO: This leaks...
                AudioSource audioSource = _api.CreateAudioSource(cMusic.Url);
                audioSource.Play();
            } catch(Exception ex)
            {
                // Do nothing. This way it might think it plays.
            }
            cMusic.IsPlaying = true;
            entity.Set(cMusic);
        }
    }


    public CreateMusicSystem(engine.Engine engine, API api)
        : base(engine.GetEcsWorld())
    {
        _engine = engine;
        _api = api;
    }
}