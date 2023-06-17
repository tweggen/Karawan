using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public class ControllerState
    {
        public int FrontMotion { get {
                int frontMotion = WalkForward - WalkBackward;
                if (WalkFast)
                {
                    if (frontMotion > 0)
                    {
                        frontMotion = 255;
                    } else if(frontMotion<0)
                    {
                        frontMotion = -255;
                    }

                }
                return frontMotion; 
            }
        }

        public bool ShowMap;
            
        public bool WalkFast;
        public int WalkForward;
        public int WalkBackward;
        public int TurnLeft;
        public int TurnRight;

        public sbyte ZoomState;

        public void Reset()
        {
            ShowMap = false;
            WalkFast = false;
            WalkForward = 0;
            WalkBackward = 0;
            TurnLeft = 0;
            TurnRight = 0;
        }

        public ControllerState()
        {
            Reset();
        }
    }
}
