/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

//Includes
#include "/common.glsl"

/* Copies incoming fragment color without change. */
uniform vec3 color;
uniform float intensity;


//Inputs
in vec4 fragPos;
in vec3 N;

//Deferred Shading outputs
out vec4 outcolors[5];


void main()
{	
	vec4 diffTexColor = vec4(1.0, 1.0, 0.0, 1.0); 
	//outcolors[0] = vec4(1.0, 1.0, 1.0, 1.0);
	outcolors[0] = diffTexColor;
	//gl_FragColor = vec4(N, 1.0);
    outcolors[1] = fragPos;
    outcolors[2] = vec4(N, 0.0);
    outcolors[3] = vec4(0.0);
    outcolors[4] = vec4(0.0);
}