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
layout(location=78) uniform int16_t boneRemap[512];
layout(location=590) uniform vec4 skinMatRows[128 * 3];

uniform int id;



flat out int object_id;



vec4 getSkinPosition(int index, vec4 skinMatRows[128 * 3], vec4 position){
	mat4 skinMatrix = mat4( skinMatRows[index * 3 + 0],
							skinMatRows[index * 3 + 1],
							skinMatRows[index * 3 + 2],
							vec4(0.0, 0.0, 0.0, 1.0));

	return skinMatrix * position;
}


void main()
{
	//Check F02_SKINNED
    if (matflags[1]){

    	vec4 wPos=vec4(0.0, 0.0, 0.0, 0.0);
	    ivec4 index;

		index.x = boneRemap[int(blendIndices.x)];
	    index.y = boneRemap[int(blendIndices.y)];
	    index.z = boneRemap[int(blendIndices.z)];
	    index.w = boneRemap[int(blendIndices.w)];

		wPos =  blendWeights.x * getSkinPosition(index.x, skinMatRows, vPosition);
		wPos += blendWeights.y * getSkinPosition(index.y, skinMatRows, vPosition);
		wPos += blendWeights.z * getSkinPosition(index.z, skinMatRows, vPosition);
		wPos += blendWeights.w * getSkinPosition(index.w, skinMatRows, vPosition);

		gl_Position = mvp * wPos;
        
    } else{
    	gl_Position = mvp * worldMat * vPosition;
    }
    object_id = id;
}