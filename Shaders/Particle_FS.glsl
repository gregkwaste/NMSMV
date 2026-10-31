in vec2 uv;
in vec4 color;
flat in float layer;

uniform sampler2DArray particleMap;
out vec4 fragColor;

void main()
{
    vec4 texel = texture(particleMap, vec3(uv, layer));
    fragColor = texel * color;
    if (fragColor.a < 0.004)
        discard;
}
