namespace Boom.OpenAL.components;

public struct BoomSound
{
    public ISound AudioSource;

    public BoomSound(in ISound audioSource)
    {
        AudioSource = audioSource;
    }
}