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

uniform int materialFlags;

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


struct Plane {
        vec3 position;
        vec3 normal;
        vec3 color;
};


struct Ray {
        vec3 direction;
        vec3 origin;
};


vec4 checkVisibility(Ray ray, Plane plane, vec4 zBuffer) {
    float distance = dot(plane.position - ray.origin, plane.normal) / dot(plane.normal, ray.direction);
    if (distance < zBuffer.w) {
        zBuffer.w = distance;
        zBuffer.rgb = plane.color;
    }
    return zBuffer;
}


void applyLightDirectional(in Light light, inout vec3 col3TotalLight)
{
    vec3 v3nDirLight = -normalize(light.target - light.position);
    float dotNormalLight = max(dot(fragNormal, v3nDirLight), 0.0);
    col3TotalLight += light.color.rgb*dotNormalLight;
}


void applyLight(in Light light, inout vec3 col3TotalLight)
{
    if (light.enabled == 0) return;

    if (light.type == LIGHT_DIRECTIONAL)
    {
        applyLightDirectional(light, col3TotalLight);
    }

    if (light.type == LIGHT_POINT)
    {
        vec3 v3DirFragLight = light.position - vec3(fragPosition);
        float lengthFragLight = length(v3DirFragLight);
        vec3 v3nDirLight = v3DirFragLight / lengthFragLight;

        if (light.param1 > -1.0)
        {
            // This is a directional v3nDirLight, consider the target.
            // Minus, because we care about the angle at t he v3nDirLight.
            float dotTarget = -dot(light.target,v3nDirLight);
            if (dotTarget > light.param1)
            {
                float dotNormalLight = max(dot(fragNormal, v3nDirLight), 0.0);
                col3TotalLight += (light.color.rgb*dotNormalLight) / lengthFragLight;
            }
        } else
        {
            float dotNormalLight = max(dot(fragNormal, v3nDirLight), 0.0);
            col3TotalLight += (light.color.rgb*dotNormalLight) / lengthFragLight;
        }
    }
}


void main()
{
    vec3 v3RelFragPosition = vec3(fragPosition)-v3AbsPosView;

    // Texel color fetching from texture sampler
    vec4 col4TexelDiffuse = texture(texture0, fragTexCoord);
    vec4 col4TexelEmissive = texture(texture2, fragTexCoord2);
    vec3 col3TotalLight = vec3(0.0);

    // Is it all too transparent?
    if ((col4TexelEmissive.a+col4TexelDiffuse.a) < 0.01) {
        //discard;
        //finalColor = vec4(1.0,1.0,0.0,0.0);
        // return;
    } 

    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        applyLight(lights[i], col3TotalLight);    
    }
    
    vec4 col4DiffuseTotal = col4TexelDiffuse + col4Diffuse;
    vec4 col4EmissiveTotal = col4TexelEmissive * col4EmissiveFactors + col4Emissive; 
    vec4 col4AmbientTotal = col4Ambient;

    vec4 col4Unfogged =  
        vec4(col4DiffuseTotal.xyz * col3TotalLight, col4DiffuseTotal.w)
        //+ col4DiffuseTotal * vec4(col3TotalLight,0.0)
        + col4EmissiveTotal
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
