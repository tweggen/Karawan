namespace Boom;

public interface ISoundAPI : System.IDisposable
{
    void SetupDone();

    void SuspendOutput();
    void ResumeOutput();

    Task<ISound> LoadSound(string uri);
    ISound FindSound(string uri);
}