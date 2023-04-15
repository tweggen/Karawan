using System;
using BepuPhysics;

namespace engine.audio.components
{
    public struct MovingSound
    {
        /*
         * Designed to take 16 bytes.
         */
        public engine.audio.Sound Sound;
        public float MaxDistance;
        public ushort MotionVolume;
        public const float MotionVolumeMax = 65535f;
        public sbyte MotionPan;
        public const float MotionPanMax = 127f;
        /*
         * The number of frames accumulated so far in this structure.
         */
        public byte NFrames = 0;
        public float MotionPitch;

        public override string ToString()
        {
            return $"{base.ToString()}, Sound={Sound}, MaxDistance={MaxDistance}, MotionVolume={MotionVolume}, MotionPan={MotionPan}, MotionPitch={MotionPitch}, NFrames={NFrames}";
        }
        
        public void AddFrame(in MovingSound cNewFrame)
        {
            if (0 == NFrames)
            {
                MotionVolume = cNewFrame.MotionVolume;
                MotionPan = cNewFrame.MotionPan;
                MotionPitch = cNewFrame.MotionPitch;
                NFrames = 1;
            }
            else
            {
                /*
                 * We take the maximal volume because players notice when something
                 * should have been close.
                 */
#if true
                MotionVolume = UInt16.Max(MotionVolume, cNewFrame.MotionVolume);
#else
                /*
                 * This would be the averaged volume
                 */
                MotionVolume = (ushort) ((int)Math.Min(
                    MotionVolumeMax,
                    (float)NFrames * (float)MotionVolume + (float)cNewFrame.MotionVolume) / (float)(NFrames + 1)
                    );
#endif
                MotionPan = (sbyte)((int)
                    Math.Min(
                        MotionPanMax,
                        Math.Max(
                            -MotionPanMax,
                            ((float)NFrames * (float)MotionPan + (float)cNewFrame.MotionPan) / (float)(NFrames + 1)
                        )
                    )
                );
                MotionPitch = ((float)NFrames * (float)MotionPitch + (float)cNewFrame.MotionPitch) / (float)(NFrames + 1);
                NFrames++;
            }
        }
        
        public MovingSound(in engine.audio.Sound sound, in float maxDistance, 
            in ushort motionVolume, in sbyte motionPan, in float motionPitch)
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
            MotionVolume = (ushort)(motionVolume * MotionVolumeMax);
            MotionPan = (sbyte)(motionPan * MotionPanMax);
            MotionPitch = motionPitch;
        }

        
        public MovingSound(in engine.audio.Sound sound, in float maxDistance)
        {
            Sound = sound;
            MaxDistance = maxDistance;
            MotionPitch = 1;
        }
    }
}