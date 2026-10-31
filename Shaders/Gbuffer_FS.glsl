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

//Key light shadow map
uniform sampler2DShadow shadowMap;
uniform mat4 shadowMat; //Rotated scene space to light clip space
uniform float shadowsEnabled;
uniform float shadowTexelSize; //World size of a shadow map texel

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

//Visibility of the key light at a world position: 1 lit, 0 shadowed
float calcKeyShadow(vec3 pos, vec3 N)
{
	if (shadowsEnabled < 0.5)
		return 1.0;

	//Normal offset: move the lookup off the surface so it does not shadow itself
	vec4 lightClip = shadowMat * vec4(pos + N * (1.5 * shadowTexelSize), 1.0);
	vec3 p = lightClip.xyz / lightClip.w * 0.5 + 0.5;
	if (any(lessThan(p, vec3(0.0))) || any(greaterThan(p, vec3(1.0))))
		return 1.0;

	//3x3 PCF, every tap is already a bilinear 2x2 comparison
	vec2 texel = 1.0 / vec2(textureSize(shadowMap, 0));
	float lit = 0.0;
	for (int y = -1; y <= 1; ++y)
		for (int x = -1; x <= 1; ++x)
			lit += texture(shadowMap, vec3(p.xy + vec2(x, y) * texel, p.z));
	return lit / 9.0;
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
	float lfGlow = fragParams.a; //Simple_FS writes glow intensity here
	float isLit = fragNormal.a;
	
	vec4 finalColor = vec4(0.0);

	//finalColor = mix(finalColor, albedoColor, lfGlow);
	vec4 ambient = vec4(vec3(0.03) * albedoColor.rgb, 0.0);

#ifdef _D_LIGHTING
	//Background: nothing was drawn at this pixel. Dark materials are not background.
	//Only the lighting pass has the depth texture bound; the final unlit pass receives the lit
	//result, where the background already holds the clear colour
	if (depthColor.r >= 1.0){
		fragColor = vec4(clearColor, 1.0);
		return;
	}
#endif

#ifdef _D_LIGHTING
	if ((mpCommonPerFrame.use_lighting > 0.0) && (isLit < 0.5)) {
		//Unlit materials keep their colour
		finalColor.rgb = albedoColor.rgb;
		finalColor.rgb += albedoColor.rgb * lfGlow;
		finalColor.a = albedoColor.a;
	} else if ((mpCommonPerFrame.use_lighting > 0.0)) {
		//A zero normal (degenerate vertex or normal map texel) would normalize to NaN
		vec3 lNormal = dot(fragNormal.xyz, fragNormal.xyz) > 1e-12 ? normalize(fragNormal.xyz) : vec3(0.0, 0.0, 1.0);

		//Viewport environment lighting. Light 0 is the default light and acts as the key light direction
		finalColor.rgb = calcEnvironmentLighting(mpCommonPerFrame.lights[0], fragPos, lNormal,
			mpCommonPerFrame.cameraPosition.xyz, albedoColor.rgb, lfMetallic, lfRoughness, ao,
			calcKeyShadow(fragPos.xyz, lNormal));

		//Scene lights, from the light clusters
		finalColor.rgb += calcSceneLighting(gl_FragCoord.xy, viewDepthFromWindowZ(depthColor.r, mpCommonPerFrame.projMatInv),
			fragPos, lNormal, mpCommonPerFrame.cameraPosition.xyz, mpCommonPerFrame.cameraDirection.xyz,
			albedoColor.rgb, lfMetallic, lfRoughness, ao);

		//Emissive, added before bloom so bright glow blooms
		finalColor.rgb += albedoColor.rgb * lfGlow;

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
