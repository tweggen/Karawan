
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
out vec4 fragPosition;
flat out vec4 fragFlatPosition;
out vec2 fragTexCoord;
out vec2 fragTexCoord2;
out vec4 fragColor;
out vec3 fragNormal;
out vec3 fragUp;
out vec3 fragRight;
out vec3 fragFront;

// NOTE: Add here your custom variables

void main()
{
    // instanceTransform = mat4(1.0);
    // Compute MVP for current instance
    mat4 mvpi = mvp * instanceTransform;
    vec4 vertex = vec4(vertexPosition, 1.0);

    // Send vertex attributes to fragment shader
    fragPosition = instanceTransform * vertex;
    fragFlatPosition = instanceTransform * vertex;
    fragTexCoord = vertexTexCoord;
    fragTexCoord2 = vertexTexCoord2;
    fragColor = vertexColor;
    fragNormal = normalize(vec3(instanceTransform * vec4(vertexNormal, 0.0)));

    fragUp = vec3(0.0, 1.0, 0.0);
    fragRight = vec3(1.0,0.0,0.0); //cross(fragNormal, fragUp);
    fragFront = vec3(0.0,0.0,1.0); //fragNormal;
    // Calculate final vertex position
    gl_Position = mvpi*vertex;
}

