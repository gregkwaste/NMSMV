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
out vec4 outcolors[4];

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
			diffTexColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(uv0, mpCommonPerMesh.gUserDataVec4.w), mipmaplevel);
			//diffTexColor = vec4(1.0, 1.0, 1.0, 1.0);
		}
		else {
			diffTexColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(uv0, 0.0), mipmaplevel);
			//diffTexColor = vec4(1.0, 0.0, 0.0, 1.0);
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
	if (mesh_has_matflag(_F25_ROUGHNESS_MASK)) {
		lfRoughness = textureLod(mpCustomPerMaterial.gMasksMap, vec3(uv0, 0.0), mipmaplevel).g;
		lfRoughness = 1.0 - lfRoughness;
		lfRoughness *= mpCustomPerMaterial.gMaterialParamsVec4.x;
	} 
	return lfRoughness;
}

float calcAO(float mipmaplevel){
	float ao = 0.0;
	if (mesh_has_matflag(_F24_AOMAP)) {
 		ao = textureLod(mpCustomPerMaterial.gMasksMap, vec3(uv0, 0.0), mipmaplevel).r;
	}
	return ao;
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


//PBR Functions
float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a      = roughness*roughness;
    float a2     = a*a;
    float NdotH  = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;
	
    float num   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
	
    return num / denom;
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r*r) / 8.0;

    float num   = NdotV;
    float denom = NdotV * (1.0 - k) + k;
	
    return num / denom;
}
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2  = GeometrySchlickGGX(NdotV, roughness);
    float ggx1  = GeometrySchlickGGX(NdotL, roughness);
	
    return ggx1 * ggx2;
}


vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}  


float calcLightAttenuation(Light light, vec4 _fragPos){
	float attenuation = 0.0f;

	//General Configuration
	
	//New light system
	//float lfLightIntensity = sqrt(light.color.w);
	float lfLightIntensity = log(light.color.w) / log(10);
	
	vec3 lightPos = light.position.xyz; //Use camera position as the light position
	vec3 lightDir = normalize(_fragPos.xyz - lightPos);
	vec3 lPosToLight = lightPos - _fragPos.xyz;

	//vec3 lightDir = normalize(mpCommonPerFrame.cameraDirection);
	float l_distance = distance(lightPos, _fragPos.xyz); //Calculate distance of 
	float lfDistanceSquared = dot(lPosToLight, lPosToLight); //Distance to light squared

	float lfFalloffType = light.falloff;
    float lfCutOff = 0.05;

    //Calculate attenuation
    if (lfFalloffType < 1.0)
    {
        // Quadratic Distance attenuation
        attenuation = lfLightIntensity / max(1.0, lfDistanceSquared);
    } else if (lfFalloffType < 2.0) {
		//Constant
		attenuation = lfLightIntensity;
    }
    else if (lfFalloffType < 3.0)
    {
        // Linear Distance attenuation
        attenuation = inversesqrt(lfDistanceSquared);
        attenuation = min( attenuation, 1.0 );
        attenuation *= lfLightIntensity;
    }

    // Conelight falloff (this can only attenuate down)
    if (length(light.direction.xyz) > 0.0001){
    	float lfLightFOV = cos(light.direction.w / 2.0);
    	float lfConeAngle = dot( -normalize(light.direction.xyz), lightDir);
    	if (lfConeAngle -  lfLightFOV > lfCutOff) {
    		return 0.0;
		}	
    }
	
    return attenuation;
}



vec3 calcColor(Light light, vec4 _fragPos, vec4 _fragColor, vec3 _fragNormal){
	//General Configuration
	float ambientStrength = 0.0001;
	float specularStrength = 0.05;
	float lightIntensityMult = 0.0001;

	//Displace the fragment based on the normal
	vec3 eff_fragpos = _fragPos.xyz + 0.05 * _fragNormal;
	
	//New light system
	float lfLightIntensity = light.color.w * lightIntensityMult;
	vec3 light_color = light.color.xyz;
	

	//Ambient Component
	vec3 ambient = ambientStrength * light_color;

	//Diffuse Component
	//vec3 lightPos = vec3(1.5, 3, -1.5); //Fix light position for now
	vec3 lightPos = light.position.xyz; //Use camera position as the light position
	vec3 lightDir = normalize(eff_fragpos - lightPos);
	vec3 lPosToLight = lightPos - eff_fragpos;

	//vec3 lightDir = normalize(mpCommonPerFrame.cameraDirection);
	float l_distance = distance(lightPos, _fragPos.xyz); //Calculate distance of 
	float lfDistanceSquared = dot(lPosToLight, lPosToLight); //Distance to light squared

	float diff_coeff = max(dot(_fragNormal, -lightDir), 0.0);
	vec3 diff = (diff_coeff * _fragColor.rgb) * light_color;
	
	//Calculate Light attenuation
	float attenuation = calcLightAttenuation(light, _fragPos);
	//Lighting stuff

	
    
    //Specular Component
	vec3 viewDir = normalize(mpCommonPerFrame.cameraDirection);
	vec3 reflectDir = reflect(-lightDir, _fragNormal);

	float spec = pow(max(dot(viewDir, reflectDir), 0.0), 16);
	vec3 specular = specularStrength * spec * light_color;

	return attenuation * (ambient + diff + specular);
	//return vec3(attenuation);
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


vec3 default_lighting()
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
	} else {
		diffTexColor = vec4(mpCommonPerMesh.color, 1.0);
		normal = N;
	}

	float alpha;
	alpha = diffTexColor.a;
	
	//Mask Checks
	
	if (mesh_has_matflag(_F22_TRANSPARENT_SCALAR)) {
		// Transparency scalar comes from float in Material
        alpha *= mpCustomPerMaterial.gMaterialColourVec4.a;	
	}

	//Check _F9_TRANSPARENT
	if (mesh_has_matflag(_F11_ALPHACUTOUT)) {
		if (alpha < 0.45) discard;
	}

	if (mesh_has_matflag(_F09_TRANSPARENT) || mesh_has_matflag(_F22_TRANSPARENT_SCALAR)) {
		if (alpha < 0.01) discard;
	}

	
	float lfMetallic = calcMetallic(alpha);

	//Accumulate colors

	//This is what I used before the new lighting system
	//diff += ambient * (0.5 + ((lfRoughness) * 0.5));
    //diff *= (1.0f * lightColor * bshininess + 1.0) * diffTexColor.rgb; //+ lSpecularColourVec3 * PhongApprox( lfRoughness, lfRoL );
    
	//Apply strength to normal
    normal.xy = 1.2 * normal.xy;
    normal = normalize(normal);


    vec3 finalColor = vec3(0.0, 0.0, 0.0);
    if (mpCommonPerFrame.use_lighting > 0.0){
		for (int i=0;i<mpCommonPerFrame.light_count;i++){
			//outcolors[0].rgb = (ambient + diff + specular);	
			if (mpCommonPerFrame.lights[i].position.w > 0.0f){ //Check if renderable
				finalColor += calcColor(mpCommonPerFrame.lights[i], fragPos, diffTexColor, normal);
			}
		}
	} else {
		finalColor = diffTexColor.rgb;
		//outcolors[0].rgb = vec3(1.0, 0.0, 0.0);
	}

	return finalColor;

}

void pbr_lighting(){

	//Final Light/Normal vector calculations
	vec4 diffTexColor;
	vec3 normal; //Fragment normal
	float lfRoughness = 1.0;
	float ao = 0.0;
	
	if (mpCommonPerFrame.diffuseFlag > 0.0){
		float mipmaplevel = get_mipmap_level();
		
		//Fetch albedo
		diffTexColor = calcDiffuseColor(mipmaplevel);
		
		//Fetch roughness value
		lfRoughness = calcRoughness(mipmaplevel);
		
		//Fetch AO value
	 	ao = calcAO(mipmaplevel);

	 	//Try to load normal from NormalTexture as well
	 	normal = calcNormal(mipmaplevel);
	} else {
		diffTexColor = vec4(mpCommonPerMesh.color, 1.0);
		normal = N;
	}

	float alpha;
	alpha = diffTexColor.a;
	
	//Mask Checks
	if (mesh_has_matflag(_F22_TRANSPARENT_SCALAR)) {
		// Transparency scalar comes from float in Material
        alpha *= mpCustomPerMaterial.gMaterialColourVec4.a;	
	}

	//Check _F9_TRANSPARENT
	if (mesh_has_matflag(_F11_ALPHACUTOUT)) {
		if (alpha < 0.45) discard;
	}

	if (mesh_has_matflag(_F09_TRANSPARENT) || mesh_has_matflag(_F22_TRANSPARENT_SCALAR)) {
		if (alpha < 0.01) discard;
	}

	float lfMetallic = calcMetallic(alpha);

	if (!mesh_has_matflag(_F09_TRANSPARENT) && !mesh_has_matflag(_F22_TRANSPARENT_SCALAR) && !mesh_has_matflag(_F11_ALPHACUTOUT)) {
		alpha = 1.0;
	}

	//Get Glow
	float lfGlow = 0.0;
	float lLowAlpha = 1.0; //TODO : Find out what exactly is that shit
	if (mesh_has_matflag(_F35_GLOW_MASK) && !mesh_has_matflag( _F09_TRANSPARENT )){
		lfGlow = mpCustomPerMaterial.gCustomParams01Vec4.y * lLowAlpha;
	} else if (mesh_has_matflag( _F34_GLOW)) {
		lfGlow = mpCustomPerMaterial.gCustomParams01Vec4.y;
	}
    
	vec3 F0 = vec3(0.04); 
    F0 = mix(F0, diffTexColor.rgb, lfMetallic);

    vec3 viewDir = -normalize(mpCommonPerFrame.cameraDirection);

    //ao = 1.0;
    //return vec3(lfRoughness, 0.0, 0.0);

	// reflectance equation
    vec3 finalColor = vec3(0.0);
    
    if (mesh_has_matflag(_F07_UNLIT)){
    	finalColor = diffTexColor.rgb;
    } else {
    	for(int i = 0; i < mpCommonPerFrame.light_count; ++i) 
	    {
	    	// calculate per-light radiance
	        Light light = mpCommonPerFrame.lights[i]; 

			if (light.position.w < 1.0)
	        	continue;
	        
	        vec3 L;
	        float attenuation;
	        //Explicitly handle Sun
	        if (i == 0) {
	        	L = normalize(light.position.xyz);	
				attenuation = 1.0;
			} else {
	        	L = normalize(light.position.xyz - fragPos.xyz);	
	        	attenuation = calcLightAttenuation(light, fragPos);
	        	//float attenuation = 1.0 / (distance * distance); //Default calculation
	    	}
			
			vec3 radiance = light.color.xyz * light.color.w * 0.0001 * attenuation;
			vec3 H = normalize(viewDir + L);
	        //float distance    = length(light.position.xyz - fragPos.xyz);
	        
			// cook-torrance brdf
	        float NDF = DistributionGGX(N, H, lfRoughness);        
	        float G   = GeometrySmith(N, viewDir, L, lfRoughness);      
	        vec3 F    = fresnelSchlick(max(dot(H, viewDir), 0.0), F0);       
	        
	        vec3 kS = F;
	        vec3 kD = vec3(1.0) - kS;
	        kD *= 1.0 - lfMetallic;	  
	        
	        vec3 numerator    = NDF * G * F;
	        float denominator = 4.0 * max(dot(N, viewDir), 0.0) * max(dot(N, L), 0.0);
	        vec3 specular     = numerator / max(denominator, 0.001);  
	            
	        // add to outgoing radiance finalColor
	        float NdotL = max(dot(N, L), 0.0);                
	        finalColor += (kD * diffTexColor.rgb / PI + specular) * radiance * NdotL; 
	    }  
    }

     
  
    vec3 ambient = vec3(0.03) * diffTexColor.rgb * ao;
    finalColor = ambient + finalColor;

    //Save Info
    //Albedo
	outcolors[0] = vec4(fixColorGamma(finalColor), alpha);
	//outcolors[0].rgb = vec3(lfGlow, lfGlow, 0);
	//outcolors[0].rgb = lfGlow * mpCustomPerMaterial.gMaterialColourVec4.xyz;
	//Positions
	outcolors[1].rgb = fragPos.xyz;	
	//Normals
	outcolors[2].rgb = normal;	
	//Bloom
	outcolors[3] = vec4(lfGlow * mpCustomPerMaterial.gMaterialColourVec4.xyz, alpha);

}


void main(){

	//use default lighting system
	//outcolors[0].rgb = default_lighting();
	//use PBR
	pbr_lighting();
}