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
uniform sampler2DMS depthTex;

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
out float isOccluded;
out float isSelected;
out vec3 N;
out vec3 viewRay;
out vec2 uv0;
out mat3 TBN;
flat out int instance_id;


void main()
{
	uv0 = uvPosition0; //Pass-through UVs
	
	//Load Per Instance data
    instance_id = gl_InstanceID;
    isOccluded = mpCommonPerMesh.instanceData[instance_id].isOccluded;
    isSelected = mpCommonPerMesh.instanceData[instance_id].isSelected;
	mat4 worldMat = mpCommonPerMesh.instanceData[instance_id].worldMat;
    

	vec4 lLocalNormalVec4 = nPosition;
    
    mat4 nMat = mpCommonPerMesh.instanceData[instance_id].normalMat;
    vec4 lWorldNormalVec4 = nMat * lLocalNormalVec4;
	N = normalize(lWorldNormalVec4).xyz;

	//vec4 wPos = inverse(mpCommonPerFrame.lookMatInv) * worldMat * vec4(vPosition.xyz, 1.0);
	//viewRay = wPos.xyz * (15000.0 / -wPos.z);

	fragPos = mpCommonPerFrame.mvp * worldMat * vec4(vPosition.xyz, 1.0);
	gl_Position = fragPos;
}