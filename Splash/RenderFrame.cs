using System.Collections.Generic;
using System.Numerics;

namespace Splash
{
    public class RenderFrame
    {
        public Vector4 ColAmbient;
        public IList<engine.joyce.components.AmbientLight> ListAmbientLights = new();
        public IList<engine.joyce.components.DirectionalLight> ListDirectionalLights = new();
        public IList<engine.joyce.components.PointLight> ListPointLights = new();

        /*
         * Contains the list of parts to render, order is significant.
         * So first into renderbuffers, finally on screen.
         */
        public IList<RenderPart> RenderParts = new();
    }
}
