
// Input vertex attributes
layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in vec2 vertexTexCoord;
layout(location = 2) in vec2 vertexTexCoord2;
layout(location = 3) in vec3 vertexNormal;
layout(location = 4) in vec4 vertexColor;
layout(location = 5) in vec4 vertexWeights;
layout(location = 6) in ivec4 vertexBoneIds;
in mat4 instanceTransform;


const int MAX_BONES = 100;

// Input uniform values
uniform mat4 mvp;
uniform mat4 m4BoneMatrices[MAX_BONES]; 
uniform uint iVertexFlags;
// uniform mat4 matNormal;

// Output vertex attributes (to fragment shader)
out vec4 v4FragPosition;
flat out vec4 v4InstancePosition;
flat out vec4 v4FragFlatPosition;
out vec2 fragTexCoord;
out vec2 fragTexCoord2;
out vec4 fragColor;
out vec3 v3FragNormal;
out vec3 v3FragUp;
out vec3 v3FragRight;
out vec3 v3FragFront;

void main()
{
    //vec4 totalPosition = vec4(0.0f);
    //for(int i = 0 ; i < MAX_BONE_INFLUENCE ; i++)
    //{
    //    if(vertexBoneIds[i] == -1) 
    //        continue;
    //    if(vertexBoneIds[i] >=MAX_BONES) 
    //    {
    //        totalPosition = vec4(pos,1.0f);
    //        break;
    //    }
    //    vec4 localPosition = m4BoneMatrices[vertexBoneIds[i]] * vec4(pos,1.0f);
    //    totalPosition += localPosition * vertexWeights[i];
    //    vec3 localNormal = mat3(m4BoneMatrices[vertexBoneIds[i]]) * vertexNormal;
    //}
        
    // instanceTransform = mat4(1.0);
    // Compute MVP for current instance
    mat4 mvpi = mvp * instanceTransform;
    v4InstancePosition = vec4(instanceTransform[3].xyz,1.0);
    vec4 vertex = vec4(vertexPosition, 1.0);

    // Send vertex attributes to fragment shader
    v4FragPosition = instanceTransform * vertex;
    v4FragFlatPosition = instanceTransform * vertex;
    fragTexCoord = vertexTexCoord;
    fragTexCoord2 = vertexTexCoord2;
    fragColor = vertexColor;
    v3FragNormal = normalize(vec3(instanceTransform * vec4(vertexNormal, 0.0)));

    v3FragUp = vec3(0.0, 1.0, 0.0);
    //v3FragFront = vec3(0.0,0.0,1.0); 
    v3FragFront = v3FragNormal;
    // v3FragRight = vec3(1.0,0.0,0.0); 
    v3FragRight = cross(v3FragFront, v3FragUp);
    // Calculate final vertex position
    gl_Position = mvpi*vertex;
}

