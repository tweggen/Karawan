using System;
using System.Collections.Generic;
using System.Text;

namespace engine.news
{
    public class ControllerState
    {
        public DateTime LastInput;
        public int FrontMotion { 
            get {
                int frontMotion = WalkForward - WalkBackward;
                if (frontMotion > 0)
                {
                    frontMotion = 255;
                } else if(frontMotion<0)
                {
                    frontMotion = -255;
                }

                return frontMotion;
            }
        }

        public int RightMotion { 
            get {
                int rightMotion = TurnRight - TurnLeft;
                if (rightMotion > 0)
                {
                    rightMotion = 255;
                } else if(rightMotion<0)
                {
                    rightMotion = -255;
                }

                return rightMotion;
            }
        }

        public int UpMotion { 
            get {
                int upMotion = FlyUp - FlyDown;
                if (upMotion > 0)
                {
                    upMotion = 255;
                } else if(upMotion<0)
                {
                    upMotion = -255;
                }
                return upMotion; 
            }
        }

        public int AnalogForward;
        public int WalkForward;
        public int AnalogBackward;
        public int WalkBackward;


        public int AnalogUp;
        public int FlyUp;
        public int AnalogDown;
        public int FlyDown;
        public int AnalogLeft;
        public int TurnLeft;
        public int AnalogRight;
        public int TurnRight;

        
        public void AnalogToWalkControllerNoLock()
        {
            WalkForward = AnalogForward;
            WalkBackward = AnalogBackward;
            TurnLeft = AnalogLeft;
            TurnRight = AnalogRight;
            FlyUp = AnalogUp;
            FlyDown = AnalogDown;
        }


        public void Reset()
        {
            WalkForward = 0;
            WalkBackward = 0;
            TurnLeft = 0;
            TurnRight = 0;
            FlyUp = 0;
            FlyDown = 0;
            AnalogForward = 0;
            AnalogBackward = 0;
            AnalogRight = 0;
            AnalogLeft = 0;
            AnalogUp = 0;
            AnalogDown = 0;
        }

        public ControllerState()
        {
            Reset();
        }
    }
}
