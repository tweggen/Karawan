﻿using System;
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

        public int UpMotion { 
            get {
                int upMotion = FlyUp - FlyDown;
                if (WalkFast)
                {
                    if (upMotion > 0)
                    {
                        upMotion = 255;
                    } else if(upMotion<0)
                    {
                        upMotion = -255;
                    }

                }
                return upMotion; 
            }
        }

        public bool WalkFast;
        public int WalkForward;
        public int WalkBackward;
        public int FlyUp;
        public int FlyDown;
        public int TurnLeft;
        public int TurnRight;

        public void Reset()
        {
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