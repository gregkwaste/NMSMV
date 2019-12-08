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
layout(location=2) in vec4 nPosition; //normals
layout(location=3) in vec4 tPosition; //tangents
layout(location=4) in vec4 bPosition; //bitangents
layout(location=5) in vec4 blendIndices;
layout(location=6) in vec4 blendWeights;

//Uniform Blocks
layout (std140) uniform Uniforms
{
    CommonPerFrameUniforms mpCommonPerFrame;
    CommonPerMeshUniforms mpCommonPerMesh;
};

//Outputs
out vec4 fragPos;
out vec3 N;

void main()
{
    vec4 wPos = vPosition; //Calculate world Position
	fragPos = wPos; //Export world position to the fragment shader
    N = nPosition.xyz;
    gl_Position = mpCommonPerFrame.mvp * wPos;
}


