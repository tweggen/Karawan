
// Input vertex attributes
layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in vec2 vertexTexCoord;
layout(location = 2) in vec2 vertexTexCoord2;
layout(location = 3) in vec3 vertexNormal;
layout(location = 4) in vec4 vertexColor;
in mat4 instanceTransform;

// Input uniform values
uniform mat4 mvp;
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

// NOTE: Add here your custom variables

void main()
{
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

