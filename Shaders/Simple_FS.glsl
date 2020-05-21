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

//Uniform Blocks
layout (std140, binding=0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};

layout (std140, binding=1) uniform _COMMON_PER_MESH
{
    CommonPerMeshUniforms mpCommonPerMesh;
};


in vec4 fragPos;
in vec4 vertColor;
in vec3 N;
in vec2 uv0;
in mat3 TBN;
in float isOccluded;
in float isSelected;

//Deferred Shading outputs
out vec4 outcolors[3];

//New Decoding function - RGTC
vec3 DecodeNormalMap(vec4 lNormalTexVec4 ){
    lNormalTexVec4 = ( lNormalTexVec4 * ( 2.0 * 255.0 / 256.0 ) ) - 1.0;
    return ( vec3( lNormalTexVec4.r, lNormalTexVec4.g, sqrt( max( 1.0 - lNormalTexVec4.r*lNormalTexVec4.r - lNormalTexVec4.g*lNormalTexVec4.g, 0.0 ) ) ) );
}

//Fetches the mipmap level
float get_mipmap_level(){
	float mipmaplevel = 0.0;
	#ifdef __F01_DIFFUSEMAP
		mipmaplevel = textureQueryLOD(mpCustomPerMaterial.gDiffuseMap, uv0).x;
	#endif
	return mipmaplevel;
}

//Calculates the diffuse color
vec4 calcDiffuseColor(float mipmaplevel, out float lHighAlpha, out float lLowAlpha, out float difftTex2Factor){
	vec4 diffTexColor; 
	lLowAlpha = 1.0;
	lHighAlpha = 1.0;

	//Check _F01_DIFFUSEMAP
#ifdef __F01_DIFFUSEMAP
	#ifdef __F55_MULTITEXTURE
		diffTexColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(uv0, mpCommonPerMesh.gUserDataVec4.w), mipmaplevel);
		//diffTexColor = vec4(1.0, 1.0, 1.0, 1.0);
	#else
		diffTexColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(uv0, 0.0), mipmaplevel);
		//diffTexColor = vec4(1.0, 0.0, 0.0, 1.0);
	#endif

	//diffTexColor = diffTexColor / diffTexColor.a;
	#if !defined(__F07_UNLIT) && defined(__F39_METALLIC_MASK)
		#if defined(__F34_GLOW) && defined(__F35_GLOW_MASK) && !defined(__F09_TRANSPARENT)
			lHighAlpha = GetUpperValue(diffTexColor.a);
		#else
			lHighAlpha = diffTexColor.a;
		#endif
	#endif
	
	#if defined(__F34_GLOW) && defined (__F35_GLOW_MASK) && !defined (__F09_TRANSPARENT)
		lLowAlpha = GetLowerValue(diffTexColor.a);
	#endif
	
	#if !defined(__F09_TRANSPARENT) && !defined(__F11_ALPHACUTOUT)
		diffTexColor.a = 1.0;
	#endif

#else
	diffTexColor = mpCustomPerMaterial.gMaterialColourVec4;
#endif

#ifdef __F16_DIFFUSE2MAP
	vec4 lDiffuse2Vec4 = textureLod(mpCustomPerMaterial.gDiffuse2Map, vec3(uv0, 0.0), mipmaplevel);
	difftTex2Factor = lDiffuse2Vec4.a;

	#ifndef __F17_MULTIPLYDIFFUSE2MAP
		diffTexColor.rgb = mix( diffTexColor.rgb, lDiffuse2Vec4.rgb, lDiffuse2Vec4.a );
	#endif
#endif

#ifdef __F21_VERTEXCOLOUR
	diffTexColor *= vertColor;
#endif
	
	//Apply gamma correction
    //diffTexColor.rgb = fixColorGamma(diffTexColor.rgb);
    //diffTexColor.rgb = GammaCorrectInput(diffTexColor.rgb);
	return diffTexColor;
}


//calculates the normal
vec3 calcNormal(float mipmaplevel){
	vec3 normal = normalize(N);
	#ifdef __F03_NORMALMAP
		vec2 lTexCoordsVec2 = uv0;
		#ifdef __F43_NORMAL_TILING
			lTexCoordsVec2 *= mpCustomPerMaterial.gCustomParams01Vec4.z;
		#endif
		normal = DecodeNormalMap(textureLod(mpCustomPerMaterial.gNormalMap, vec3(lTexCoordsVec2, 0.0), mipmaplevel));
  		normal = normalize(TBN * normal);
	#endif
  	return (vec4(normal, 0.0)).xyz; //This is normalized in any case
}

float calcRoughness(float mipmaplevel){
	float lfRoughness = 0.0;
	#if defined(__F25_ROUGHNESS_MASK) && !defined(__F07_UNLIT)
		lfRoughness = textureLod(mpCustomPerMaterial.gMasksMap, vec3(uv0, 0.0), mipmaplevel).g;
		lfRoughness = 1.0 - lfRoughness;
	#else
		lfRoughness *= mpCustomPerMaterial.gMaterialParamsVec4.x;
	#endif
	return lfRoughness;
}

float calcAO(float mipmaplevel){
	float ao = 1.0;
	#ifdef __F24_AOMAP
		ao = textureLod(mpCustomPerMaterial.gMasksMap, vec3(uv0, 0.0), mipmaplevel).r;
	#endif
	return ao;
}

float calcMetallic(float lHighAlpha){
	float lfMetallic = mpCustomPerMaterial.gMaterialParamsVec4.z;
	#if defined(__F39_METALLIC_MASK) && !defined(__F07_UNLIT)
		lfMetallic = lHighAlpha;
	#else
		lfMetallic = mpCustomPerMaterial.gMaterialParamsVec4.z;
	#endif

	return lfMetallic;
}

vec4 ApplySelectedColor(vec4 color){
	vec4 new_col = color;
	if (isSelected > 0.0)
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
	float diffTex2Factor = 1.0;
	vec3 normal; //Fragment normal
	float lfRoughness = 1.0;
	float lfMetallic = 0.0;
	float lfSubsurface = 0.0; //Not used atm
	float ao = 1.0;
	float lLowAlpha = 1.0; //TODO : Find out what exactly is that shit
	float lHighAlpha = 1.0; //TODO : Find out what exactly is that shit

	if (mpCommonPerFrame.diffuseFlag > 0.0){
		float mipmaplevel = get_mipmap_level();
		
		//Fetch albedo
		diffTexColor = calcDiffuseColor(mipmaplevel, lHighAlpha, lLowAlpha, diffTex2Factor);
		
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

	#ifdef __F62_DETAIL_ALPHACUTOUT
		kfAlphaThreshold = 0.1;
		kfAlphaThresholdMax = 0.5;
	#elif defined (__F11_ALPHACUTOUT)
		//kfAlphaThreshold = 0.45; OLD
		//kfAlphaThresholdMax = 0.8;
		kfAlphaThreshold = 0.5;
		kfAlphaThresholdMax = 0.9;
	#endif
	
	//Mask Checks
	#ifdef __F22_TRANSPARENT_SCALAR
		// Transparency scalar comes from float in Material
        diffTexColor.a *= mpCustomPerMaterial.gMaterialColourVec4.a;
    #endif
	
	#if defined(__F09_TRANSPARENT) || defined(__F22_TRANSPARENT_SCALAR) || defined(__F11_ALPHACUTOUT)
		if (diffTexColor.a < kfAlphaThreshold) discard;
		#ifdef __F11_ALPHACUTOUT
			diffTexColor.a = smoothstep(kfAlphaThreshold, kfAlphaThresholdMax, diffTexColor.a);

			if (diffTexColor.a < kfAlphaThreshold + 0.1) discard;
		#endif
	#endif

	#ifdef __F24_AOMAP
		diffTexColor.rgb *= ao;
	#endif

	#ifdef __F17_MULTIPLYDIFFUSE2MAP
		diffTexColor.rgb *= diffTex2Factor;
	#endif

	//Get Glow
	float lfGlow = 0.0;
	
	#if defined(__F35_GLOW_MASK) && !defined(__F09_TRANSPARENT)
		lfGlow = mpCustomPerMaterial.gCustomParams01Vec4.y * lLowAlpha;
	#elif defined(__F34_GLOW)
		lfGlow = mpCustomPerMaterial.gCustomParams01Vec4.y;
	#endif

	#ifdef __F34_GLOW
		diffTexColor.rgb = mix( diffTexColor.rgb, diffTexColor.rgb, lfGlow );
		#ifdef __F35_GLOW_MASK
			diffTexColor.a = lfGlow;
		#endif
	#endif

#ifdef _D_DEFERRED_RENDERING
	//Save Info to GBuffer
    //Albedo
	outcolors[0] = diffTexColor;
	//outcolors[0].rgb = vec3(lfGlow, lfGlow, 0);
	//outcolors[0].rgb = lfGlow * mpCustomPerMaterial.gMaterialColourVec4.xyz;
	outcolors[1].rgb = normal;	
	
	//Export Frag Params
	outcolors[2].x = ao;
	outcolors[2].y = lfMetallic;
	outcolors[2].z = lfRoughness;
	outcolors[2].a = lfGlow;

#else
	
	//FORWARD LIGHTING
	vec4 finalColor = vec4(0.0, 0.0, 0.0, diffTexColor.a);

	#ifndef __F07_UNLIT
		for(int i = 0; i < mpCommonPerFrame.light_count; ++i) 
	    {
	    	// calculate per-light radiance
	        Light light = mpCommonPerFrame.lights[i]; 

			//Pos.w is the renderable status of the light
			if (light.position.w < 1.0)
	        	continue;
    		
    		finalColor.rgb += calcLighting(light, fragPos, normal, mpCommonPerFrame.cameraPosition,
	            diffTexColor.rgb, lfMetallic, lfRoughness, ao);
		} 

	#else
		finalColor = diffTexColor;
	#endif

	//finalColor.rgb = vec3(1.0, 1.0, 1.0);
	//TODO: Add glow depending on the material parameters cached in the gbuffer (normalmap.a) if necessary
	
	#ifdef __F34_GLOW
		finalColor.rgb = mix( finalColor.rgb, diffTexColor.rgb, lfGlow );
		#ifdef __F35_GLOW_MASK
			finalColor.a = lfGlow;
		#endif
	#endif

	// Exposure tone mapping
	float gamma = 2.2;
    //finalColor.rgb = vec3(1.0) - exp(-finalColor.rgb * mpCommonPerFrame.HDRExposure);
    

    // Gamma correction 
    //finalColor.rgb = pow(finalColor.rgb, vec3(1.0 / gamma));

    outcolors[0] = finalColor;

#endif
}


void main(){

	//Occlusion is properly applied per instance in the main code. No need to discard anything here
	if (isOccluded > 0.0)
		discard;

	pbr_lighting();
}