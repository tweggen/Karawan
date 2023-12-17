precision highp float;

// (from vertex shader)
in vec4 v4FragPosition;
flat in vec4 v4FragFlatPosition;
flat in vec4 v4InstancePosition;
in vec2 fragTexCoord;
in vec2 fragTexCoord2;
in vec4 fragColor;
in vec3 v3FragNormal;
in vec3 v3FragUp;
in vec3 v3FragRight;
in vec3 v3FragFront;

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
    vec3 v3U;
    vec3 v3V;
};


struct Ray {
    vec3 direction;
    vec3 origin;
};


void applyLightDirectional(in vec3 v3Normal, in Light light, inout vec3 col3TotalLight)
{
    vec3 v3nDirLight = -normalize(light.target - light.position);
    float dotNormalLight = max(dot(v3Normal, v3nDirLight), 0.0);
    col3TotalLight += light.color.rgb*dotNormalLight;
}


void applyLightPoint(in vec3 v3Normal, in Light light, inout vec3 col3TotalLight)
{
    vec3 v3DirFragLight = light.position - vec3(v4FragPosition);
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


vec4 checkVisibility(in Ray ray, in Plane plane, inout vec4 zBuffer, inout Plane closest)
{
    float distance = dot(plane.position - ray.origin, plane.normal) / dot(plane.normal, ray.direction);
    if (distance < zBuffer.w) {
        zBuffer.w = distance;
        zBuffer.rgb = plane.color;
        closest = plane;
    }
    return zBuffer;
}


void computeSurfaceUV(Ray ray, in Plane plane, in vec4 zBuffer, out vec2 v2UV)
{
    vec3 v3Hit = ray.direction * zBuffer.w + ray.origin;
    vec3 v3RelPlane = v3Hit - plane.position;
    v2UV.x = mod(dot(plane.v3U, v3RelPlane), 1.0);
    v2UV.y = mod(dot(plane.v3V, v3RelPlane), 1.0);
}


/**
 * Perform interior mapping.
 * We rely on the vertex shader having computed "up", "right" and "forward"
 * vectors for the surface.
 */
void renderInterior(inout vec3 v3CurrNormal, inout vec4 col4Diffuse, inout vec4 col4Emissive)
{
    /*
     * Read the covering building texture.
     */
    vec4 col4TexDiffuse = texture(texture0, fragTexCoord);
    if (col4TexDiffuse.a > 0.0)
    {
        /*
         * We have a wall here and do not need tzo compute the interior.
         */
        col4Diffuse = col4TexDiffuse;
        col4Emissive = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    /*
     * We define this position to be everything relative to.
     */
    vec3 v3Reference = vec3(v4InstancePosition);
        
    /* 
     * optimized representation of room size to make use of component wise computation.
     */
    vec3 v3RoomSize = vec3(5.0, 3.0, 5.0);

    /*
     * This is the coordinate we say to be the origin of the building.
     * We use this to correctly offtesset the rooms.
     * We keep it in the range of 0...v3RoomSize comnponent wise.
     */
    vec3 v3BuildingBase = vec3(0.0,v4FragFlatPosition.y,0.0)-v3Reference;
        
    /*
     * Compute the ray from the camera to the current point in the current triangle.
     * This, as said, is relative to v3Reference (i.e. the origin of the ray).
     *
     * rayFromCamera.origin is a bit behind the wall at the current frag from
     * the camera's perspective.
     */
    Ray rayFromCamera;
    rayFromCamera.direction = normalize(v3RelFragPosition);
    rayFromCamera.origin = vec3(v4FragPosition)-v3Reference;
    rayFromCamera.origin += rayFromCamera.direction * 0.001;
        
    vec3 v3RelFrag = rayFromCamera.origin - v3BuildingBase;    

    /*
     * Now, given the base vectors of the rooms' walls, v3FragRight/Up/Front, the 
     * building base coordinate v3BuildingBase and the rooms' dimenstions (v3RoomSize), 
     * compute the index of the room relative to the camera origin.
     */
    float fix = ceil(dot(v3FragRight,v3RelFrag) / v3RoomSize.x);
    float fiy = ceil(dot(v3FragUp,   v3RelFrag) / v3RoomSize.y);
    float fiz = ceil(dot(v3FragFront,v3RelFrag) / v3RoomSize.z);

    /*
     * Based on some relatively arbitrary parameters, compute, if the lights are on
     * in this window.
     */
    uint isOn;
    {
        int ix = int(fix);
        int iy = int(fiy);
        int iz = int(fiz);
        uint houseSeed = uint(dot(v3One, rayFromCamera.origin));
        uint windowSeed = uint(ix*ix+iy*iy*iy+ix*iy*iz);
        uint timeSeed = ((uint(frameNo)+98765u)/(2000u+uint(ix+iy+((ix*iy*54321)&15))))*511232941u;
        isOn = timeSeed & windowSeed /* & houseSeed */;
    }

    /*
     * These variables keep the result of the computations.
     */
    vec4 zBuffer = vec4(1.0, 1.0, 1.0, 1000000.0);
    Plane surfaceClosest;
    surfaceClosest.normal.x = 12345;
        
    float testUp = dot(v3FragUp, rayFromCamera.direction);     
    if (testUp > 0.001) {
        Plane ceiling;
        ceiling.position = v3BuildingBase + (fiy * v3RoomSize.y) * v3FragUp;
        ceiling.normal = v3FragUp;
        ceiling.v3U = v3FragRight / v3RoomSize.x;
        ceiling.v3V = v3FragFront / v3RoomSize.z;
        ceiling.color = vec3(1.0,0.0,0.0); //vec3(0.1, 0.1, 0.1);
        
        zBuffer = checkVisibility(rayFromCamera, ceiling, zBuffer, surfaceClosest);
    } else if (testUp < -0.001) {
        Plane floor;
        floor.position = v3BuildingBase + ((fiy - 1.0) * v3RoomSize.y) * v3FragUp;
        floor.normal = -v3FragUp;
        floor.v3U = v3FragRight / v3RoomSize.x;
        floor.v3V = v3FragFront / v3RoomSize.z;
        floor.color = vec3(0.0,1.0,0.0); // vec3(0.2, 0.2, 0.2);
        
        zBuffer = checkVisibility(rayFromCamera, floor, zBuffer, surfaceClosest);
    }
        
    float testRight =dot (v3FragRight, rayFromCamera.direction);     
    if (testRight > 0.001) {
        Plane wall;
        wall.position = v3BuildingBase + (fix * v3RoomSize.x) * v3FragRight;
        wall.normal = v3FragRight;
        wall.color = vec3(1.0,1.0,0.0); //vec3(0.26, 0.2, 0.2);
        wall.v3U = v3FragFront / v3RoomSize.z;
        wall.v3V = v3FragUp / v3RoomSize.y;
        
        zBuffer = checkVisibility(rayFromCamera, wall, zBuffer, surfaceClosest);
    } else if (testRight < -0.001) {
        Plane wall;
        wall.position = v3BuildingBase + ((fix - 1.0) * v3RoomSize.x) * v3FragRight;
        wall.normal = -v3FragRight;
        wall.color = vec3(0.0,0.0,1.0); // vec3(0.3, 0.2, 0.3);
        wall.v3U = v3FragFront / v3RoomSize.z;
        wall.v3V = v3FragUp / v3RoomSize.y;
        
        zBuffer = checkVisibility(rayFromCamera, wall, zBuffer, surfaceClosest);
    }

    float testFront = dot(v3FragFront, rayFromCamera.direction);     
    if (testFront > 0.001) {
        Plane wall;
        wall.position = v3BuildingBase + (fiz * v3RoomSize.z) * v3FragFront;
        wall.normal = v3FragFront;
        wall.color = vec3(1.0,0.0,1.0); //vec3(0.15, 0.15, 0.15);
        wall.v3U = v3FragRight / v3RoomSize.x;
        wall.v3V = v3FragUp / v3RoomSize.y;
        
        zBuffer = checkVisibility(rayFromCamera, wall, zBuffer, surfaceClosest);
    } else if (testFront < -0.001) {
        Plane wall;
        wall.position = v3BuildingBase + ((fiz - 1.0) * v3RoomSize.z) * v3FragFront;
        wall.normal = -v3FragFront;
        wall.color = vec3(0.0,1.0,1.0); //vec3(0.1, 0.1, 0.1);
        wall.v3U = v3FragRight / v3RoomSize.x;
        wall.v3V = v3FragUp / v3RoomSize.y;
        
        zBuffer = checkVisibility(rayFromCamera, wall, zBuffer, surfaceClosest);
    }

    /*
     * Only continue with the computations if we really hit any of the walls.
     */

    /*
     * Only render something if any of the buffers hit.
     * Generate a striking purple color for debugging.
     * In debugging, we would have troaysdc<X:
     */
    if (surfaceClosest.normal.x >= 10.0)
    {
        col4Diffuse = vec4(1.0,0.0,1.0,1.0);
        col4Emissive = vec4(1.0,0.0,1.0,1.0);
        return;    
    }
        
    /*
     * We know we had something to render.
     */
    vec2 v2UV;
    computeSurfaceUV(rayFromCamera, surfaceClosest, zBuffer, v2UV);
        
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
    } else
    {
        light = 0.2;
    }
    col4Diffuse = vec4(zBuffer.rgb * light, 1.0);
     
    /*
     * As a test, directly use the uv as a color.
     */
    {
        col4Diffuse.x = v2UV.x;
        col4Diffuse.y = v2UV.y;
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
    v3RelFragPosition = vec3(v4FragPosition)-v3AbsPosView;
    v3One = vec3(1.0, 1.0, 1.0);

    /*
     * Collect the pixel diffuse/emissive color and normal 
     * to render.
     */
    vec4 col4TexelDiffuse;
    vec4 col4TexelEmissive;
    vec3 v3Normal = v3FragNormal;    
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
