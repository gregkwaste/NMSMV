#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable

//Includes
#include "/common.glsl"
#include "/common_structs.glsl"

//Diffuse Textures
uniform sampler2DMS diffuseTex;
uniform sampler2DMS positionTex;
uniform sampler2DMS normalTex;
uniform sampler2DMS depthTex;
uniform sampler2DMS bloomTex;
uniform sampler2DMS parameterTex;

uniform mat4 mvp;
in vec2 uv0;


uniform CommonPerFrameSamplers mpCommonPerFrameSamplers;

//Uniform Blocks
layout (std140) uniform Uniforms
{
    CommonPerFrameUniforms mpCommonPerFrame;
    CommonPerMeshUniforms mpCommonPerMesh;
};


vec4 worldfromDepth()
{
	vec4 world;
	//world.xy = clipPos.xy / clipPos.w; //tick
	vec2 depth_uv = uv0;
	//depth_uv.y = 1.0 - depth_uv.y;
	//world.z = 0.5 * texture2D(depthTex, depth_uv).r + 1.0; //wrong
	//world.z += 0.5;
	float zNear = 2.0;
	float zFar = 1000.0;
	
	vec4 texelValue = texelFetch(depthTex, ivec2(gl_FragCoord.xy), 0);
	world.z = 2.0 * texelValue.z;
	//world.z = 2.0 * zNear * zFar / (zFar + zNear - world.z * (zFar - zNear));
	
	world.xy = 2.0 * uv0 - 1.0;
	//world.xy = uv0;
	//world.z = 2.0 * texture2D(depthTex, depth_uv).r - 1.0;
	world.w = 1.0f;

	//world = vec4(world.xyz / world.w, world.w);

	//My way
	world = inverse(mvp) * world;
	world.xyz /= world.w;

	return world;
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
	float lfLightIntensity = log(light.color.w) / log(10.0);
	
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


void main()
{
	//sample our texture
	vec4 albedoColor = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 fragPos = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 fragNormal = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 bloomColor = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 depthColor = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 fragParams = vec4(0.0, 0.0, 0.0, 0.0);
	
	//Gather MS
	for (int i=0; i<8; i++){
		albedoColor   += texelFetch(diffuseTex, ivec2(gl_FragCoord.xy), i);	
		depthColor += texelFetch(depthTex, ivec2(gl_FragCoord.xy), i);
		fragPos    += texelFetch(positionTex, ivec2(gl_FragCoord.xy), i);	
		fragNormal += texelFetch(normalTex, ivec2(gl_FragCoord.xy), i);	
		fragParams += texelFetch(parameterTex, ivec2(gl_FragCoord.xy), i);	
		bloomColor += texelFetch(bloomTex, ivec2(gl_FragCoord.xy), i);	
	}
	
	//Normalize Values
	albedoColor = 0.125 * albedoColor;
	fragPos = 0.125 * fragPos;
	fragNormal = 0.125 * fragNormal;
	fragParams = 0.125 * fragParams;
	depthColor = 0.125 * depthColor;
	bloomColor = 0.125 * bloomColor;

	vec3 clearColor = vec3(0.13, 0.13, 0.13);
	vec3 mixColor = (albedoColor + bloomColor).rgb;
	
	//Old color + bloom
	//gl_FragColor = vec4(mix(clearColor, mixColor, texColor.a), 1.0);

	//Load Frag Info
	float ao = fragParams.x;
	float lfMetallic = fragParams.y;
	float lfRoughness = fragParams.z;

	vec3 F0 = vec3(0.04); 
	vec3 N = fragNormal.xyz;
    F0 = mix(F0, albedoColor.rgb, lfMetallic);

    vec3 viewDir = -normalize(mpCommonPerFrame.cameraDirection);

    //ao = 1.0;
	vec3 ambient = vec3(0.03) * albedoColor.rgb * ao;
	//return vec3(lfRoughness, 0.0, 0.0);

	vec3 finalColor = vec3(0.0);
    
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
        finalColor += (kD * albedoColor.rgb / PI + specular) * radiance * NdotL; 
    }  
    

    //Apply Gamma Correction
    finalColor = fixColorGamma(ambient + finalColor + bloomColor.rgb);

    gl_FragColor = vec4(mix(clearColor, finalColor, albedoColor.a), 1.0);
	
	//gl_FragColor = vec4(texture2D(depthTex, uv0).rrr, 1.0);
	//vec4 world = worldfromDepth();

	//Transform from clip to view space
	//world = inverse(mvp) * world;
	//Fix w
	//world /= world.w;
	
	//gl_FragColor = texture2D(positionTex, uv0) - world;
	//gl_FragColor = vec4(texture2D(positionTex, uv0).rgb, 1.0) - vec4(world.xyz, 1.0);
	//gl_FragColor = vec4(worldfromDepth().zzz, 1.0);
	//gl_FragColor = vec4(1.0, 0.0, 1.0, 1.0);
}
