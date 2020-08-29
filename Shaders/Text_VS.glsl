/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

 
//Imports
#include "/common.glsl"
#include "/common_structs.glsl"

//Mesh Attributes
layout(location=0) in vec4 vPosition;
layout(location=1) in vec2 uvPosition0;

//Uniform Blocks
layout (std140, binding=0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};


//uniforms
uniform float textSize; //Desired textSize
uniform float fontSize; //Font Height in Pixels
uniform vec2 textDim; //Test Dimensions in pixels
uniform vec2 offset;

//Outputs
out vec2 uv;


void main()
{
    //Pass uv to fragment shader
    uv = uvPosition0;
    
    //Scale
    //float scale_factor1 =  lineHeight / textDim.y;
    float scale_factor1 =  textSize / fontSize;

    //Project to -1 , 1
    vec2 scale_factor2 = vec2(scale_factor1 * 2.0 / mpCommonPerFrame.frameDim.x, 
                        scale_factor1 * 2.0 / mpCommonPerFrame.frameDim.y);
    vec2 pos =  scale_factor2 * vPosition.xy; //Also apply uniform scaling
    
    //Position
    pos.xy -= 1.0; //Reset pos to left bottom corner of the screen
    pos.xy += offset * (2.0 / mpCommonPerFrame.frameDim.y);
    
    //pos.xy -= 0.8;
    //Convert to line
    //pos += vec2(-1.0, -0.9);
    gl_Position = vec4(pos, 0.0, 1.0);

}


