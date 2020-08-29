/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */


//Includes
#include "/common.glsl"
#include "/common_structs.glsl"

//TODO: Do some queries internally and figure out the exact locations of the uniforms
uniform CommonPerFrameSamplers mpCommonPerFrameSamplers;

//Uniform Blocks
layout (std140, binding=0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};

in vec4 vertColor;
out vec4 fragColor;

uniform float color_mult;
uniform float is_active;



void main(){
	float avg_col = 0.33 * (vertColor.r + vertColor.g + vertColor.b);
	vec4 gray = vec4(avg_col, avg_col, avg_col, 1.0);
	vec4 col = mix(vertColor, gray, 0.5);
	fragColor = mix(vertColor, col, is_active);
}