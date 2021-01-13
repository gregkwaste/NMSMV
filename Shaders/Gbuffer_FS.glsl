/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */
 
//Includes
#include "/common.glsl"
#include "/common_structs.glsl"
#include "/common_lighting.glsl"


//Diffuse Textures
uniform sampler2D albedoTex;
uniform sampler2D normalTex;
uniform sampler2D depthTex;
uniform sampler2D parameterTex;

uniform mat4 mvp;
in vec2 uv0;
out vec4 fragColor;


uniform CommonPerFrameSamplers mpCommonPerFrameSamplers;

//Uniform Blocks
layout (std140, binding=0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};


vec4 worldfromDepth(in vec2 screen, in float depth)
{
	vec4 world;
	
	float n = mpCommonPerFrame.cameraNearPlane;
	float f = mpCommonPerFrame.cameraFarPlane;

	//Convert depth back to (-1:1)
	world.xy = 2.0 * screen - 1.0;
	world.z = 2.0 * depth - 1.0; 
	world.w = 1.0f;

	world = mpCommonPerFrame.projMatInv * world;
	world /= world.w;
	world = mpCommonPerFrame.lookMatInv * world;
	//world /= world.w;

	return world;
}

// Converts post-projection z/w to linear z
float LinearDepth(float perspectiveDepth)
{
	float n = mpCommonPerFrame.cameraNearPlane;
	float f = mpCommonPerFrame.cameraFarPlane;

	float ProjectionA = f / (f - n);
    float ProjectionB = (-f * n) / (f - n);
	
	return ProjectionB / (perspectiveDepth - ProjectionA);
}


void main()
{
	//sample our texture
	vec4 bloomColor = vec4(0.0, 0.0, 0.0, 0.0);
	vec3 clearColor = vec3(0.13, 0.13, 0.13);
	vec2 uv = gl_FragCoord.xy / (1.0 * mpCommonPerFrame.frameDim);
    
	//vec4 albedoColor = texelFetch(albedoTex, ivec2(gl_FragCoord.xy), 0);	
	vec4 albedoColor = texture(albedoTex, uv);	
	//vec4 fragNormal = texelFetch(normalTex, ivec2(gl_FragCoord.xy), 0);	
	vec4 fragNormal = texture(normalTex, uv);
	//vec4 fragParams = texelFetch(parameterTex, ivec2(gl_FragCoord.xy), 0);
	vec4 fragParams = texture(parameterTex, uv);
	//vec4 depthColor = texelFetch(depthTex, ivec2(gl_FragCoord.xy), 0);	
	vec4 depthColor = texture(depthTex, uv);
	
	//Resolve Colors

	//get back from sRGB
	//albedoColor.rgb = pow(albedoColor.rgb, vec3(2.2));

	//Old color + bloom
	//gl_FragColor = vec4(mix(clearColor, mixColor, texColor.a), 1.0);

	//Calculate fragment Position from depth
	vec4 fragPos = worldfromDepth(uv, depthColor.r);

	//Load Frag Info
	float ao = fragParams.x;
	float lfMetallic = fragParams.y;
	float lfRoughness = fragParams.z;
	float lfSubsurface = fragParams.a;
	float isLit = fragNormal.a;
	
	vec4 finalColor = vec4(0.0);

	//finalColor = mix(finalColor, albedoColor, lfGlow);
	vec4 ambient = vec4(vec3(0.03) * albedoColor.rgb, 0.0);

	if (dot(albedoColor, vec4(1.0)) < 1e-3){
		fragColor = vec4(clearColor, 1.0);
		return;
	}

#ifdef _D_LIGHTING
	finalColor.rgb = ambient.rgb;
	if ((mpCommonPerFrame.use_lighting > 0.0)) {
		for(int i = 0; i < mpCommonPerFrame.light_count; ++i) 
		{
	    	// calculate per-light radiance
	        Light light = mpCommonPerFrame.lights[i]; 

			if (light.position.w < 1.0)
	        	continue;

        	finalColor.rgb += calcLighting(light, fragPos, fragNormal.xyz, 
			mpCommonPerFrame.cameraPosition.xyz, mpCommonPerFrame.cameraDirection.xyz, albedoColor.rgb, lfMetallic, lfRoughness, ao);
		}

		finalColor.a = albedoColor.a;
	} else {
		finalColor = albedoColor;
	}
#else
	finalColor = albedoColor;
#endif
	
	//Add ambient lighting
	//finalColor += ambient;

	//vec3 lumcoeff = vec3(0.299,0.587,0.114);
    
	//TODO: Add glow depending on the material parameters cached in the gbuffer (normalmap.a) if necessary
	
	//fragColor = vec4(albedoColor.rgb, 1.0);
	//fragColor = vec4(mix(clearColor, finalColor.rgb, albedoColor.a), 1.0);
	fragColor = finalColor;
	//fragColor = vec4(fragPos.rgb, 1.0);
	//fragColor = vec4(clearColor, 1.0);
	//fragColor = fragNormal;
	//fragColor = vec4(texture2D(depthTex, uv0).rrr, 1.0);
	
	//Transform from clip to view space
	//world = inverse(mvp) * world;
	//Fix w
	//world /= world.w;
	
	//fragColor = texture2D(positionTex, uv0) - world;
	//fragColor = vec4(texture2D(positionTex, uv0).rgb, 1.0) - vec4(world.xyz, 1.0);
	//fragColor = vec4(worldfromDepth().zzz, 1.0);
	//fragColor = vec4(1.0, 0.0, 1.0, 1.0);
}
