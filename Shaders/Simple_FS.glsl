#version 450
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_texture_query_lod : enable
#extension GL_ARB_gpu_shader5 : enable

//Includes
#include "/common.glsl"
#include "/common_structs.glsl"

//TODO: Do some queries internally and figure out the exact locations of the uniforms
uniform CustomPerMaterialUniforms mpCustomPerMaterial;
uniform CommonPerFrameSamplers mpCommonPerFrameSamplers;

//Uniform Blocks
layout (std140) uniform Uniforms
{
    CommonPerFrameUniforms mpCommonPerFrame;
    CommonPerMeshUniforms mpCommonPerMesh;
};

in vec4 fragPos;
in vec3 N;
in vec2 uv0;
in mat3 TBN;

//Deferred Shading outputs
out vec4 outcolors[5];

//New Decoding function - RGTC
vec3 DecodeNormalMap(vec4 lNormalTexVec4 ){
    lNormalTexVec4 = ( lNormalTexVec4 * ( 2.0 * 255.0 / 256.0 ) ) - 1.0;
    return ( vec3( lNormalTexVec4.r, lNormalTexVec4.g, sqrt( max( 1.0 - lNormalTexVec4.r*lNormalTexVec4.r - lNormalTexVec4.g*lNormalTexVec4.g, 0.0 ) ) ) );
}

//Bool checks for material flags
bool mesh_has_matflag(int FLAG){
	return (mpCustomPerMaterial.matflags[FLAG] > 0.0);
}

//Fetches the mipmap level
float get_mipmap_level(){
	float mipmaplevel = 0.0;
	if (mesh_has_matflag(_F01_DIFFUSEMAP)) {
		mipmaplevel = textureQueryLOD(mpCustomPerMaterial.gDiffuseMap, uv0).x;
	}

	return mipmaplevel;
}



//Calculates the diffuse color
vec4 calcDiffuseColor(float mipmaplevel, out float lHighAlpha, out float lLowAlpha){
	vec4 diffTexColor; 
	lLowAlpha = 1.0;
	lHighAlpha = 1.0;

	//Check _F01_DIFFUSEMAP
	if (mesh_has_matflag(_F01_DIFFUSEMAP)) {
		if (mesh_has_matflag(_F55_MULTITEXTURE)){	
			diffTexColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(uv0, mpCommonPerMesh.gUserDataVec4.w), mipmaplevel);
			//diffTexColor = vec4(1.0, 1.0, 1.0, 1.0);
		}
		else {
			diffTexColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(uv0, 0.0), mipmaplevel);
			//diffTexColor = vec4(1.0, 0.0, 0.0, 1.0);
		}

		if (!mesh_has_matflag(_F07_UNLIT) && mesh_has_matflag(_F39_METALLIC_MASK)){
			if (mesh_has_matflag(_F34_GLOW) && mesh_has_matflag(_F35_GLOW_MASK) && !mesh_has_matflag(_F09_TRANSPARENT)){
				lHighAlpha = GetUpperValue(diffTexColor.a);
			} else{
				lHighAlpha = diffTexColor.a;
			}
		}

		if (mesh_has_matflag(_F34_GLOW) && mesh_has_matflag(_F35_GLOW_MASK) && !mesh_has_matflag(_F09_TRANSPARENT)){
			lLowAlpha = GetLowerValue(diffTexColor.a);
		}

		if (!mesh_has_matflag(_F09_TRANSPARENT) && !mesh_has_matflag(_F11_ALPHACUTOUT)){
			diffTexColor.a = 1.0;
		}

	} else {
		diffTexColor = mpCustomPerMaterial.gMaterialColourVec4;
		//diffTexColor = vec4(mpCustomPerMaterial.matflags[_F01_DIFFUSEMAP], 0.0, 0.0, 1.0);
	}
	
	//Apply gamma correction
    //diffTexColor.rgb = fixColorGamma(diffTexColor.rgb);
    //diffTexColor.rgb = GammaCorrectInput(diffTexColor.rgb);
	return diffTexColor;
}


//calculates the normal
vec3 calcNormal(float mipmaplevel){
	vec3 normal = normalize(N);
	//Check _F03_NORMALMAP 63
	if (mesh_has_matflag(_F03_NORMALMAP)) {
		normal = DecodeNormalMap(textureLod(mpCustomPerMaterial.gNormalMap, vec3(uv0, 0.0), mipmaplevel));
  		normal = normalize(TBN * normal);
	}
  	return (vec4(normal, 0.0)).xyz; //This is normalized in any case
}

float calcRoughness(float mipmaplevel){
	float lfRoughness = 1.0;
	if (mesh_has_matflag(_F25_ROUGHNESS_MASK) && !mesh_has_matflag(_F07_UNLIT)) {
		lfRoughness = textureLod(mpCustomPerMaterial.gMasksMap, vec3(uv0, 0.0), mipmaplevel).g;
		lfRoughness = 1.0 - lfRoughness;
	} 

	lfRoughness *= mpCustomPerMaterial.gMaterialParamsVec4.x;
	return lfRoughness;
}

float calcAO(float mipmaplevel){
	float ao = 0.0;
	if (mesh_has_matflag(_F24_AOMAP)) {
 		ao = textureLod(mpCustomPerMaterial.gMasksMap, vec3(uv0, 0.0), mipmaplevel).r;
	}
	return ao;
}

float calcMetallic(float lHighAlpha){
	float lfMetallic = mpCustomPerMaterial.gMaterialParamsVec4.z;
	if (mesh_has_matflag(_F39_METALLIC_MASK) && !mesh_has_matflag(_F07_UNLIT))
		lfMetallic = lHighAlpha;
	else{
		lfMetallic = mpCustomPerMaterial.gMaterialParamsVec4.z;
	}
	return lfMetallic;
}

vec4 ApplySelectedColor(vec4 color){
	vec4 new_col = color;
	if (mpCommonPerMesh.selected > 0.0)
		new_col *= vec4(0.005, 1.5, 0.005, 1.0);
	return new_col;
}

float calcShadow(vec4 _fragPos, Light light){
	// get vector between fragment position and light position
	vec3 fragToLight = (_fragPos - light.position).xyz;
	
	// use the light to fragment vector to sample from the depth map 
	float closestDepth = texture(mpCommonPerFrameSamplers.depthMap, fragToLight).r; 
	
	// it is currently in linear range between [0,1]. Re-transform back to original value 
	closestDepth *= mpCommonPerFrame.cameraFarPlane; 
	
	// now get current linear depth as the length between the fragment and light position 
	float currentDepth = length(fragToLight);

	// now test for shadows
	float bias = 0.05;
	float shadow = currentDepth - bias > closestDepth ? 1.0 : 0.0;
	
	return shadow;
}


void pbr_lighting(){

	//Final Light/Normal vector calculations
	vec4 diffTexColor;
	vec3 normal; //Fragment normal
	float lfRoughness = 1.0;
	float lfMetallic = 0.0;
	float lfSubsurface = 0.0; //Not used atm
	float ao = 0.0;
	float lLowAlpha = 1.0; //TODO : Find out what exactly is that shit
	float lHighAlpha = 1.0; //TODO : Find out what exactly is that shit

	if (mpCommonPerFrame.diffuseFlag > 0.0){
		float mipmaplevel = get_mipmap_level();
		
		//Fetch albedo
		diffTexColor = calcDiffuseColor(mipmaplevel, lHighAlpha, lLowAlpha);
		
		//Fetch roughness value
		lfRoughness = calcRoughness(mipmaplevel);
		
		//Fetch metallic value
		lfMetallic = calcMetallic(lHighAlpha);

		//Fetch AO value
	 	ao = calcAO(mipmaplevel);

	 	//Try to load normal from NormalTexture as well
	 	normal = calcNormal(mipmaplevel);
	} else {
		diffTexColor = vec4(mpCommonPerMesh.color, 1.0);
		normal = N;
	}


	//TRANSPARENCY

	//Set alphaTrehsholds
	float kfAlphaThreshold = 0.0001;
	float kfAlphaThresholdMax = 0.8;

	if (mesh_has_matflag(_F62_DETAIL_ALPHACUTOUT)){
		kfAlphaThreshold = 0.1;
		kfAlphaThresholdMax = 0.5;
	} else if (mesh_has_matflag(_F11_ALPHACUTOUT)){
		kfAlphaThreshold = 0.45;
		kfAlphaThresholdMax = 0.8;
	}

	//Mask Checks
	if (mesh_has_matflag(_F22_TRANSPARENT_SCALAR)) {
		// Transparency scalar comes from float in Material
        diffTexColor.a *= mpCustomPerMaterial.gMaterialColourVec4.a;
	}

	if (mesh_has_matflag(_F09_TRANSPARENT) || mesh_has_matflag(_F22_TRANSPARENT_SCALAR)|| mesh_has_matflag(_F11_ALPHACUTOUT)) {
		if (diffTexColor.a < kfAlphaThreshold) discard;

		if (mesh_has_matflag(_F11_ALPHACUTOUT)){
			diffTexColor.a = smoothstep(kfAlphaThreshold, kfAlphaThresholdMax, diffTexColor.a);
		}

	}

	//Get Glow
	float lfGlow = 0.0;
	if (mesh_has_matflag(_F35_GLOW_MASK) && !mesh_has_matflag( _F09_TRANSPARENT )){
		lfGlow = mpCustomPerMaterial.gCustomParams01Vec4.y * lLowAlpha;
	} else if (mesh_has_matflag( _F34_GLOW)) {
		lfGlow = mpCustomPerMaterial.gCustomParams01Vec4.y;
	}
    
 	//TODO Save ambient and ao in another color attachment
    vec3 ambient = vec3(0.03) * diffTexColor.rgb * ao;


#ifdef _DEFERRED_RENDERING

    //Save Info
    //Albedo
	outcolors[0] = diffTexColor;
	//outcolors[0].rgb = vec3(lfGlow, lfGlow, 0);
	//outcolors[0].rgb = lfGlow * mpCustomPerMaterial.gMaterialColourVec4.xyz;
	//Positions
	outcolors[1].rgb = fragPos.xyz;	
	//Normals + lfGlow in alpha channel
	outcolors[2].rgb = normal;	
	
	//Do not use the 3rd channel which is the color after lighting

	//Export Frag Params
	outcolors[4].x = ao;
	outcolors[4].y = lfMetallic;
	outcolors[4].z = lfRoughness;
	outcolors[4].a = lfGlow;
#else
	outcolors[0] = vec4(1.0, 0.0, 0.0, 0.0);
#endif
}


void main(){

	pbr_lighting();
}