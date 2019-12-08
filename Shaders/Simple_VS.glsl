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
layout(location=4) in vec4 bPosition; //bitangents/ vertex color
layout(location=5) in vec4 blendIndices;
layout(location=6) in vec4 blendWeights;


uniform CustomPerMaterialUniforms mpCustomPerMaterial;

//Uniform Blocks
layout (std140) uniform Uniforms
{
    CommonPerFrameUniforms mpCommonPerFrame;
    CommonPerMeshUniforms mpCommonPerMesh;
};

//Outputs
out vec4 fragPos;
out vec4 vertColor;
out float isOccluded;
out vec3 N;
out vec2 uv0;
out mat3 TBN;



//Bool checks for material flags
bool mesh_has_matflag(int FLAG){
    return (mpCustomPerMaterial.matflags[FLAG] > 0.0);
}

void main()
{
    //Pass uv to fragment shader
    uv0 = uvPosition0;
    vertColor = bPosition;

    if (mesh_has_matflag(_F14_UVSCROLL)) {
        vec4 lFlippedScrollingUVVec4 = mpCustomPerMaterial.gUVScrollStepVec4;
        //TODO: Convert uvs to vec4 for diffuse2maps
        uv0 += lFlippedScrollingUVVec4.xy * mpCommonPerFrame.gfTime;
    }

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
        lWorldMat = mpCommonPerMesh.instanceData[gl_InstanceID].worldMat;
    }

    vec4 wPos = lWorldMat * vPosition; //Calculate world Position
    fragPos = wPos; //Export world position to the fragment shader
    gl_Position = mpCommonPerFrame.mvp * wPos;
    
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

    //Send world normal to fragment shader
    N = normalize(lWorldNormalVec4).xyz;
}


