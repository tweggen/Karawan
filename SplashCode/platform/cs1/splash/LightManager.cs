using engine.joyce.components;
using Raylib_CsLo;
using System;
using System.Numerics;

namespace Karawan.platform.cs1.splash
{
    public class LightManager
    {
        private object _lo = new();

        private engine.Engine _engine;


        public const int MAX_LIGHTS = 4;         // Max dynamic lights supported by shader

        /**
         * Light structure, as expected inside the shader.
         */
        public struct Light
        {

            public LightType type;
            public Vector3 position;
            public Vector3 target;
            public Color color;
            public bool enabled;

            // Shader locations
            public int enabledLoc;
            public int typeLoc;
            public int posLoc;
            public int targetLoc;
            public int colorLoc;
        }

        // Light type
        public enum LightType
        {
            LIGHT_DIRECTIONAL = 0,
            LIGHT_POINT
        }

        private Light[] _lights;

        private int _lightsCount;


        // Create a light and get shader locations
        private Light _addLight(
            in LightType type, 
            in Vector3 position, in Vector3 target, 
            in Vector4 color, ref Shader shader)
        {
            Light light = new();

            if (_lightsCount < MAX_LIGHTS)
            {
                light.enabled = true;
                light.type = type;
                light.position = position;
                light.target = target;
                light.color = new Color( 
                    (byte)(255f*color.X),
                    (byte)(255f*color.Y),
                    (byte)(255f*color.Z),
                    (byte)(255f*color.W)
                );

                // TODO: Below code doesn't look good to me, 
                // it assumes a specific shader naming and structure
                // Probably this implementation could be improved
                string enabledName = $"lights[{_lightsCount}].enabled";
                string typeName = $"lights[{_lightsCount}].type";
                string posName = $"lights[{_lightsCount}].position";
                string targetName = $"lights[{_lightsCount}].target";
                string colorName = $"lights[{_lightsCount}].color";

                // Set location name [x] depending on lights count
                //enabledName[7] = '0' + lightsCount;
                //typeName[7] = '0' + lightsCount;
                //posName[7] = '0' + lightsCount;
                //targetName[7] = '0' + lightsCount;
                //colorName[7] = '0' + lightsCount;

                light.enabledLoc = Raylib.GetShaderLocation(shader, enabledName);
                light.typeLoc = Raylib.GetShaderLocation(shader, typeName);
                light.posLoc = Raylib.GetShaderLocation(shader, posName);
                light.targetLoc = Raylib.GetShaderLocation(shader, targetName);
                light.colorLoc = Raylib.GetShaderLocation(shader, colorName);

                _lights[_lightsCount] = light;

                _lightsCount++;
                // updateAllLightsValues(ref shader);
            }

            return light;
        }
    
        /**
         * Update lights value in shader
         */
        private unsafe void _updateLightValues(ref Shader shader, in Light light)
        {
            fixed (Light* pLight = &light)
            {
                // Send to shader light enabled state and type
                Raylib.SetShaderValue(shader, light.enabledLoc, &pLight->enabled, ShaderUniformDataType.SHADER_UNIFORM_INT);
                Raylib.SetShaderValue(shader, light.typeLoc, &pLight->type, ShaderUniformDataType.SHADER_UNIFORM_INT);

                // Send to shader light position values
                Vector3 position = new(light.position.X, light.position.Y, light.position.Z);
                Raylib.SetShaderValue(shader, light.posLoc, position, ShaderUniformDataType.SHADER_UNIFORM_VEC3);

                // Send to shader light target position values
                Vector3 target = new(light.target.X, light.target.Y, light.target.Z);
                Raylib.SetShaderValue(shader, light.targetLoc, target, ShaderUniformDataType.SHADER_UNIFORM_VEC3);

                // Send to shader light color values
                Vector4 color = new((float)light.color.r / (float)255, (float)light.color.g / (float)255,
                                   (float)light.color.b / (float)255, (float)light.color.a / (float)255);
                Raylib.SetShaderValue(shader, light.colorLoc, color, ShaderUniformDataType.SHADER_UNIFORM_VEC4);
            }
        }


        private void _updateAllLights(ref Shader shader)
        {
            foreach (var light in _lights)
            {
                _updateLightValues(ref shader, light);
            }
        }


        private void _collectAmbientLights(in RlShaderEntry rlShaderEntry)
        {
            var listAmbientLights = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.AmbientLight>()
                .AsEnumerable();
            Vector4 colAmbient = new Vector4(0, 0, 0, 0);
            foreach(var eLight in listAmbientLights)
            {
                colAmbient += eLight.Get<engine.joyce.components.AmbientLight>().Color;
            }

            int ambientLoc = Raylib.GetShaderLocation(rlShaderEntry.RlShader, "ambient");
            Raylib.SetShaderValue(
                rlShaderEntry.RlShader,
                ambientLoc, colAmbient,
                ShaderUniformDataType.SHADER_UNIFORM_VEC4);
        }


        private void _collectDirectionalLights(in RlShaderEntry rlShaderEntry)
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
                var matTransform = eLight.Get<engine.transform.components.Transform3ToWorld>().Matrix;
                var vRight = new Vector3(matTransform.M11, matTransform.M12, matTransform.M13);
                var cLight = eLight.Get<engine.joyce.components.DirectionalLight>();
                _addLight(LightType.LIGHT_DIRECTIONAL,
                    matTransform.Translation, vRight + matTransform.Translation, cLight.Color, ref rlShaderEntry.RlShader);
            }
        }

        private void _collectPointLights(in RlShaderEntry rlShaderEntry)
        {
            /*
             * Collect all lights.
             * // TXWTODO: This assumes we are only a littly amount of directional lights.
             */
            var listPointLights = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.PointLight>()
                .With<engine.transform.components.Transform3ToWorld>()
                .AsEnumerable();
            foreach (var eLight in listPointLights)
            {
                var matTransform = eLight.Get<engine.transform.components.Transform3ToWorld>().Matrix;
                var vRight = new Vector3(matTransform.M11, matTransform.M12, matTransform.M13);
                var cLight = eLight.Get<engine.joyce.components.PointLight>();
                _addLight(LightType.LIGHT_POINT,
                    matTransform.Translation, vRight+matTransform.Translation, cLight.Color, ref rlShaderEntry.RlShader);
            }
        }


        /**
         * Find all appropriate light entities and collect the most important.
         */
        public void CollectLights(in RlShaderEntry rlShaderEntry)
        {
            for(int i=0; i<_lightsCount; i++)
            {
                _lights[i].enabled = false;
            }
            _lightsCount = 0;
            _collectDirectionalLights(rlShaderEntry);
            _collectPointLights(rlShaderEntry);
            _collectAmbientLights(rlShaderEntry);
            _updateAllLights(ref rlShaderEntry.RlShader);
        }

        public LightManager(engine.Engine engine) 
        {
            _engine = engine;
            _lights = new Light[4];
        }
    }
}
