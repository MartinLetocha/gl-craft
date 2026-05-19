#version 330 core
layout (location = 0) in vec3 vPos;
layout (location = 1) in vec2 vUv;
layout (location = 2) in vec3 instancePos;

uniform mat4 uView;
uniform mat4 uProjection;

out vec2 fUv;

void main()
{
    gl_Position = uProjection * uView * vec4(vPos + instancePos, 1.0);
    fUv = vUv;
}
