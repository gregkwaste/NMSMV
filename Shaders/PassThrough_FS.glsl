#version 440
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
/* Copies incoming fragment color without change. */

//Diffuse Textures
uniform sampler2DMS InTex;

out vec4 fragColour; 

void main()
{
	fragColour = vec4(texelFetch(InTex, ivec2(gl_FragCoord.xy), 0).rgb, 1.0);
}
