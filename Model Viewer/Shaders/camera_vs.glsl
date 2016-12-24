#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable

/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */
layout(location=0) in vec4 vPosition;
layout(location=1) in vec4 nPosition; //normals
uniform mat4 self_mvp;
uniform mat4 mvp;

//Outputs
void main()
{
    gl_Position = mvp * inverse(self_mvp) * vPosition;
}