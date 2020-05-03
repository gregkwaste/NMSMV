/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */
 
//Includes
#include "/common.glsl"
#include "/common_structs.glsl"
#include "/common_lighting.glsl"


//Diffuse Textures
uniform sampler2DMS albedoTex;
uniform sampler2DMS positionTex;
uniform sampler2DMS normalTex;
uniform sampler2DMS depthTex;
uniform sampler2DMS parameterTex;
uniform sampler2DMS parameter2Tex;

uniform mat4 mvp;
in vec2 uv0;


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


vec4 worldfromDepth(out float depth)
{
	vec4 world;
	world.xy = gl_FragCoord.xy;
	
	vec2 depth_uv = 0.5 * world.xy + 0.5; //Convert to (0:1) range
	

	depth = 0.0;
	for (int i=0; i<8; i++){
		depth += texelFetch(depthTex, ivec2(depth_uv), i).x;
	}

	depth *= 0.125;
	
	//Fetch the value from the depth Texture (0:1) and convert it back to (-1:1)
	world.xy = gl_FragCoord.xy;
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
	//sample our texture
	vec4 albedoColor = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 fragPos = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 fragNormal = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 bloomColor = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 depthColor = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 fragParams = vec4(0.0, 0.0, 0.0, 0.0);
	vec4 fragParams2 = vec4(0.0, 0.0, 0.0, 0.0);
	
	
	//Gather MS
	for (int i=0; i< int(mpCommonPerFrame.MSAA_SAMPLES); i++){
		albedoColor += texelFetch(albedoTex, ivec2(gl_FragCoord.xy), i);	
		depthColor += texelFetch(depthTex, ivec2(gl_FragCoord.xy), i);
		fragPos    += texelFetch(positionTex, ivec2(gl_FragCoord.xy), i);	
		fragNormal += texelFetch(normalTex, ivec2(gl_FragCoord.xy), i);	
		fragParams += texelFetch(parameterTex, ivec2(gl_FragCoord.xy), i);
		fragParams2 += texelFetch(parameter2Tex, ivec2(gl_FragCoord.xy), i);
	}

	float ratio = 1.0 / int(mpCommonPerFrame.MSAA_SAMPLES);
	
	//Normalize Values
	albedoColor = ratio * albedoColor;
	fragPos = ratio * fragPos;
	fragNormal = ratio * fragNormal;
	depthColor = ratio * depthColor;
	bloomColor = ratio * bloomColor;
	fragParams = ratio * fragParams;
	fragParams2 = ratio * fragParams2;

	vec3 clearColor = vec3(0.13, 0.13, 0.13);
	
	//get back from sRGB
	albedoColor.rgb = pow(albedoColor.rgb, vec3(2.2));

	//Old color + bloom
	//gl_FragColor = vec4(mix(clearColor, mixColor, texColor.a), 1.0);

	//Load Frag Info
	float ao = fragParams.x;
	float lfMetallic = fragParams.y;
	float lfRoughness = fragParams.z;
	float lfGlow = fragParams.a;
	float isLit = fragParams2.x;

	vec3 finalColor = vec3(0.0);
	if ((mpCommonPerFrame.use_lighting > 0.0) && (isLit > 0.0)){
		
		for(int i = 0; i < mpCommonPerFrame.light_count; ++i) 
	    {
	    	// calculate per-light radiance
	        Light light = mpCommonPerFrame.lights[i]; 

			if (light.position.w < 1.0)
	        	continue;
	    	
	        finalColor += calcLighting(light, fragPos, fragNormal.xyz, mpCommonPerFrame.cameraPosition,
	            albedoColor.rgb, lfMetallic, lfRoughness, ao);
		}  

		vec3 ambient = vec3(0.03) * albedoColor.rgb * ao;
    	finalColor = ambient + finalColor;
	
	} else {
		finalColor = albedoColor.rgb;
	}

	
	//TODO: Add glow depending on the material parameters cached in the gbuffer (normalmap.a) if necessary
	
	//Tone Mapping
	finalColor = finalColor / (finalColor + vec3(1.0));
	
	//Apply Gamma Correction
    finalColor = fixColorGamma(finalColor);

    //gl_FragColor = vec4(albedoColor.rgb, 1.0);
	gl_FragColor = vec4(mix(clearColor, finalColor, albedoColor.a), 1.0);
	//gl_FragColor = fragNormal;
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
