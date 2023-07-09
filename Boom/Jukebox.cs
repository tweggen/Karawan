using engine.joyce;

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
    public void LoadThenPlaySong(string uri, float volume)
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
                _soundCurrentSong.Play();
            }
        });
    }
    
    public Jukebox()
    {
        _aSound = engine.Implementations.Get<ISoundAPI>();
    }
}