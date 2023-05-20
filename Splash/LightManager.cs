using System;
using System.Numerics;

namespace Splash
{
    public class LightManager
    {
        private object _lo = new();

        private engine.Engine _engine;
        private IThreeD _threeD;

        public const int MAX_LIGHTS = 4;         // Max dynamic lights supported by shader
        

        private Light[] _lights;

        private int _lightsCount;


        private void _addLightEntry(in LightType type,
            in Vector3 position, in Vector3 target,
            in Vector4 color)
        {
            Light light = new();

            if (_lightsCount < MAX_LIGHTS)
            {
                light.enabled = true;
                light.type = type;
                light.position = position;
                light.target = target;
                light.color = new Vector4(
                    color.X,
                    color.Y,
                    color.Z,
                    color.W);
                _lights[_lightsCount] = light;
                _lightsCount++;
            }
        }


        private void _collectAmbientLights(in RenderFrame renderFrame)
        {
            var listAmbientLights = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.AmbientLight>()
                .AsEnumerable();
            Vector4 colAmbient = new Vector4(0, 0, 0, 0);
            foreach (var eLight in listAmbientLights)
            {
                colAmbient += eLight.Get<engine.joyce.components.AmbientLight>().Color;
            }
            renderFrame.ColAmbient = colAmbient;
        }


        private void _collectDirectionalLights(in RenderFrame renderFrame)
        {
            /*
             * Collect all lights.
             * // TXWTODO: This assumes we are only a littly amount of directional lights.
             */
            var listDirectionalLights = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.DirectionalLight>()
                .With<engine.transform.components.Transform3ToWorld>()
                .AsEnumerable();
            foreach (var eLight in listDirectionalLights)
            {
                if (_lightsCount==MAX_LIGHTS)
                {
                    break;
                }

                var matTransform = eLight.Get<engine.transform.components.Transform3ToWorld>().Matrix;
                var vRight = new Vector3(matTransform.M11, matTransform.M12, matTransform.M13);
                var cLight = eLight.Get<engine.joyce.components.DirectionalLight>();
                _addLightEntry(LightType.LIGHT_DIRECTIONAL,
                    matTransform.Translation, vRight + matTransform.Translation, cLight.Color);
            }
        }
        

        private void _collectPointLights(in RenderFrame renderFrame)
        {
            /*
             * Collect all lights.
             * // TXWTODO: This assumes we are only a little amount of directional lights.
             */
            var listPointLights = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.PointLight>()
                .With<engine.transform.components.Transform3ToWorld>()
                .AsEnumerable();
            foreach (var eLight in listPointLights)
            {
                if (_lightsCount == MAX_LIGHTS)
                {
                    break;
                }

                var matTransform = eLight.Get<engine.transform.components.Transform3ToWorld>().Matrix;
                var vRight = new Vector3(matTransform.M11, matTransform.M12, matTransform.M13);
                var cLight = eLight.Get<engine.joyce.components.PointLight>();
                _addLightEntry(LightType.LIGHT_POINT,
                    matTransform.Translation, vRight + matTransform.Translation, cLight.Color);
            }
        }


        /**
         * Find all appropriate light entities and collect the most important.
         */
        public void CollectLights(in RenderFrame renderFrame)
        {
            for(int i=0; i<_lightsCount; i++)
            {
                _lights[i].enabled = false;
            }
            _lightsCount = 0;
            _collectDirectionalLights(renderFrame);
            _collectPointLights(renderFrame);
            _collectAmbientLights(renderFrame);
        }

        /**
         * Find all appropriate light entities and collect the most important.
         */
        public void ApplyLights(in RenderFrame renderFrame, in AShaderEntry aShaderEntry)
        {
            _threeD.ApplyAmbientLights(renderFrame.ColAmbient, aShaderEntry);
            _threeD.ApplyAllLights(_lights, aShaderEntry);
        }


        public LightManager(engine.Engine engine, in IThreeD threeD) 
        {
            _engine = engine;
            _threeD = threeD;
            _lights = new Light[MAX_LIGHTS];
        }
    }
}
