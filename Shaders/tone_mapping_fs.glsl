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

	//Exposure tone mapping
	color.rgb = vec3(1.0) - exp(-color.rgb * mpCommonPerFrame.HDRExposure);
    
    //NMS Kodak Tone Mapping
    //color.rgb = TonemapKodak(color.rgb) / TonemapKodak( vec3(1.0,1.0,1.0) );
    
    //Gamma correction
    color.rgb = GammaCorrectOutput(color.rgb);

    fragColour = vec4(color.rgb, color.a);
}
