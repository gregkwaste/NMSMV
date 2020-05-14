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
layout (std140, binding=0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};

layout (std140, binding=1) uniform _COMMON_PER_MESH
{
    CommonPerMeshUniforms mpCommonPerMesh;
};

//Outputs
out vec4 fragPos;
out vec4 vertColor;
out float isOccluded;
out float isSelected;
out vec3 N;
out vec2 uv0;
out mat3 TBN;

/*
** Returns matrix4x4 from texture cache.
*/
mat4 get_skin_matrix(int offset)
{
    return (mat4(texelFetch(mpCustomPerMaterial.skinMatsTex, offset),
                 texelFetch(mpCustomPerMaterial.skinMatsTex, offset + 1),
                 texelFetch(mpCustomPerMaterial.skinMatsTex, offset + 2),
                 texelFetch(mpCustomPerMaterial.skinMatsTex, offset + 3)));
}


//Bool checks for material flags
bool mesh_has_matflag(int FLAG){
    return (mpCustomPerMaterial.matflags[FLAG] > 0.0);
}

void main()
{
    //Pass uv to fragment shader
    uv0 = uvPosition0;
    vertColor = bPosition;

    #ifdef __F14_UVSCROLL
        vec4 lFlippedScrollingUVVec4 = mpCustomPerMaterial.gUVScrollStepVec4;
        //TODO: Convert uvs to vec4 for diffuse2maps
        uv0 += lFlippedScrollingUVVec4.xy * mpCommonPerFrame.gfTime;
    #endif
    
    //Load Per Instance data
    isOccluded = mpCommonPerMesh.instanceData[gl_InstanceID].isOccluded;
    isSelected = mpCommonPerMesh.instanceData[gl_InstanceID].isSelected;
    
    mat4 lWorldMat;
    
    //Check F02_SKINNED
    #ifdef __F02_SKINNED
        ivec4 index;
        
        index.x = int(blendIndices.x);
        index.y = int(blendIndices.y);
        index.z = int(blendIndices.z);
        index.w = int(blendIndices.w);

        //Assemble matrices from 
        int instanceMatOffset = gl_InstanceID * 128 * 4;
        lWorldMat =  blendWeights.x * get_skin_matrix(instanceMatOffset + 4 * index.x);
        lWorldMat += blendWeights.y * get_skin_matrix(instanceMatOffset + 4 * index.y);
        lWorldMat += blendWeights.z * get_skin_matrix(instanceMatOffset + 4 * index.z);
        lWorldMat += blendWeights.w * get_skin_matrix(instanceMatOffset + 4 * index.w);
    #else
        lWorldMat = mpCommonPerMesh.instanceData[gl_InstanceID].worldMat;
    #endif
    
    vec4 wPos = lWorldMat * vPosition; //Calculate world Position
    fragPos = wPos; //Export world position to the fragment shader
    gl_Position = mpCommonPerFrame.mvp * wPos;
    
    //Construct TBN matrix
    //Nullify w components
    vec4 lLocalTangentVec4 = tPosition;
    vec4 lLocalNormalVec4 = nPosition;
    
    mat4 nMat = mpCommonPerMesh.instanceData[gl_InstanceID].normalMat;

    //OLD
    vec4 lWorldTangentVec4 = nMat * lLocalTangentVec4;
    vec4 lWorldNormalVec4 = nMat * lLocalNormalVec4;
    
    vec4 lWorldBitangentVec4 = vec4( cross(lWorldNormalVec4.xyz, lWorldTangentVec4.xyz), 0.0);
    
    TBN = mat3( normalize(lWorldTangentVec4.xyz),
                normalize(lWorldBitangentVec4.xyz),
                normalize(lWorldNormalVec4.xyz) );

    //Send world normal to fragment shader
    N = normalize(lWorldNormalVec4).xyz;

}


