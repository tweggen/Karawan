namespace Boom;

public interface ISoundAPI : System.IDisposable
{
    uint SoundMask { get; set; }
    
    void SetupDone();

    Task<ISound> LoadSound(string uri);
    ISound FindSound(string uri);
}