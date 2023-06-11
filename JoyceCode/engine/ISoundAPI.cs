namespace engine;

public interface ISoundAPI
{
    void PlaySound(string uri);
    void StopSound(string uri);
}