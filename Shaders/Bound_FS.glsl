
#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_texture_query_lod : enable

//Includes
#include "/common.glsl"

/* Copies incoming fragment color without change. */
uniform vec3 color;
uniform float intensity;

//Deferred Shading outputs
out vec4 outcolors[3];


void main()
{	
	vec4 diffTexColor = vec4(color, 1.0); 
	//outcolors[0] = vec4(1.0, 1.0, 1.0, 1.0);
	outcolors[0] = diffTexColor;
    //gl_FragColor = vec4(N, 1.0);
    outcolors[1] = vec4(0.0, 0.0, 0.0, 0.0);
    //outcolors[1] = vec4(N, 1.0);
    outcolors[2] = vec4(0.0, 0.0, 0.0, 0.0);
}