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
	vec4 highlight = vec4(1.0, 0.85, 0.12, 1.0);
	fragColor = mix(vertColor, highlight, 0.8 * is_active);
}
