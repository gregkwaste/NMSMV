/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

//Includes
#include "/common.glsl"
#include "/common_structs.glsl"
#include "/common_lighting.glsl"


//TODO: Do some queries internally and figure out the exact locations of the uniforms
uniform CustomPerMaterialUniforms mpCustomPerMaterial;
uniform CommonPerFrameSamplers mpCommonPerFrameSamplers;
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


in vec2 uv0;
in vec3 N;
in vec4 fragPos;
in vec3 viewRay;
in float isOccluded;
flat in int instance_id;


//Deferred Shading outputs
out vec4 outcolors[6];


//Bool checks for material flags
bool mesh_has_matflag(int FLAG){
    return (mpCustomPerMaterial.matflags[FLAG] > 0.0);
}

vec2 clipToUV(vec2 test){
	//TODO: Check if we should include the depth buffer aspect ratio in the calculation
	return 0.5 * test + 0.5;
}

vec2 UVToClip(vec2 test){
	return 2.0 * test - 1.0;
}


void clip(float test) { if (test < 0.0) discard; }


vec4 worldfromDepth(out float depth)
{
	vec4 world;
	world.xy = fragPos.xy;
	world.xy /= fragPos.w;

	vec2 depth_uv = 0.5 * world.xy + 0.5; //Convert to (0:1) range

	depth = 0.0;
	for (int i=0; i<8; i++){
		depth += texelFetch(depthTex, ivec2(gl_FragCoord.xy), i).x;
	}

	depth *= 0.125;
	
	//Fetch the value from the depth Texture (0:1) and convert it back to (-1:1)
	world.z = 2.0 * depth - 1.0; 
	world.w = 1.0f;

	world = mpCommonPerFrame.projMatInv * world;
	world /= world.w;
	world = mpCommonPerFrame.lookMatInv * world;
	world /= world.w;
	
	return world;
}


void main()
{
	if (isOccluded > 0.0)
		discard;

	float isLit = 0.0;

	float depth = 0.0;
    vec4 world = worldfromDepth(depth);

	//Convert vertex to the local space of the box
	
	//WAY ONE : NOT WORKING
	vec4 localPos = mpCommonPerMesh.instanceData[instance_id].worldMatInv * world;
	localPos /= localPos.w;

	//Clip
	clip(0.5 - abs(localPos.x));
	clip(0.5 - abs(localPos.y));
	clip(0.5 - abs(localPos.z));
	


	vec2 texCoords = 0.5 * localPos.xy + 0.5;
	texCoords.y *= -1; //Flip on Y axis


	//TODO: Use proper mipmap calculation here as well
	vec4 color = vec4(1.0);

	if (mesh_has_matflag(_F51_DECAL_DIFFUSE)){
		color = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(texCoords, 0.0), 0);
		//color = vec4(1.0, 0.0, 0.0, 1.0);
	} else {
		color = mpCustomPerMaterial.gMaterialColourVec4;	
	}

	if (!mesh_has_matflag(_F07_UNLIT))
		isLit = 1.0;


	//TRANSPARENCY

	//Set alphaTrehsholds
	float kfAlphaThreshold = 0.0001;
	float kfAlphaThresholdMax = 0.8;

	if (mesh_has_matflag(_F62_DETAIL_ALPHACUTOUT)){
		kfAlphaThreshold = 0.1;
		kfAlphaThresholdMax = 0.5;
	} else if (mesh_has_matflag(_F11_ALPHACUTOUT)){
		//kfAlphaThreshold = 0.45; OLD
		//kfAlphaThresholdMax = 0.8;
		kfAlphaThreshold = 0.5;
		kfAlphaThresholdMax = 0.9;
		
	}

	//Mask Checks
	if (mesh_has_matflag(_F22_TRANSPARENT_SCALAR)) {
		// Transparency scalar comes from float in Material
        color.a *= mpCustomPerMaterial.gMaterialColourVec4.a;
	}

	if (mesh_has_matflag(_F09_TRANSPARENT) || mesh_has_matflag(_F22_TRANSPARENT_SCALAR)|| mesh_has_matflag(_F11_ALPHACUTOUT)) {
		if (color.a < kfAlphaThreshold) discard;

		if (mesh_has_matflag(_F11_ALPHACUTOUT)){
			color.a = smoothstep(kfAlphaThreshold, kfAlphaThresholdMax, color.a);

			if (color.a < kfAlphaThreshold + 0.1) discard;
		}
	}


	//Save Info to GBuffer
    //Albedo
	outcolors[0] = color;
	//outcolors[0] = vec4(depth);
	//outcolors[0] = vec4(depth);
	//Positions
	outcolors[1].rgb = world.xyz;
	//Normals in alpha channel
	outcolors[2].rgb = N;
	//Do not use the 3rd channel which is the color after lighting

	//Export Frag Params
	outcolors[4].x = 0.5;
	outcolors[4].y = 0.0;
	outcolors[4].z = 0.0;
	outcolors[4].a = 0.0;
	outcolors[5].x = isLit;
}
