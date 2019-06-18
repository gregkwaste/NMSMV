#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable

#include "/common_structs.glsl"


layout(location=0) in vec3 vPosition;
layout(location=2) in vec3 vcolor;

uniform float scale;

//Uniform Blocks
layout (std140) uniform Uniforms
{
    CommonPerFrameUniforms mpCommonPerFrame;
    CommonPerMeshUniforms mpCommonPerMesh;
};



out vec3 color;
out vec4 finalPos;

void main()
{
    //Set color
    color = vcolor;
	finalPos = mpCommonPerMesh.worldMat * vec4(scale * vPosition, 1.0);
    gl_Position = mpCommonPerFrame.mvp * finalPos;
}