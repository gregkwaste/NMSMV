#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
#extension GL_NV_gpu_shader5 : enable

/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */

//Imports
#include "/common.glsl"

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
layout(location=75) uniform mat4 skinMats[128];

uniform vec3 light;

//Outputs
out vec3 E;
out vec3 N;
out vec2 uv0;
out float l_distance;
out mat3 TBN;
out float bColor;
out vec4 finalPos;
out vec4 finalNormal;


void main()
{
	vec4 light4 = vec4(light, 1.0);
    //Pass uv
    uv0 = uvPosition0;
    //Pas normal mapping related vectors
    //nvectors[0] = tPosition.xyz;
    //nvectors[1] = cross(tPosition.xyz, nPosition.xyz);
    //nvectors[2] = nPosition.xyz;
	// Remeber: thse matrices are column-major
    
    vec4 wPos = vec4(0.0, 0.0, 0.0, 0.0);
    mat4 nMat;
    mat4 lWorldMat;
    //Check F02_SKINNED
    if (matflags[_F02_SKINNED]) { //Needs fixing again
        ivec4 index;
        
        index.x = int(blendIndices.x);
	    index.y = int(blendIndices.y);
	    index.z = int(blendIndices.z);
	    index.w = int(blendIndices.w);

        lWorldMat = blendWeights.x * skinMats[index.x];
        lWorldMat += blendWeights.y * skinMats[index.y];
        lWorldMat += blendWeights.z * skinMats[index.z];
        lWorldMat += blendWeights.w * skinMats[index.w];
        
        wPos = lWorldMat * vPosition;
		bColor = blendIndices.x/255.0;
	    
	    //gl_PointSize = 10.0;
    } else {
        lWorldMat = worldMat;
        wPos = worldMat * vPosition;
    }

    gl_Position = mvp * wPos;
    
    nMat = transpose(inverse(rotMat * lWorldMat));

    N = normalize(nMat * nPosition).xyz;
    
    //mat4 nMat = rotMat;
    
    //TBN = transpose(TBN);
    //Construct TBN matrix
    //Nullify w components
    vec4 lLocalTangentVec4 = tPosition;
    vec4 lLocalBitangentVec4 = vec4(bPosition.xyz, 0.0);
    vec4 lLocalNormalVec4 = nPosition;
    
    vec4 lWorldTangentVec4 = nMat * lLocalTangentVec4;
    vec4 lWorldNormalVec4 = nMat * lLocalNormalVec4;
    vec4 lWorldBitangentVec4 = vec4( cross(lWorldNormalVec4.xyz, lWorldTangentVec4.xyz), 0.0);
    
    TBN = mat3( normalize(lWorldTangentVec4.xyz),
                normalize(lWorldBitangentVec4.xyz),
                normalize(lWorldNormalVec4.xyz) );

    //Calculate Lighting stuff
    //gl_FrontColor = gl_Color;
    E = (rotMat * (light4 - wPos)).xyz; //Light vector
    l_distance = distance(wPos.xyz, light);
    //E = - ((vPosition-light4)).xyz; //Light vector
    E = normalize(E);
    //E = - rotMat(vPosition-light4).xyz; //Light vector

}