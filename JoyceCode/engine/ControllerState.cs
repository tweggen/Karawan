using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public class ControllerState
    {
        public bool WalkFast;
        public int WalkForward;
        public int WalkBackward;
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
