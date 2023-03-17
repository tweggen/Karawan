using System;

namespace engine.audio.components
{
    public struct MovingSound
    {
        public engine.audio.Sound Sound;
        public float MaxDistance;
        public ushort MotionVolume;
        public short MotionPan;
        public float MotionPitch;

        
        public MovingSound(in engine.audio.Sound sound, in float maxDistance, 
            in ushort motionVolume, in short motionPan, in float motionPitch)
        {
            Sound = sound;
            MaxDistance = maxDistance;
            MotionVolume = motionVolume;
            MotionPan = motionPan;
            MotionPitch = motionPitch;
        }
        
        
        public MovingSound(in engine.audio.Sound sound, in float maxDistance, 
            in float motionVolume, in float motionPan, in float motionPitch)
        {
            Sound = sound;
            MaxDistance = maxDistance;
            MotionVolume = (ushort)(motionVolume * 65535f);
            MotionPan = (short)(motionPan * 32767f);
            MotionPitch = motionPitch;
        }

        
        public MovingSound(in engine.audio.Sound sound, in float maxDistance)
        {
            Sound = sound;
            MaxDistance = maxDistance;
        }
    }
}