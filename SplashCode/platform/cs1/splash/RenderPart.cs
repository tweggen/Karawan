using Karawan.platform.cs1;
using System;
using System.Collections.Generic;
using System.Text;

namespace Karawan.platform.cs1.splash
{
    public class RenderPart
    {
        // Camera parameters
        public engine.transform.components.Transform3ToWorld Transform3ToWorld;
        public engine.joyce.components.Camera3 Camera3;
        public CameraOutput CameraOutput;

        public RenderPart()
        {
        }
    }
}
