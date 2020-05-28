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
uniform float color_mult;

void main(){

	gl_FragColor = color_mult * vertColor;
}