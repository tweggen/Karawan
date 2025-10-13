using System;
using System.Collections.Generic;
using System.Text;

namespace engine.news
{
    public class ControllerState
    {
        public DateTime LastInput;
        public int BumpersMotion { 
            get {
                int bumpersMotion = AnalogRight2 - AnalogLeft2;
                if (bumpersMotion > 255)
                {
                    bumpersMotion = 255;
                } 
                else if(bumpersMotion<-255)
                {
                    bumpersMotion = -255;
                }

                return bumpersMotion;
            }
        }

        public int AnalogLeftStickHoriz { 
            get {
                int rightMotion = AnalogLeftStickRight - AnalogLeftStickLeft;
                if (rightMotion > 255)
                {
                    rightMotion = 255;
                } 
                else if(rightMotion<-255)
                {
                    rightMotion = -255;
                }

                return rightMotion;
            }
        }

        public int AnalogLeftStickVert { 
            get {
                int upMotion = AnalogLeftStickUp - AnalogLeftStickDown;
                if (upMotion > 255)
                {
                    upMotion = 255;
                } 
                else if(upMotion< -255)
                {
                    upMotion = -255;
                }
                return upMotion; 
            }
        }

        public int WASDHoriz
        {
            get {
                int rightMotion = WASDRight - WASDLeft;
                if (rightMotion > 255)
                {
                    rightMotion = 255;
                } 
                else if(rightMotion<-255)
                {
                    rightMotion = -255;
                }

                return rightMotion;
            }
        }
        
        public int WASDVert
        {
            get {
                int upMotion = WASDUp - WASDDown;
                if (upMotion > 255)
                {
                    upMotion = 255;
                } 
                else if(upMotion<-255)
                {
                    upMotion = -255;
                }
                return upMotion; 
            }
        }


        public int TouchLeftStickHoriz
        {
            get {
                int rightMotion = TouchLeftStickRight - TouchLeftStickLeft;
                if (rightMotion > 255)
                {
                    rightMotion = 255;
                } 
                else if(rightMotion<-255)
                {
                    rightMotion = -255;
                }

                return rightMotion;
            }
        }

        
        public int TouchLeftStickVert
        {
            get {
                int upMotion = TouchLeftStickUp - TouchLeftStickDown;
                if (upMotion > 255)
                {
                    upMotion = 255;
                } 
                else if(upMotion<-255)
                {
                    upMotion = -255;
                }
                return upMotion; 
            }
        }
        
        
        public int TouchLeftPushHoriz
        {
            get {
                int rightMotion = TouchLeftPushRight - TouchLeftPushLeft;
                if (rightMotion > 255)
                {
                    rightMotion = 255;
                } 
                else if(rightMotion<-255)
                {
                    rightMotion = -255;
                }

                return rightMotion;
            }
        }

        
        public int TouchLeftPushVert
        {
            get {
                int upMotion = TouchLeftPushUp - TouchLeftPushDown;
                if (upMotion > 255)
                {
                    upMotion = 255;
                } 
                else if(upMotion<-255)
                {
                    upMotion = -255;
                }
                return upMotion; 
            }
        }
        
        
        public int AnalogRight2;
        public int AnalogLeft2;
        public int AnalogLeftStickUp;
        public int AnalogLeftStickDown;
        public int AnalogLeftStickLeft;
        public int AnalogLeftStickRight;
        public int WASDUp;
        public int WASDDown;
        public int WASDLeft;
        public int WASDRight;
        public int TouchLeftStickUp;
        public int TouchLeftStickDown;
        public int TouchLeftPushUp;
        public int TouchLeftPushDown;
        public int TouchLeftStickLeft;
        public int TouchLeftStickRight;
        public int TouchLeftPushLeft;
        public int TouchLeftPushRight;
        
        public void Reset()
        {
            AnalogRight2 = 0;
            AnalogLeft2 = 0;
            AnalogLeftStickRight = 0;
            AnalogLeftStickLeft = 0;
            AnalogLeftStickUp = 0;
            AnalogLeftStickDown = 0;
            WASDUp = 0;
            WASDDown = 0;
            WASDLeft = 0;
            WASDRight = 0;
        }

        public ControllerState()
        {
            Reset();
        }
    }
}
