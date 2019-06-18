#version 330

#include "/common_structs.glsl"

layout(location=0) in vec4 vPosition;

//Uniform Blocks
layout (std140) uniform Uniforms
{
    CommonPerFrameUniforms mpCommonPerFrame;
    CommonPerMeshUniforms mpCommonPerMesh;
};

void main()
{
	gl_Position = mpCommonPerFrame.mvp * vPosition;
}