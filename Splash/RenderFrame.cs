using System.Collections.Generic;
using System.Numerics;

namespace Karawan.platform.cs1.splash
{
    public class RenderFrame
    {
        public Vector4 ColAmbient;
        public IList<engine.joyce.components.AmbientLight> ListAmbientLights;
        public IList<engine.joyce.components.DirectionalLight> ListDirectionalLights;
        public IList<engine.joyce.components.PointLight> ListPointLights;

        public IList<RenderPart> RenderParts;

        public RenderFrame()
        {
            ListAmbientLights = new List<engine.joyce.components.AmbientLight>();
            ListDirectionalLights = new List<engine.joyce.components.DirectionalLight>();
            ListPointLights = new List<engine.joyce.components.PointLight>();

            RenderParts = new List<RenderPart>();
        }
    }
}
