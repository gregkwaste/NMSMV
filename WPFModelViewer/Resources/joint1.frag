#version 330
/* Copies incoming fragment color without change. */
in vec3 color;

out vec4 outcolors[3];
void main()
{	
	outcolors[0] = vec4(color, 1.0);
	outcolors[1] = vec4(1.0, 1.0, 1.0, 1.0);
	outcolors[2] = vec4(1.0, 1.0, 1.0, 1.0);
}