namespace Boom.components
{

    public struct BoomSound
    {
        public Boom.Sound Sound;

        public BoomSound(in Boom.Sound sound)
        {
            Sound = sound;
        }
    }

}