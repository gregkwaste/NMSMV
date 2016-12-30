#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable

layout(location=0) in vec3 vPosition;
layout(location=1) in vec3 vcolor;
uniform mat4 mvp, worldMat;
out vec3 color;

void main()
{
    //Set color
    color = vcolor;
    gl_Position = mvp * worldMat * vec4(vPosition, 1.0);
}