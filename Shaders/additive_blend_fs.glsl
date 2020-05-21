/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

//Additive Blend Shader

//Diffuse Textures
uniform sampler2D in1Tex;
uniform sampler2D in2Tex;

out vec4 fragColour; 

void main()
{
	vec3 color1 = texelFetch(in1Tex, ivec2(gl_FragCoord.xy), 0).rgb;
	vec3 color2 = texelFetch(in2Tex, ivec2(gl_FragCoord.xy), 0).rgb;
	
	color1 += color2;

	fragColour = vec4(color1, 1.0);
}
