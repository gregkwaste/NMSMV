#version 420
/* Copies incoming fragment color without change. */
varying vec3 color;
void main()
{	
	gl_FragColor = vec4(color, 1.0);
}