namespace Boom.NAudio.components;

public struct BoomSound
{
    public Sound Sound;

    public BoomSound(in Sound sound)
    {
        Sound = sound;
    }
}

