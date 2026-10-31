/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

 
//Imports
#include "/common.glsl"
#include "/common_structs.glsl"

//Mesh Attributes
layout(location=0) in vec4 vPosition;
layout(location=1) in vec4 uvPosition0;
layout(location=2) in vec4 nPosition; //normals
layout(location=3) in vec4 tPosition; //tangents
layout(location=4) in vec4 bPosition; //bitangents/ vertex color
layout(location=5) in vec4 blendIndices;
layout(location=6) in vec4 blendWeights;
layout(location=11) in uint packedFrame; //packed normal + tangent (semantic 11), meshes without nPosition


uniform CustomPerMaterialUniforms mpCustomPerMaterial;


//Uniform Blocks
layout (std140, binding=0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};

layout (std430, binding=1) buffer _COMMON_PER_MESH
{
    vec3 color; //Mesh Default Color
    float skinned;
    MeshInstance instanceData[]; //Instance world matrices, normal matrices, occlusion and selection status
};

layout (std430, binding=5) readonly buffer _VISIBLE_INSTANCE_IDS
{
    uint visibleInstanceIds[];
};

//Outputs
out vec4 fragPos;
out vec4 screenPos;
out vec4 vertColor;
out float isOccluded;
out float isSelected;
out vec3 mTangentSpaceNormalVec3;
out vec4 uv;
out mat3 TBN;
flat out int instanceId;

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


/*
** Packed normal/tangent frame (geometry semantic 11).
** Bits 0-19: octahedral normal (2 x unorm10). Bits 20-27: tangent angle inside a 45 degree octant,
** measured from the major axis. Bit 28: v negative, bit 29: u positive, bit 30: u is the major axis.
** Bit 31: bitangent sign (1 = negative). u and v are coordinates in a frame built from the normal.
*/
vec3 octDecode(vec2 e)
{
    vec3 n = vec3(e, 1.0 - abs(e.x) - abs(e.y));
    if (n.z < 0.0)
        n.xy = (1.0 - abs(n.yx)) * vec2(n.x >= 0.0 ? 1.0 : -1.0, n.y >= 0.0 ? 1.0 : -1.0);
    return normalize(n);
}

void decodeFrame(uint p, out vec3 N, out vec4 T)
{
    N = octDecode(vec2(float(p & 1023u), float((p >> 10) & 1023u)) / 1023.0 * 2.0 - 1.0);

    float a = float((p >> 20) & 255u) / 256.0 * 0.78539816;
    float ma = cos(a);
    float mi = sin(a);
    bool uMajor = ((p >> 30) & 1u) != 0u;
    float u = (uMajor ? ma : mi) * (((p >> 29) & 1u) != 0u ? 1.0 : -1.0);
    float v = (uMajor ? mi : ma) * (((p >> 28) & 1u) != 0u ? -1.0 : 1.0);

    vec3 f1 = normalize(abs(N.z) > abs(N.y) ? vec3(N.z, 0.0, -N.x) : vec3(N.y, -N.x, 0.0));
    vec3 f2 = cross(N, f1);
    T = vec4(normalize(u * f1 + v * f2), (p >> 31) != 0u ? -1.0 : 1.0);
}


void main()
{
    int stableInstanceId = int(visibleInstanceIds[gl_InstanceID]);

    //Pass uv to fragment shader
    uv = uvPosition0;
    vertColor = bPosition;

    #ifdef __F13_UV_EFFECT
        //Scroll both uv sets: xy feeds the diffuse, normal and masks maps, zw the diffuse2 map
        vec4 lFlippedScrollingUVVec4 = mpCustomPerMaterial.gUVScrollStepVec4;
        uv += lFlippedScrollingUVVec4 * mpCommonPerFrame.gfTime;
    #endif
    
    //Load Per Instance data
    instanceId = stableInstanceId;
    isOccluded = instanceData[stableInstanceId].isOccluded;
    isSelected = instanceData[stableInstanceId].isSelected;
    
    mat4 lWorldMat;
    
    //Check F02_SKINNED
    #if defined(__F02_SKINNED)
        ivec4 index;
        
        index.x = int(blendIndices.x);
        index.y = int(blendIndices.y);
        index.z = int(blendIndices.z);
        index.w = int(blendIndices.w);

        //Assemble matrices from 
        int instanceMatOffset = stableInstanceId * mpCustomPerMaterial.skinMatsStride * 4; //Stride in bones
        lWorldMat =  blendWeights.x * get_skin_matrix(instanceMatOffset + 4 * index.x);
        lWorldMat += blendWeights.y * get_skin_matrix(instanceMatOffset + 4 * index.y);
        lWorldMat += blendWeights.z * get_skin_matrix(instanceMatOffset + 4 * index.z);
        lWorldMat += blendWeights.w * get_skin_matrix(instanceMatOffset + 4 * index.w);
    #else
        lWorldMat = instanceData[stableInstanceId].worldMat;
    #endif
    
    //lWorldMat = instanceData[gl_InstanceID].worldMat;
    vec4 wPos = lWorldMat * vPosition; //Calculate world Position

    #ifdef __D_SCREEN_SIZE
        //Viewer helpers keep their size on screen: scale the local geometry by the distance from the camera
        //(the camera position lives in the rotated scene space)
        vec3 lOrigin = (mpCommonPerFrame.rotMat * (lWorldMat * vec4(0.0, 0.0, 0.0, 1.0))).xyz;
        float lfDist = max(length(lOrigin - mpCommonPerFrame.cameraPosition.xyz), 1e-4);
        wPos = lWorldMat * vec4(vPosition.xyz * (0.5 * lfDist), 1.0);
    #endif
    fragPos = wPos; //Export world position to the fragment shader
    screenPos = mpCommonPerFrame.mvp * mpCommonPerFrame.rotMat * fragPos;
    gl_Position = screenPos;
    
    //Construct TBN matrix
    //Tangent w holds the bitangent sign. bPosition is not used here, location 4 also carries vertex colours
    vec4 lLocalTangentVec4 = tPosition;
    vec4 lLocalNormalVec4 = nPosition;

    //Meshes with the packed frame have no normal attribute bound, so nPosition reads the (0,0,0,1) default
    if (dot(nPosition.xyz, nPosition.xyz) < 1e-8)
    {
        vec3 lPackedNormalVec3;
        vec4 lPackedTangentVec4;
        decodeFrame(packedFrame, lPackedNormalVec3, lPackedTangentVec4);
        lLocalNormalVec4 = vec4(lPackedNormalVec3, 0.0);
        lLocalTangentVec4 = lPackedTangentVec4;
    }
    
    //Normal matrix. rotMat is a pure rotation, so the inverse transpose of (rotMat * worldMat) is
    //rotMat * normalMat, and normalMat is precomputed per instance on the CPU.
    //Skinned vertices are placed by the blended bone matrix; bones carry no non-uniform scale,
    //so its 3x3 part works as the normal matrix (the result is normalized below)
    #if defined(__F02_SKINNED)
        mat3 nMat = mat3(mpCommonPerFrame.rotMat) * mat3(lWorldMat);
    #else
        mat3 nMat = mat3(mpCommonPerFrame.rotMat) * mat3(instanceData[stableInstanceId].normalMat);
    #endif

    vec3 lWorldNormalVec3 = normalize(nMat * lLocalNormalVec4.xyz);
    vec3 lWorldTangentVec3 = nMat * lLocalTangentVec4.xyz;

    //Re-orthogonalize the tangent against the normal (Gram-Schmidt). Fall back to any perpendicular
    //direction when the tangent is missing or parallel to the normal
    lWorldTangentVec3 -= dot(lWorldTangentVec3, lWorldNormalVec3) * lWorldNormalVec3;
    if (dot(lWorldTangentVec3, lWorldTangentVec3) < 1e-8)
        lWorldTangentVec3 = cross(lWorldNormalVec3, abs(lWorldNormalVec3.y) < 0.99 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0));
    lWorldTangentVec3 = normalize(lWorldTangentVec3);

    float lBitangentSign = lLocalTangentVec4.w < 0.0 ? -1.0 : 1.0;
    vec3 lWorldBitangentVec3 = cross(lWorldNormalVec3, lWorldTangentVec3) * lBitangentSign;

    TBN = mat3( lWorldTangentVec3,
                lWorldBitangentVec3,
                lWorldNormalVec3 );

    //Send world normal to fragment shader
    mTangentSpaceNormalVec3 = lWorldNormalVec3;
}


