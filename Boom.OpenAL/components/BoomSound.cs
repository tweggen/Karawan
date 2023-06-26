namespace Boom.OpenAL.components;

public struct BoomSound
{
    public AudioSource AudioSource;

    public BoomSound(in AudioSource audioSource)
    {
        AudioSource = audioSource;
    }
}