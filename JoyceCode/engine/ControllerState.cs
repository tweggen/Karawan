using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public class ControllerState
    {
        public bool Pause;
        public bool WalkFast;
        public int WalkForward;
        public int WalkBackward;
        public int StrafeLeft;
        public int StrafeRight;
        public int TurnLeft;
        public int TurnRight;
        public bool Shoot;
        public bool DebugInfo;


        public void Reset()
        {
            Pause = false;
            WalkFast = false;
            WalkForward = 0;
            WalkBackward = 0;
            StrafeLeft = 0;
            StrafeRight = 0;
            TurnLeft = 0;
            TurnRight = 0;
            Shoot = false;
            DebugInfo = false;
        }

        public ControllerState()
        {
            Reset();
        }
    }
}
