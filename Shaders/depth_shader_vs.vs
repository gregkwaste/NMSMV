#version 450
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_gpu_shader5 : enable

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

void main()
{
    mat4 lWorldMat;
    //Check F02_SKINNED
    if (mpCommonPerMesh.skinned > 0.0) { //Needs fixing again
        ivec4 index;
        
        index.x = int(blendIndices.x);
	    index.y = int(blendIndices.y);
	    index.z = int(blendIndices.z);
	    index.w = int(blendIndices.w);

        lWorldMat =  blendWeights.x * mpCommonPerMesh.skinMats[index.x];
        lWorldMat += blendWeights.y * mpCommonPerMesh.skinMats[index.y];
        lWorldMat += blendWeights.z * mpCommonPerMesh.skinMats[index.z];
        lWorldMat += blendWeights.w * mpCommonPerMesh.skinMats[index.w];
    } else {
        lWorldMat = mpCommonPerMesh.worldMat;
    }

    vec4 wPos = lWorldMat * vPosition; //Calculate world Position
    fragPos = wPos; //Export world position to the fragment shader
    gl_Position = mpCommonPerFrame.mvp * wPos;
    
}


