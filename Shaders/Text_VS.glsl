layout(location = 0) in vec2 positionPx;
layout(location = 1) in vec2 atlasUv;

uniform vec2 viewportSize;
out vec2 uv;

void main()
{
    uv = atlasUv;
    vec2 clip = vec2(positionPx.x * 2.0 / viewportSize.x - 1.0,
                     1.0 - positionPx.y * 2.0 / viewportSize.y);
    gl_Position = vec4(clip, 0.0, 1.0);
}
