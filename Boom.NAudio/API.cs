﻿using Boom.NAudio.systems;
using engine;
using static engine.Logger;

namespace Boom.NAudio;

public class API : Boom.ISoundAPI
{
    private object _lo = new();

    private bool _haveSettings = false;
    private engine.Engine _engine;
    private CreateMusicSystem _createMusicSystem;
    private AudioPlaybackEngine _audioPlaybackEngine;
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
        AudioPlaybackEngine.Instance.FindCachedSound(
            uri, (CachedSound cachedSound) => { AudioPlaybackEngine.Instance.PlaySound(cachedSound); });
    }


    public void SetupDone()
    {
        _engine.LogicalFrame += _onLogicalFrame;
    }

    public void Dispose()
    {
        AudioPlaybackEngine.Instance.Dispose();
    }


    public API(Engine engine)
    {
        _engine = engine;
        _createMusicSystem = new(engine);
        _updateMovingSoundsSystem = new UpdateMovingSoundSystem(engine);
    }
}
