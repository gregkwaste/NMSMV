/* Copies incoming fragment color without change. */
out vec4 gl_FragColor;
in vec3 color;
void main()
{	
	gl_FragColor = vec4(color, 1.0);
}