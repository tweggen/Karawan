
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
const int MAX_BONE_INFLUENCE = 4;

// Input uniform values
uniform mat4 mvp;
uniform mat4 m4BoneMatrices[MAX_BONES]; 
uniform int iVertexFlags;
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
    vec4 v4TotalPosition;
    vec3 v3TotalNormal;

    {
        vec4 v4Vertex = vec4(vertexPosition, 1.0);

        // if ((int(iVertexFlags) & 1) != 0)
        if (int(iVertexFlags) == 1)
        {
            v4TotalPosition = vec4(0.0);
            v3TotalNormal = vec3(0.0);
            for (int i = 0; i < MAX_BONE_INFLUENCE; i++)
            {
                if (vertexBoneIds[i] == -1)
                {
                    fragColor = vec4(1.0, 0.0, 0.0, 1.0);
                    continue;
                }
                int boneId = vertexBoneIds[i]; 
                if (boneId >= MAX_BONES)
                {
                    v4TotalPosition = v4Vertex;
                    // v4TotalPosition = vec4(100.0, 100.0, 0.0, 1.0);
                    v3TotalNormal = vertexNormal;
                    fragColor = vec4(0.0, 1.0, 0.0, 1.0);
                    break;
                }

                mat4 m4BoneMatrix = m4BoneMatrices[boneId];

                vec4 v4LocalPosition = m4BoneMatrix * v4Vertex;
                v4TotalPosition += v4LocalPosition * vertexWeights[i];
                vec3 v3LocalNormal = mat3(m4BoneMatrix) * vertexNormal;
                v3TotalNormal += v3LocalNormal * vertexWeights[i];
            }
            
        } else
        {
            v4TotalPosition = v4Vertex;
            v3TotalNormal = vertexNormal;
        }
    }
        
    // instanceTransform = mat4(1.0);
    // Compute MVP for current instance
    mat4 mvpi = mvp * instanceTransform;
    v4InstancePosition = vec4(instanceTransform[3].xyz,1.0f);

    // Send vertex attributes to fragment shader
    v4FragPosition = instanceTransform * v4TotalPosition;
    v4FragFlatPosition = instanceTransform * v4TotalPosition;
    fragTexCoord = vertexTexCoord;
    fragTexCoord2 = vertexTexCoord2;
    fragColor = vertexColor;
    v3FragNormal = normalize(vec3(instanceTransform * vec4(v3TotalNormal, 0.0)));

    v3FragUp = vec3(0.0, 1.0, 0.0);
    //v3FragFront = vec3(0.0,0.0,1.0); 
    v3FragFront = v3FragNormal;
    // v3FragRight = vec3(1.0,0.0,0.0); 
    v3FragRight = cross(v3FragFront, v3FragUp);
    // Calculate final vertex position
    gl_Position = mvpi*v4TotalPosition;
}

