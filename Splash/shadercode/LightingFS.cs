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
precision highp float;

// (from vertex shader)
in vec4 fragPosition;
in vec2 fragTexCoord;
in vec2 fragTexCoord2;
in vec4 fragColor;
in vec3 fragNormal;

// Input uniform values
uniform sampler2D texture0;
uniform sampler2D texture2;
uniform vec4 col4Diffuse;
uniform vec4 col4Emissive;
uniform vec4 col4EmissiveFactors;
uniform vec4 col4Ambient;

uniform float fogDistance;

// Output fragment color
out vec4 finalColor;

// NOTE: Add here your custom variables

#define     MAX_LIGHTS              4
#define     LIGHT_DIRECTIONAL       0
#define     LIGHT_POINT             1

struct MaterialProperty
{
    vec3 color;
    int useSampler;
    sampler2D sampler;
};

struct Light
{
    int enabled;
    int type;
    vec3 position;
    vec3 target;
    vec4 color;
    float param1;
};

// Input lighting values
uniform Light lights[MAX_LIGHTS];

uniform vec3 v3AbsPosView;

void main()
{
    vec3 v3RelFragPosition = vec3(fragPosition)-v3AbsPosView;

    // Texel color fetching from texture sampler
    vec4 col4TexelDiffuse = texture(texture0, fragTexCoord);
    vec4 col4TexelEmissive = texture(texture2, fragTexCoord2);
    vec3 col3TotalLight = vec3(0.0);
    vec3 v3nNormal = normalize(fragNormal);

    // Is it all too transparent?
    if ((col4TexelEmissive.a+col4TexelDiffuse.a) < 0.01) discard;

    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        if (lights[i].enabled != 0)
        {
            vec3 v3nDirLight = vec3(0.0);

            if (lights[i].type == LIGHT_DIRECTIONAL)
            {
                v3nDirLight = -normalize(lights[i].target - lights[i].position);
                float dotNormalLight = max(dot(v3nNormal, v3nDirLight), 0.0);
                col3TotalLight += lights[i].color.rgb*dotNormalLight;
            }

            if (lights[i].type == LIGHT_POINT)
            {
                vec3 v3DirFragLight = lights[i].position - vec3(fragPosition);
                float lengthFragLight = length(v3DirFragLight);
                v3nDirLight = v3DirFragLight / lengthFragLight;

                if (lights[i].param1 > -1.0)
                {
                    // This is a directional v3nDirLight, consider the target.
                    // Minus, because we care about the angle at t he v3nDirLight.
                    float dotTarget = -dot(lights[i].target,v3nDirLight);
                    if (dotTarget > lights[i].param1)
                    {
                        float dotNormalLight = max(dot(v3nNormal, v3nDirLight), 0.0);
                        col3TotalLight += (lights[i].color.rgb*dotNormalLight) / lengthFragLight;
                    }
                } else
                {
                    float dotNormalLight = max(dot(v3nNormal, v3nDirLight), 0.0);
                    col3TotalLight += (lights[i].color.rgb*dotNormalLight) / lengthFragLight;
                }
            }

        }
    }
    
    vec4 col4DiffuseTotal = col4TexelDiffuse + col4Diffuse;
    vec4 col4EmissiveTotal = col4TexelEmissive * col4EmissiveFactors + col4Emissive; 
    vec4 col4AmbientTotal = col4Ambient;

    vec4 col4Unfogged = 
        col4DiffuseTotal * vec4(col3TotalLight,0.0)
        + col4EmissiveTotal
        //+ vec4(0.53,0.15,0.18,0.0)
        + col4AmbientTotal
        ;

    if (fogDistance > 1.0)
    {
        vec3 col3Fog = vec3(0.2,0.18,0.2); 
        vec3 col3Unfogged = vec3(col4Unfogged.xyz);
        float distance = length(v3RelFragPosition);
        float fogIntensity = clamp(distance, 0.0, fogDistance) / (fogDistance+50.0);
        vec3 col3FoggedColor = (1.0-fogIntensity) * col3Unfogged + fogIntensity * col3Fog;

        finalColor = vec4(col3FoggedColor.xyz, col4Unfogged.w);
    }
    else
    {
        finalColor = col4Unfogged;
    }

    // Gamma correction
    // finalColor = pow(finalColor, vec4(1.0/2.2));
}

";

    }
}
