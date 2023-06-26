using System;
using static engine.Logger;

namespace Splash.shadercode
{
    public class LightingFS
    {
        public static string GetShaderCode()
        {
            string api = engine.GlobalSettings.Get("platform.threeD.API");
            if (api== "OpenGL")
            {
                return "#version 330\n\n" + _shaderCodeCommon;
            } else if (api == "OpenGLES")
            {
                return "#version 300 es\n\n" + _shaderCodeCommon;
            } 
            ErrorThrow($"Invalid graphics API setup in global config \"platform.threeD.API\": \"{api}\".",
                m => new InvalidOperationException(m));
            return "";
        }

        static private string _shaderCodeCommon = @"
// (from vertex shader)
in vec4 fragPosition;
in vec2 fragTexCoord;
in vec2 fragTexCoord2;
in vec4 fragColor;
in vec3 fragNormal;

// Input uniform values
uniform sampler2D texture0;
uniform sampler2D texture2;
uniform vec4 colDiffuse;
uniform vec4 ambient;



// Output fragment color
out vec4 finalColor;

// NOTE: Add here your custom variables

#define     MAX_LIGHTS              4
#define     LIGHT_DIRECTIONAL       0
#define     LIGHT_POINT             1

struct MaterialProperty {
    vec3 color;
    int useSampler;
    sampler2D sampler;
};

struct Light {
    int enabled;
    int type;
    vec3 position;
    vec3 target;
    vec4 color;
};

// Input lighting values
uniform Light lights[MAX_LIGHTS];

uniform vec3 viewPos;

void main()
{
    // Texel color fetching from texture sampler
    vec4 texelColor = texture(texture0, fragTexCoord);
    vec4 emissiveColor = texture(texture2, fragTexCoord);
    vec3 totalLight = vec3(0.0);
    vec3 normal = normalize(fragNormal);
    vec3 viewD = normalize(viewPos - vec3(fragPosition));
    vec3 specular = vec3(0.0);

    if ((emissiveColor.a+texelColor.a) < 0.01) discard;

    // NOTE: Implement here your fragment shader code

    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        if (lights[i].enabled != 0)
        {
            vec3 light = vec3(0.0);

            if (lights[i].type == LIGHT_DIRECTIONAL)
            {
                light = -normalize(lights[i].target - lights[i].position);
                float dotNormalLight = max(dot(normal, light), 0.0);
                totalLight += lights[i].color.rgb*dotNormalLight;
            }

            if (lights[i].type == LIGHT_POINT)
            {
                light = normalize(lights[i].position - vec3(fragPosition));
                float dotNormalLight = max(dot(normal, light), 0.0);
                totalLight += lights[i].color.rgb*dotNormalLight;
            }

        }
    }

    
    vec4 colDiffuseTotal = /*texelColor +*/ colDiffuse;
    vec4 colEmissiveTotal = emissiveColor; 
    vec4 colAmbientTotal = ambient; 
    finalColor = 
        colDiffuseTotal * vec4(totalLight,0.0)
        /*+ colEmissiveTotal*/
        //+ vec4(0.53,0.15,0.18,0.0)
        + colAmbientTotal
        ;    

    // Gamma correction
    // finalColor = pow(finalColor, vec4(1.0/2.2));
}

";

    }
}
