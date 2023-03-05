namespace engine.audio.components
{

    public struct MovingSound
    {
        public engine.audio.Sound Sound;
        public float MaxDistance;
        public float MotionVolume;
        public float MotionPitch;

        public MovingSound(in engine.audio.Sound sound, in float maxDistance, in float motionVolume, in float motionPitch)
        {
            Sound = sound;
            MaxDistance = maxDistance;
            MotionVolume = motionVolume;
            MotionPitch = motionPitch;
        }
    }
}