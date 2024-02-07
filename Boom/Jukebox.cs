using engine.joyce;
using static engine.Logger;

namespace Boom;

public class Jukebox
{
    private readonly object _lo = new();
    
    private readonly ISoundAPI _aSound;
    private ISound? _soundCurrentSong = null;
    private string _uriCurrentSong = "";

    /*
     * Without caching, first asynchronously load the uiri to a sound source,
     * then start playing.
     */
    public void LoadThenPlaySong(string uri, float volume, bool isLooped, Action onStart, Action onStop)
    {
        lock (_lo)
        {
            if (_uriCurrentSong == uri)
            {
                return;
            }

            _uriCurrentSong = uri;
        }
        _aSound.LoadSound(uri).ContinueWith((Task<ISound> loadTask) =>
        {
            ISound sound = loadTask.Result;
            lock (_lo)
            {
                if (_soundCurrentSong != null)
                {
                    _soundCurrentSong.Stop();
                    _soundCurrentSong.Dispose();
                    _soundCurrentSong = null;
                }
                _soundCurrentSong = sound;
                _soundCurrentSong.Volume = volume;
                _soundCurrentSong.IsLooped = isLooped;
                _soundCurrentSong.SoundMask = 0x00010000;

                _soundCurrentSong.Play();
                try
                {
                    if (onStart != default) onStart();
                }
                catch (Exception e)
                {
                    Error($"Caught exception in onStart() of music {uri}: {e}");
                }
            }
        });
    }
    
    public Jukebox()
    {
        _aSound = engine.I.Get<ISoundAPI>();
    }
}