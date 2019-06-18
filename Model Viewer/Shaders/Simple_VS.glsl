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
out vec3 E;
out vec3 N;
out vec2 uv0;
out float l_distance;
out mat3 TBN;
out vec3 default_color;
out vec4 finalPos;
out vec4 finalNormal;

void main()
{
    //vec4 light4 = lights[0].position;
    vec4 light4 = vec4(0.0, 0.0, 0.0, 0.0);
	//Pass uv
    uv0 = uvPosition0;
    //Pas normal mapping related vectors
    //nvectors[0] = tPosition.xyz;
    //nvectors[1] = cross(tPosition.xyz, nPosition.xyz);
    //nvectors[2] = nPosition.xyz;
	// Remeber: thse matrices are column-major
    
    vec4 wPos = vec4(0.0, 0.0, 0.0, 0.0);
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
        
        wPos = lWorldMat * vPosition;
		default_color = mpCommonPerMesh.color;
	    
	    //gl_PointSize = 10.0;
    } else {
        lWorldMat = mpCommonPerMesh.worldMat;
        wPos = mpCommonPerMesh.worldMat * vPosition;
    }

    gl_Position = mpCommonPerFrame.mvp * wPos;
    
    //TODO: Move that shit to the CPU within the CommonPerMeshUniforms
    //nMat = transpose(inverse(mpCommonPerFrame.rotMat * lWorldMat));

    N = normalize(mpCommonPerMesh.nMat * nPosition).xyz;
    
    //mat4 nMat = rotMat;
    
    //TBN = transpose(TBN);
    //Construct TBN matrix
    //Nullify w components
    vec4 lLocalTangentVec4 = tPosition;
    vec4 lLocalBitangentVec4 = vec4(bPosition.xyz, 0.0);
    vec4 lLocalNormalVec4 = nPosition;
    
    vec4 lWorldTangentVec4 = mpCommonPerMesh.nMat * lLocalTangentVec4;
    vec4 lWorldNormalVec4 = mpCommonPerMesh.nMat * lLocalNormalVec4;
    vec4 lWorldBitangentVec4 = vec4( cross(lWorldNormalVec4.xyz, lWorldTangentVec4.xyz), 0.0);
    
    TBN = mat3( normalize(lWorldTangentVec4.xyz),
                normalize(lWorldBitangentVec4.xyz),
                normalize(lWorldNormalVec4.xyz) );

    //Calculate Lighting stuff
    //gl_FrontColor = gl_Color;
    E = (mpCommonPerFrame.rotMat * (light4 - wPos)).xyz; //Light vector
    l_distance = distance(wPos.xyz, light4.xyz);
    //E = - ((vPosition-light4)).xyz; //Light vector
    E = normalize(E);
    //E = - rotMat(vPosition-light4).xyz; //Light vector

}


