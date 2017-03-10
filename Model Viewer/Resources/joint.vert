#version 330
#extension GL_ARB_explicit_uniform_location : enable
/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */

layout(location=0) in vec4 vPosition;
layout(location=1) in vec3 vcolor;

layout(location=7) uniform mat4 mvp;
layout(location=10) uniform mat4 worldMat;

out vec3 color;

void main()
{
    //Set color
    color = vcolor;
	gl_Position = mvp * vPosition;
}