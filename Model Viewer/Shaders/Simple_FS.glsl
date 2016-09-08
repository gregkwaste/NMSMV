/* Copies incoming fragment color without change. */
out vec4 gl_FragColor;
uniform vec3 color;
in vec3 E,N;
void main()
{	
	float kd = max(dot(E, N), 0.0);
    vec3 diff = vec3(1.0, 0.0, 1.0);
    float intense = 0.5;
    
    gl_FragColor = intense*vec4((kd+0.5)* color, 1.0);	
    //gl_FragColor = intense*vec4(diff, 1.0);	
    
}