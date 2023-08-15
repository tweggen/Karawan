namespace Boom.OpenAL.components;

public struct BoomSound
{
    public ISound? AudioSource;

    public override string ToString()
    {
        if (AudioSource != null)
        {
            return "AudioSource: ISound";
        }
        else
        {
            return "AudioSource: null";
        }
    }

    public BoomSound(in ISound? audioSource)
    {
        AudioSource = audioSource;
    }
}