using engine;
using static engine.Logger;

namespace boom.naudio;

public class API : Boom.ISoundAPI
{
    private object _lo = new();

    private bool _haveSettings = false;
    private engine.Engine _engine;
    private boom.naudio.systems.CreateMusicSystem _createMusicSystem;
    private naudio.AudioPlaybackEngine _audioPlaybackEngine;
    private systems.UpdateMovingSoundSystem _updateMovingSoundsSystem;
    private bool _traceStartStop;

    public bool TraceStartStop()
    {
        lock (_lo)
        {
            if (!_haveSettings)
            {
                _traceStartStop = engine.GlobalSettings.Get("boom.AudioPlaybackEngine.TraceStartStop") == "true";
                _haveSettings = true;
            }

            return _traceStartStop;
        }
    }


    private void _onLogicalFrame(object sender, float dt)
    {
        _createMusicSystem.Update(dt);
        _updateMovingSoundsSystem.Update(dt);
    }


    public void StopSound(string uri)
    {
        ErrorThrow("Not yet implemented", (m) => new InvalidOperationException(m));
    }


    public void PlaySound(string uri)
    {
        naudio.AudioPlaybackEngine.Instance.FindCachedSound(
            uri, (naudio.CachedSound cachedSound) => { naudio.AudioPlaybackEngine.Instance.PlaySound(cachedSound); });
    }


    public void SetupDone()
    {
        _engine.LogicalFrame += _onLogicalFrame;
    }


    public API(Engine engine)
    {
        _engine = engine;
        _createMusicSystem = new(engine);
        _updateMovingSoundsSystem = new boom.naudio.systems.UpdateMovingSoundSystem(engine);
    }
}
