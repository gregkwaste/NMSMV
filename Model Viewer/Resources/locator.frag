/* Copies incoming fragment color without change. */
in vec3 color;
void main()
{	
	gl_FragColor = vec4(color, 1.0);
}