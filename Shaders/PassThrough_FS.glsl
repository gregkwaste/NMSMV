/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

 
/* Copies incoming fragment color without change. */

//Diffuse Textures
uniform sampler2D InTex;

out vec4 fragColour; 

void main()
{
	fragColour = texelFetch(InTex, ivec2(gl_FragCoord.xy), 0);
}
