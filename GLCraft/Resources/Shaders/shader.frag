#version 330 core
in vec2 fUv;

uniform sampler2D uTexture0;

out vec4 FragColor;

void main()
{
    vec4 texColor = texture(uTexture0, fUv);
    if (texColor.a < 0.1)
    {
        discard;
    }

    FragColor = texColor;
}
