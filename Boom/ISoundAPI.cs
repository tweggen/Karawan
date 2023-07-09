namespace Boom;

public interface ISoundAPI : System.IDisposable
{
    void SetupDone();
    Task<ISound> LoadSound(string uri);
    ISound FindSound(string uri);
}