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

struct MaterialProperty
{
    vec3 color;
    int useSampler;
    sampler2D sampler;
};

uniform vec3 v3AbsPosView;

void main()
{
    /*
     * Collect the pixel diffuse/emissive color and normal 
     * to render.
     */
    vec4 col4TexelEmissive;
    vec3 v3Normal = v3FragNormal;
    vec4 v4PrevPoint = texture(texture2, vec2(fragTexCoord2.x-1.0/256.0, fragTexCoord2.y));
    vec4 v4CurrPoint = texture(texture2, fragTexCoord2);
    col4TexelEmissive = vec4((v4CurrPoint.r+v4PrevPoint.r)/2.0, v4PrevPoint.g, v4CurrPoint.b, v4CurrPoint.a);
            
    
    vec4 col4EmissiveTotal = col4TexelEmissive * col4EmissiveFactors + col4Emissive;
    
    finalColor = col4EmissiveTotal;
    
    // Gamma correction
    // finalColor = pow(finalColor, vec4(1.0/2.2));
}
