#include "/common.glsl"
#include "/common_structs.glsl"

layout(location = 0) in vec2 corner;
layout(location = 1) in vec2 particleUv;
layout(location = 2) in vec3 particlePosition;
layout(location = 3) in vec2 sizeRotation;
layout(location = 4) in vec4 particleColor;
layout(location = 5) in float textureLayer;

layout (std140, binding = 0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};

out vec2 uv;
out vec4 color;
flat out float layer;

void main()
{
    float c = cos(sizeRotation.y);
    float s = sin(sizeRotation.y);
    vec2 rotated = mat2(c, -s, s, c) * corner * sizeRotation.x;

    vec4 center = mpCommonPerFrame.rotMat * vec4(particlePosition, 1.0);
    vec3 cameraRight = normalize(mpCommonPerFrame.lookMatInv[0].xyz);
    vec3 cameraUp = normalize(mpCommonPerFrame.lookMatInv[1].xyz);
    vec3 billboardOffset = cameraRight * rotated.x + cameraUp * rotated.y;

    gl_Position = mpCommonPerFrame.mvp * vec4(center.xyz + billboardOffset, 1.0);
    uv = particleUv;
    color = particleColor;
    layer = textureLayer;
}
