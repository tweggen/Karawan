precision highp float;

// (from vertex shader)
in vec4 fragPosition;
flat in vec4 fragFlatPosition;
in vec2 fragTexCoord;
in vec2 fragTexCoord2;
in vec4 fragColor;
in vec3 fragNormal;
in vec3 fragUp;
in vec3 fragRight;
in vec3 fragFront;

// Input uniform values
uniform sampler2D texture0;
uniform sampler2D texture2;
uniform vec4 col4Diffuse;
uniform vec4 col4Emissive;
uniform vec4 col4EmissiveFactors;
uniform vec4 col4Ambient;

uniform float fogDistance;

uniform int materialFlags;
uniform int frameNo;

// Output fragment color
out vec4 finalColor;

// NOTE: Add here your custom variables

#define MATERIAL_FLAGS_RENDER_INTERIOR 0x00000001

#define MAX_LIGHTS              4
#define LIGHT_DIRECTIONAL       0
#define LIGHT_POINT             1

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


void applyLightDirectional(in vec3 v3Normal, in Light light, inout vec3 col3TotalLight)
{
    vec3 v3nDirLight = -normalize(light.target - light.position);
    float dotNormalLight = max(dot(v3Normal, v3nDirLight), 0.0);
    col3TotalLight += light.color.rgb*dotNormalLight;
}


void applyLightPoint(in vec3 v3Normal, in Light light, inout vec3 col3TotalLight)
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
            float dotNormalLight = max(dot(v3Normal, v3nDirLight), 0.0);
            col3TotalLight += (light.color.rgb*dotNormalLight) / lengthFragLight;
        }
    } else
    {
        float dotNormalLight = max(dot(v3Normal, v3nDirLight), 0.0);
        col3TotalLight += (light.color.rgb*dotNormalLight) / lengthFragLight;
    }
}


void applyLight(in vec3 v3Normal, in Light light, inout vec3 col3TotalLight)
{
    if (light.enabled == 0) return;

    if (light.type == LIGHT_DIRECTIONAL)
    {
        applyLightDirectional(v3Normal, light, col3TotalLight);
    }

    if (light.type == LIGHT_POINT)
    {
        applyLightPoint(v3Normal, light, col3TotalLight);    
    }
}
        

vec3 v3RelFragPosition;
vec3 v3One;
        
        
/**
 * Perform interior mapping.
 * We rely on the vertex shader having computed "up", "right" and "forward"
 * vectors for the surface.
 */
void renderInterior(inout vec3 v3CurrNormal, inout vec4 col4Diffuse, inout vec4 col4Emissive)
{
    float room_height = 3.0;
    float room_width = 5.0;
    float room_depth = 5.0;
            
    float base_height = fragFlatPosition.y - floor(fragFlatPosition.y/room_height)*room_height;
        
    Ray surfaceToCamera;
    surfaceToCamera.direction = normalize(v3RelFragPosition);
    surfaceToCamera.origin = vec3(fragPosition);
    surfaceToCamera.origin += surfaceToCamera.direction * 0.001;

    vec4 zBuffer = vec4(1.0, 1.0, 1.0, 1000000.0);
        
    int ix = int(ceil(dot(fragRight,surfaceToCamera.origin) / room_width));
    int iy = int(ceil((dot(fragUp,surfaceToCamera.origin)-base_height) / room_height)); // consider base_height
    int iz = int(ceil(dot(fragFront,surfaceToCamera.origin) / room_depth));
    
    uint houseSeed = uint(dot(v3One, fragFlatPosition.xyz));
    uint windowSeed = uint(ix*ix+iy*iy*iy+ix*iy*iz);
    uint timeSeed = ((uint(frameNo)+98765u)/(2000u+uint(ix+iy+((ix*iy*54321)&15))))*511232941u;
    uint isOn = timeSeed & windowSeed & houseSeed;
    float fix = float(ix);
    float fiy = float(iy);
    float fiz = float(iz);
    if (dot(fragUp, surfaceToCamera.direction) > 0.0) {
        Plane ceiling;
        ceiling.position = (fiy * room_height + base_height) * fragUp;
        ceiling.normal = fragUp;
        ceiling.color = vec3(1.0,0.0,0.0); //vec3(0.1, 0.1, 0.1);
        
        zBuffer = checkVisibility(surfaceToCamera, ceiling, zBuffer);
    } else {
        Plane floor;
        floor.position = ((fiy - 1.0) * room_height + base_height) * fragUp;
        floor.normal = -fragUp;
        floor.color = vec3(0.0,1.0,0.0); // vec3(0.2, 0.2, 0.2);
        
        zBuffer = checkVisibility(surfaceToCamera, floor, zBuffer);
    }
    if (dot(fragRight, surfaceToCamera.direction) > 0.0) {
        Plane wall;
        wall.position = (fix * room_width) * fragRight;
        wall.normal = fragRight;
        wall.color = vec3(1.0,1.0,0.0); //vec3(0.26, 0.2, 0.2);
        
        zBuffer = checkVisibility(surfaceToCamera, wall, zBuffer);
    } else {
        Plane wall;
        wall.position = ((fix - 1.0) * room_width) * fragRight;
        wall.normal = -fragRight;
        wall.color = vec3(0.0,0.0,1.0); // vec3(0.3, 0.2, 0.3);
        
        zBuffer = checkVisibility(surfaceToCamera, wall, zBuffer);
    }

    if (dot(fragFront, surfaceToCamera.direction) > 0.0) {
        Plane wall;
        wall.position = (fiz * room_depth) * fragFront;
        wall.normal = fragFront;
        wall.color = vec3(1.0,0.0,1.0); //vec3(0.15, 0.15, 0.15);
        
        zBuffer = checkVisibility(surfaceToCamera, wall, zBuffer);
    } else {
        Plane wall;
        wall.position = ((fiz - 1.0) * room_depth) * fragFront;
        wall.normal = -fragFront;
        wall.color = vec3(0.0,1.0,1.0); //vec3(0.1, 0.1, 0.1);
        
        zBuffer = checkVisibility(surfaceToCamera, wall, zBuffer);
    }
    vec4 col4TexDiffuse = texture(texture0, fragTexCoord);
    if (col4TexDiffuse.a == 0.0)
    {
        float light;
        if((isOn & 0x88888888u) != 0u)
        {
            light = 0.3;
            col4Emissive = vec4(0.1, 0.1, 0.15, 1.0);
        } else if ((isOn & 0x44444444u) != 0u)
        {
            light = 0.3;
            col4Emissive = vec4(0.2, 0.2, 0.175, 1.0);
        } else if ((isOn & 0x22222222u) != 0u)
        {
            light = 0.5;
            col4Emissive = vec4(0.15, 0.15, 0.15, 1.0);
        } else if ((isOn & 0x11111111u) != 0u)
        {
            light = 0.5;
            col4Emissive = vec4(0.15, 0.15, 0.1, 1.0);
        }
        col4Diffuse = vec4(zBuffer.rgb * light, 1.0);
    }
    else
    {
        col4Diffuse = col4TexDiffuse;
        col4Emissive = vec4(0.0, 0.0, 0.0, 1.0);
    }   
}


/**
 * Render a completely basic texture.
 */
void renderStandard(inout vec3 v3CurrNormal, inout vec4 col4Diffuse, inout vec4 col4Emissive)
{
    col4Diffuse = texture(texture0, fragTexCoord);
    col4Emissive = texture(texture2, fragTexCoord2);
}


void main()
{
    /*
     * Update the relative position of the fragment pixel to the camera.
     * We need that for lighting and some FX shaders.
     */
    v3RelFragPosition = vec3(fragPosition)-v3AbsPosView;
    v3One = vec3(1.0, 1.0, 1.0);

    /*
     * Collect the pixel diffuse/emissive color and normal 
     * to render.
     */
    vec4 col4TexelDiffuse;
    vec4 col4TexelEmissive;
    vec3 v3Normal = fragNormal;    
    if (0 != (materialFlags & MATERIAL_FLAGS_RENDER_INTERIOR))
    {
        renderInterior(v3Normal, col4TexelDiffuse, col4TexelEmissive);    
    } else
    {
        renderStandard(v3Normal, col4TexelDiffuse, col4TexelEmissive);
    }

    vec3 col3TotalLight = vec3(0.0);
    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        applyLight(v3Normal, lights[i], col3TotalLight);    
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
