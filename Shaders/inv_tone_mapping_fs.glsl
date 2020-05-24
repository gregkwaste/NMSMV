/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

//Tone Mapping shader

//Includes
#include "/common.glsl"
#include "/common_structs.glsl"

//Diffuse Textures
uniform sampler2D inTex;


//Uniform Blocks
layout (std140, binding=0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};


out vec4 fragColour; 

void main()
{
	vec4 color = texelFetch(inTex, ivec2(gl_FragCoord.xy), 0);

	//Invert gamma correction
	color.rgb = GammaCorrectInput(color.rgb);

	//Invert exposure tone mapping
	color.rgb = -log(1 - color.rgb) / mpCommonPerFrame.HDRExposure;
	
	fragColour = vec4(color.rgb, color.a);
}
