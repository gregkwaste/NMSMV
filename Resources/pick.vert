#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
#extension GL_NV_gpu_shader5 : enable

layout(location=0) in vec4 vPosition;
layout(location=1) in vec2 uvPosition0;
layout(location=2) in vec4 nPosition; //normals
layout(location=3) in vec4 tPosition; //tangents
layout(location=4) in vec4 bPosition; //bitangents
layout(location=5) in vec4 blendIndices;
layout(location=6) in vec4 blendWeights;

layout(location=7) uniform mat4 mvp;
layout(location=8) uniform mat4 nMat;
layout(location=9) uniform mat4 rotMat;
layout(location=10) uniform mat4 worldMat;

layout(location=11) uniform bool matflags[64];
layout(location=78) uniform mat4 skinMats[128];

uniform int id;

flat out int object_id;



void main()
{
	//Check F02_SKINNED
    if (matflags[1]){

    	vec4 wPos = vec4(0.0, 0.0, 0.0, 0.0);
	    ivec4 index;

	    index.x = int(blendIndices.x);
	    index.y = int(blendIndices.y);
	    index.z = int(blendIndices.z);
	    index.w = int(blendIndices.w);

        wPos =  blendWeights.x * skinMats[index.x] * vPosition;
        wPos += blendWeights.y * skinMats[index.y] * vPosition;
        wPos += blendWeights.z * skinMats[index.z] * vPosition;
        wPos += blendWeights.w * skinMats[index.w] * vPosition;

		//gl_PointSize = 10.0;
	    gl_Position = mvp * wPos;
        
    } else{
    	gl_Position = mvp * worldMat * vPosition;
    }
    object_id = id;
}