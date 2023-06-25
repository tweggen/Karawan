namespace engine;

public interface ISoundAPI : System.IDisposable
{
    void SetupDone();
    void PlaySound(string uri);
    void StopSound(string uri);
}