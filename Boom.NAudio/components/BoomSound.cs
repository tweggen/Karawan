namespace boom.naudio.components;

public struct BoomSound
{
    public boom.naudio.Sound Sound;

    public BoomSound(in boom.naudio.Sound sound)
    {
        Sound = sound;
    }
}

