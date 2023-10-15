using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using engine;
using static engine.Logger;

namespace Splash
{
    public class LightCollector
    {
        private object _lo = new();

        private engine.Engine _engine;
        private IThreeD _threeD;

        public const int MAX_LIGHTS = 4;         // Max dynamic lights supported by shader
        

        private Light[] _lights;

        private int _lightsCount;

        public Light[] Lights {
            get => _lights; 
        }
        public Vector4 ColAmbient;
        

        private void _addLightEntry(in LightType type,
            in Vector3 position, in Vector3 target,
            in Vector4 color, float param1)
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
                light.param1 = param1;
                _lights[_lightsCount] = light;
                _lightsCount++;
            }
        }


        private void _collectAmbientLights()
        {
            var listAmbientLights = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.AmbientLight>()
                .AsEnumerable();
            Vector4 colAmbient = new Vector4(0, 0, 0, 0);
            foreach (var eLight in listAmbientLights)
            {
                colAmbient += eLight.Get<engine.joyce.components.AmbientLight>().Color;
            }
            ColAmbient = colAmbient;
        }


        private void _collectDirectionalLights()
        {
            /*
             * Collect all lights.
             * // TXWTODO: This assumes we are only a littly amount of directional lights.
             */
            var listDirectionalLights = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.DirectionalLight>()
                .With<engine.joyce.components.Transform3ToWorld>()
                .AsEnumerable();
            foreach (var eLight in listDirectionalLights)
            {
                if (_lightsCount==MAX_LIGHTS)
                {
                    Warning("Out of lights.");
                    break;
                }

                var matTransform = eLight.Get<engine.joyce.components.Transform3ToWorld>().Matrix;
                var vRight = new Vector3(matTransform.M11, matTransform.M12, matTransform.M13);
                var cLight = eLight.Get<engine.joyce.components.DirectionalLight>();
                _addLightEntry(LightType.LIGHT_DIRECTIONAL,
                    matTransform.Translation, vRight + matTransform.Translation, cLight.Color, 0f);
            }
        }
        

        private void _collectPointLights()
        {
            /*
             * Collect all lights.
             * // TXWTODO: This assumes we are only a little amount of directional lights.
             */
            var listPointLights = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.PointLight>()
                .With<engine.joyce.components.Transform3ToWorld>()
                .AsEnumerable();
            foreach (var eLight in listPointLights)
            {
                if (_lightsCount == MAX_LIGHTS)
                {
                    Warning("Out of lights.");
                    break;
                }

                var cTransform = eLight.Get<engine.joyce.components.Transform3ToWorld>();
                var cLight = eLight.Get<engine.joyce.components.PointLight>();
                var position = cTransform.Matrix.Translation;
                var target = Vector3.Transform(cLight.Target, cTransform.Matrix) - position;
                _addLightEntry(LightType.LIGHT_POINT,
                    cTransform.Matrix.Translation,
                     target,
                    cLight.Color * cLight.Distance,
                    cLight.CosOpening);
            }
        }


        /**
         * Find all appropriate light entities and collect the most important.
         */
        public void CollectLights()
        {
            lock (_lo)
            {
                for (int i = 0; i < _lightsCount; i++)
                {
                    _lights[i].enabled = false;
                }

                _lightsCount = 0;
                _collectDirectionalLights();
                _collectPointLights();
                _collectAmbientLights();
            }
        }

        public LightCollector() 
        {
            _engine = I.Get<Engine>();
            _threeD = I.Get<IThreeD>();
            _lights = new Light[MAX_LIGHTS];
        }
    }
}
