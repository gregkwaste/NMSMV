#version 330
#extension GL_ARB_explicit_uniform_location : enable

#include "/common_structs.glsl"

layout(location=0) in vec4 vPosition;
layout(location=1) in vec3 vcolor;

//Uniform Blocks
layout (std140) uniform Uniforms
{
    CommonPerFrameUniforms mpCommonPerFrame;
    CommonPerMeshUniforms mpCommonPerMesh;
};

out vec3 color;

void main()
{
    //Set color
    color = vcolor;
	gl_Position = mpCommonPerFrame.mvp * vPosition;
}