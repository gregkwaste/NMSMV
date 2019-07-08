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
uniform Light light; //Support up to 4 lights for now

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
out vec4 outcolors[3];

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
vec4 calcDiffuseColor(float mipmaplevel){
	vec4 diffTexColor; 
	
	//Check _F01_DIFFUSEMAP
	if (mesh_has_matflag(_F01_DIFFUSEMAP)) {
		if (mesh_has_matflag(_F55_MULTITEXTURE)){	
			diffTexColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(uv0, mpCustomPerMaterial.gUserDataVec4.w), mipmaplevel);
			//diffTexColor = vec4(1.0, 1.0, 1.0, 1.0);
		}
		else {
			diffTexColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(uv0, 0.0), mipmaplevel);
			//diffTexColor = vec4(1.0, 0.0, 0.0, 1.0);
		}

		if (!mesh_has_matflag(_F09_TRANSPARENT) || !mesh_has_matflag(_F22_TRANSPARENT_SCALAR)){
			diffTexColor.a = 1.0f;
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
		normal = DecodeNormalMap(textureLod(mpCustomPerMaterial.gNormalMap, vec3(uv0,0.0), mipmaplevel));
  		normal = normalize(TBN * normal);
	}
  	return normal; //This is normalized in any case
}

float calcRoughness(float mipmaplevel){
	float lfRoughness = 1.0;
	if (mesh_has_matflag(_F25_ROUGHNESS_MASK)) {
		lfRoughness = textureLod(mpCustomPerMaterial.gMasksMap, vec3(uv0, 0.0), mipmaplevel).g;
		lfRoughness = 1.0 - lfRoughness;
		lfRoughness *= mpCustomPerMaterial.gMaterialParamsVec4.x;
	} 
	return lfRoughness;
}

float calcMetallic(float alpha){
	float lfMetallic = mpCustomPerMaterial.gMaterialParamsVec4.z;
	if (mesh_has_matflag(_F39_METALLIC_MASK))
		lfMetallic = GetUpperValue(alpha);
	return lfMetallic;
}

vec4 ApplySelectedColor(vec4 color){
	vec4 new_col = color;
	if (mpCommonPerMesh.selected > 0.0)
		new_col *= vec4(0.005, 1.5, 0.005, 1.0);
	return new_col;
}

void main()
{	
	//Final Light/Normal vector calculations
	
	vec4 diffTexColor;
	vec3 normal; //Fragment normal
	float lfRoughness = 1.0;
	
	if (mpCommonPerFrame.diffuseFlag > 0.0){
		//Load Textures
		float mipmaplevel = get_mipmap_level();
		diffTexColor = calcDiffuseColor(mipmaplevel);
		
		//Fetch mask texture attributes
		lfRoughness = calcRoughness(mipmaplevel);
		
		//Apply _F24_AOMAP
	 	if (mesh_has_matflag(_F24_AOMAP)) {
	 		float maskalpha = textureLod(mpCustomPerMaterial.gMasksMap, vec3(uv0, 0.0), mipmaplevel).r;
	 		diffTexColor.rgb *= maskalpha; //Is the r channel the ambient occlusion map?
	 	}

	 	//Try to load normal from NormalTexture as well
	 	normal = calcNormal(mipmaplevel);
	} else{
		diffTexColor = vec4(mpCommonPerMesh.color, 1.0);
		normal = N;
	}

	//Light properties
	vec3 lightColor = vec3(1.0, 1.0, 1.0);
	//vec3 lightColor = light.color;
	float ambientStrength = 0.001;
	float specularStrength = 0.0;

	float alpha;
	alpha = diffTexColor.a;
	
	//Mask Checks
	
	//Check _F11_ALPHACUTOUT
	if (mesh_has_matflag(_F11_ALPHACUTOUT)) {
		if (alpha <= 0.05) discard;
	}

	if (mesh_has_matflag(_F22_TRANSPARENT_SCALAR)) {
		// Transparency scalar comes from float in Material
        alpha *= mpCustomPerMaterial.gMaterialColourVec4.a;	
	}

	//Check _F9_TRANSPARENT
	if (mesh_has_matflag(_F09_TRANSPARENT) || mesh_has_matflag(_F22_TRANSPARENT_SCALAR)) {
		if (alpha <= 0.05) discard;
	}
	
	float lfMetallic = calcMetallic(alpha);

	//Accumulate colors

	//This is what I used before the new lighting system
	//diff += ambient * (0.5 + ((lfRoughness) * 0.5));
    //diff *= (1.0f * lightColor * bshininess + 1.0) * diffTexColor.rgb; //+ lSpecularColourVec3 * PhongApprox( lfRoughness, lfRoL );
    
    //New light system test

    //Ambient Component
	vec3 ambient = ambientStrength * lightColor;

	//Diffuse Component
	//vec3 lightPos = vec3(50, 50, 50); //Fix light position for now
	vec3 lightPos = mpCommonPerFrame.cameraPosition; //Use camera position as the light position
	vec3 lightDir = normalize(lightPos - fragPos.xyz);
	//vec3 lightDir = normalize(mpCommonPerFrame.cameraDirection);
	float l_distance = distance(lightPos, fragPos.xyz); //Calculate distance of 

	float diff_coeff = max(dot(normal, -lightDir), 0.0);
	vec3 diff = diff_coeff * diffTexColor.rgb * lightColor;
	//diff = (1.0 /(l_distance*l_distance)) * diff; //Quadratic attenuation

	//Specular Component
	vec3 viewDir = normalize(mpCommonPerFrame.cameraDirection);
	vec3 reflectDir = reflect(-lightDir, normal);

	float spec = pow(max(dot(-viewDir, reflectDir), 0.0), 32);
	vec3 specular = specularStrength * spec * lightColor;

	if (mpCommonPerFrame.use_lighting > 0.0){
		outcolors[0].rgb = (ambient + diff + specular);
	} else {
		outcolors[0].rgb = diffTexColor.rgb;	
		//outcolors[0].rgb = vec3(1.0, 0.0, 0.0);	
	}

	outcolors[0].rgb = fixColorGamma(outcolors[0].rgb);

	//Final output
    outcolors[0] = ApplySelectedColor(outcolors[0]);
	//gl_FragColor = vec4(N, 1.0);
    outcolors[1] = fragPos;
    //outcolors[1] = vec4(N, 1.0);
    outcolors[2] = vec4(0.0, 1.0, 0.0, 1.0);

	//fixColorGamma(diffTexColor.rgb);
}
