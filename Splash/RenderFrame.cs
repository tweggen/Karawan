using System;
using System.Collections.Generic;
using System.Numerics;
using engine.joyce.components;

namespace Splash
{
    public class RenderFrame
    {
        public uint FrameNumber;
        public DateTime StartCollectTime;
        public DateTime EndCollectTime;
        public LightCollector LightCollector = new();
        public FrameStats FrameStats = new();
        
        public IList<engine.joyce.components.AmbientLight> ListAmbientLights = new List<AmbientLight>();
        public IList<engine.joyce.components.DirectionalLight> ListDirectionalLights = new List<DirectionalLight>();
        public IList<engine.joyce.components.PointLight> ListPointLights = new List<PointLight>();

        /*
         * Contains the list of parts to render, order is significant.
         * So first into renderbuffers, finally on screen.
         */
        public IList<RenderPart> RenderParts = new List<RenderPart>();
    }
}
