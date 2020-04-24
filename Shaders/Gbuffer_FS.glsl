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
	for (int i=0; i<8; i++){
		albedoColor += texelFetch(albedoTex, ivec2(gl_FragCoord.xy), i);	
		depthColor += texelFetch(depthTex, ivec2(gl_FragCoord.xy), i);
		fragPos    += texelFetch(positionTex, ivec2(gl_FragCoord.xy), i);	
		fragNormal += texelFetch(normalTex, ivec2(gl_FragCoord.xy), i);	
		fragParams += texelFetch(parameterTex, ivec2(gl_FragCoord.xy), i);
		fragParams2 += texelFetch(parameter2Tex, ivec2(gl_FragCoord.xy), i);
	}
	
	//Normalize Values
	albedoColor = 0.125 * albedoColor;
	fragPos = 0.125 * fragPos;
	fragNormal = 0.125 * fragNormal;
	depthColor = 0.125 * depthColor;
	bloomColor = 0.125 * bloomColor;
	fragParams = 0.125 * fragParams;
	fragParams2 = 0.125 * fragParams2;

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
	    	
	        int isDirectional = 0;

	        finalColor += calcLighting(light, fragPos, fragNormal.xyz, mpCommonPerFrame.cameraPosition,
	            albedoColor.rgb, lfMetallic, lfRoughness, ao, isDirectional);
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
