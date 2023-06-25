namespace boom.components
{

    public struct BoomSound
    {
        public naudio.Sound Sound;

        public BoomSound(in naudio.Sound sound)
        {
            Sound = sound;
        }
    }

}