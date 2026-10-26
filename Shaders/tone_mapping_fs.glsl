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
	if (any(isnan(color.rgb)))
		color.rgb = vec3(0.0);
	//Infinity would give inf/inf = NaN in the curve below, treat it as the brightest value instead
	color.rgb = min(color.rgb, vec3(65000.0));

	//Exposure: cameraPosition.w is 2^EV (set from the HDR Exposure slider). The 0.6 pre-scale is part
	//of the ACES fit below, it puts 1.0 linear input at about 0.6 on the curve
	color.rgb = max(color.rgb, vec3(0.0)) * (mpCommonPerFrame.cameraPosition.w * 0.6);

	//Filmic tone mapping (ACES fitted, Narkowicz 2015): soft shoulder for highlights instead of
	//the per channel exponential curve that washed bright colors out to white
	color.rgb = clamp((color.rgb * (2.51 * color.rgb + 0.03)) / (color.rgb * (2.43 * color.rgb + 0.59) + 0.14), 0.0, 1.0);
    
    //NMS Kodak Tone Mapping
    //color.rgb = TonemapKodak(color.rgb) / TonemapKodak( vec3(1.0,1.0,1.0) );
    
    //Gamma correction
    color.rgb = GammaCorrectOutput(color.rgb);
	
    fragColour = vec4(color.rgb, color.a);
}
