#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
#extension GL_NV_gpu_shader5 : enable

/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */

//Imports
#include "/common.glsl"

layout(location=0) in vec4 vPosition;
layout(location=1) in vec2 uvPosition0;
layout(location=2) in vec4 nPosition; //normals
layout(location=3) in vec4 tPosition; //tangents
layout(location=4) in vec4 bPosition; //bitangents

layout(location=7) uniform mat4 mvp;
layout(location=8) uniform mat4 nMat;
layout(location=9) uniform mat4 rotMat;
layout(location=10) uniform mat4 worldMat;

void main()
{
	vec4 wPos = worldMat * vPosition;
    gl_Position = mvp * wPos;
}