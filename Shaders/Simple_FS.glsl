/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */


//Extra preprocessor directives
#if defined( __F25_MASKS_MAP ) || defined( __F42_DETAIL_NORMAL ) || defined( __F22_OCCLUSION_MAP )
    #define D_MASKS
#endif

#if defined( __F01_DIFFUSEMAP ) || defined( __F03_NORMALMAP ) || defined( D_IMPOSTERMASKS ) || defined(D_MASKS) || defined(__F53_COLOURISABLE)
    #define D_TEXCOORDS
#endif

#if defined( __F42_DETAIL_NORMAL )
    #define D_DETAIL
#endif

 
//Includes
#include "/common.glsl"
#include "/common_structs.glsl"
#include "/common_lighting.glsl"


//TODO: Do some queries internally and figure out the exact locations of the uniforms
uniform CustomPerMaterialUniforms mpCustomPerMaterial;
uniform CommonPerFrameSamplers mpCommonPerFrameSamplers;

#if defined(__F51_DECAL_DIFFUSE) || defined(__F52_DECAL_NORMAL)
uniform vec3 decalAabbMin;
uniform vec3 decalAabbMax;
#endif

//Uniform Blocks
layout (std140, binding=0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};

layout (std430, binding=1) buffer _COMMON_PER_MESH
{
    vec3 color; //Mesh Default Color
    float skinned;
    MeshInstance instanceData[]; //Instance world matrices, normal matrices, occlusion and selection status
};

in vec4 fragPos;
in vec4 screenPos;
in vec4 vertColor;
in vec3 mTangentSpaceNormalVec3;
in vec4 uv;
in mat3 TBN;
flat in int instanceId;
in float isSelected;

//Deferred Shading outputs
out vec4 outcolors[3];


#ifdef __F62_DETAIL_ALPHACUTOUT
	const float kfAlphaThreshold = 0.1;
	const float kfAlphaThresholdMax = 0.5;
#elif defined (__F11_ALPHACUTOUT)
	//kfAlphaThreshold = 0.45; OLD
	//kfAlphaThresholdMax = 0.8;
	const float kfAlphaThreshold = 0.45;
	const float kfAlphaThresholdMax = 0.8;
#else
	const float kfAlphaThreshold = 0.0001;
#endif


//New Decoding function - RGTC
//Two channel normal maps (BC5/RGTC) only store x and y, so z is rebuilt from them.
//The b and a channels hold no normal data and must not take part in the normalization
vec3 DecodeNormalMap(vec4 lNormalTexVec4 ){
    vec2 lXY = 2.0 * lNormalTexVec4.rg - 1.0;
    return normalize( vec3( lXY, sqrt( max( 1.0 - dot( lXY, lXY ), 0.0 ) ) ) );
}

float
mip_map_level(in vec2 texture_coordinate)
{
    // The OpenGL Graphics System: A Specification 4.2
    //  - chapter 3.9.11, equation 3.21

    vec2  dx_vtc        = dFdx(texture_coordinate);
    vec2  dy_vtc        = dFdy(texture_coordinate);
    float delta_max_sqr = max(dot(dx_vtc, dx_vtc), dot(dy_vtc, dy_vtc));

    //return max(0.0, 0.5 * log2(delta_max_sqr) - 1.0); // == log2(sqrt(delta_max_sqr));
    return 0.5 * log2(delta_max_sqr); // == log2(sqrt(delta_max_sqr));
}

void clip(float test) { if (test < 0.0) discard; }


vec4 worldfromDepth()
{
	vec4 world;
	
	//PARTIALLY WORKING
	vec2 depth_uv = gl_FragCoord.xy / (1.0 * mpCommonPerFrame.frameDim);
	
	//Fetch depth
	float depth = texture(mpCommonPerFrameSamplers.depthMap, depth_uv).x; 
	//The opaque pass leaves the background at the far plane. It has no receiver surface,
	//so allowing it through would project the decal onto arbitrary far-plane positions.
	if (depth >= 0.999999)
		discard;

	//Convert depth back to (-1:1)
	world.xy = 2.0 * depth_uv - 1.0;
	world.z = 2.0 * depth - 1.0; 
	world.w = 1.0f;

	world = mpCommonPerFrame.projMatInv * world;
	world /= world.w;
	world = mpCommonPerFrame.lookMatInv * world;
	world /= world.w;
	
	return world;
}


//Green highlight for the mesh selected in the scene tree (keeps some of the texture detail)
vec4 ApplySelectedColor(vec4 color){
	vec4 new_col = color;
	if (isSelected > 0.0)
		new_col.rgb = mix(new_col.rgb, vec3(0.1, 0.9, 0.1), 0.5);
	return new_col;
}

float calcShadow(vec4 _fragPos, Light light){
	// get vector between fragment position and light position
	vec3 fragToLight = (_fragPos - light.position).xyz;
	
	// use the light to fragment vector to sample from the depth map 
	float closestDepth = texture(mpCommonPerFrameSamplers.shadowMap, fragToLight).r; 
	
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
	vec4 lColourVec4;
	float diffTex2Factor = 1.0;
	vec3 lNormalVec3 = mTangentSpaceNormalVec3;
	float lLowAlpha = 1.0; //Diffuse texture alpha, used as the glow mask on opaque materials
	float lHighAlpha = 1.0; //TODO : Find out what exactly is that shit
	vec4 world = fragPos;
	float isLit = 1.0;
	float lfRoughness = 1.0;
	float lfMetallic = 0.0;
	float lfSubsurface = 0.0; //Not used atm. Its G-buffer slot (params.a) carries glow instead
	float ao = 1.0;
	float lfGlow = 0.0;

	#if defined(__F07_UNLIT)
		isLit = 0.0;
	#endif

	#ifdef __F55_MULTITEXTURE
    	float lfMultiTextureIndex = instanceData[instanceId].gUserDataVec4.w;
	#endif

    #ifdef D_TEXCOORDS
    	
    	vec4 lTexCoordsVec4;
        lTexCoordsVec4 = uv;

        //Decal stuff
		#if defined(__F51_DECAL_DIFFUSE) || defined(__F52_DECAL_NORMAL)
        {
            world = worldfromDepth();
			//Convert vertex to the local space of the box

			//Depth reconstruction returns the rotated scene space used for rendering. Instance
			//matrices are stored in the unrotated scene space, so undo the global scene rotation
			//before entering the projector's local space.
			vec4 localPos = instanceData[instanceId].worldMatInv * mpCommonPerFrame.rotMatInv * world;
			localPos /= localPos.w;

			//Use the imported projector bounds. Most game projectors are unit cubes, but some
			//carry deeper custom volumes and must not be truncated to hardcoded +/-0.5 limits.
			vec3 decalSize = max(decalAabbMax - decalAabbMin, vec3(1e-6));
			vec3 decalBias = 0.01 * decalSize;
			clip(localPos.x - decalAabbMin.x - decalBias.x);
			clip(localPos.y - decalAabbMin.y - decalBias.y);
			clip(localPos.z - decalAabbMin.z - decalBias.z);
			clip(decalAabbMax.x - decalBias.x - localPos.x);
			clip(decalAabbMax.y - decalBias.y - localPos.y);
			clip(decalAabbMax.z - decalBias.z - localPos.z);

			lTexCoordsVec4.xy = (localPos.xy - decalAabbMin.xy) / decalSize.xy;
			lTexCoordsVec4.y *= -1; //Flip on Y axis
        }
        #endif

	    #if defined( D_IMPOSTER_COLOUR ) && defined( __F12_BATCHED_BILLBOARD )
	        lTexCoordsVec4.y = 1.0 - lTexCoordsVec4.y;
	    #endif

    #endif


	//Decal stuff
	#if defined(__F51_DECAL_DIFFUSE) || defined(__F52_DECAL_NORMAL)
		
		
	#endif

	
	if (mpCommonPerFrame.diffuseFlag > 0.0){
		float mipmaplevel = mip_map_level(uv.xy);
		
		#ifdef __F01_DIFFUSEMAP
			#ifdef __F55_MULTITEXTURE
				lColourVec4 = texture(mpCustomPerMaterial.gDiffuseMap, vec3(lTexCoordsVec4.xy, lfMultiTextureIndex));
			#else
				lColourVec4 = texture(mpCustomPerMaterial.gDiffuseMap, vec3(lTexCoordsVec4.xy, 0.0));
			#endif

			lLowAlpha = lColourVec4.a;
			#if !defined(__F09_TRANSPARENT) && !defined(__F11_ALPHACUTOUT) && \
			    !defined(__F51_DECAL_DIFFUSE) && !defined(__F52_DECAL_NORMAL)
				lColourVec4.a = 1.0;
			#endif
		#else
			lColourVec4 = mpCustomPerMaterial.gMaterialColourVec4;
		#endif

		#ifdef D_MASKS
	    	#ifdef __F55_MULTITEXTURE
	            vec4 lMasks = texture(mpCustomPerMaterial.gMasksMap, vec3(lTexCoordsVec4.xy, lfMultiTextureIndex));
	        #else
	            vec4 lMasks = texture(mpCustomPerMaterial.gMasksMap, vec3(lTexCoordsVec4.xy, 0.0));
	        #endif
	    #endif

	    #ifdef __F16_DIFFUSE2MAP
			vec4 lDiffuse2Vec4 = texture(mpCustomPerMaterial.gDiffuse2Map, vec3(lTexCoordsVec4.zw, 0.0));
			diffTex2Factor = lDiffuse2Vec4.a;
			lColourVec4.rgb = mix( lColourVec4.rgb, lDiffuse2Vec4.rgb, lDiffuse2Vec4.a );
		#endif

		#ifdef __F53_COLOURISABLE
		{
			vec4 colouriseMask = texture(mpCustomPerMaterial.gColouriseMaskMap,
				vec3(lTexCoordsVec4.xy, 0.0));
			float weight = dot(colouriseMask, vec4(1.0));
			if (weight > 1e-5)
			{
				vec3 tint = (instanceData[instanceId].colourisePrimary.rgb * colouriseMask.r +
				             instanceData[instanceId].colouriseSecondary.rgb * colouriseMask.g +
				             instanceData[instanceId].colouriseTernary.rgb * colouriseMask.b +
				             instanceData[instanceId].colouriseQuaternary.rgb * colouriseMask.a) / weight;
				lColourVec4.rgb = mix(lColourVec4.rgb, lColourVec4.rgb * tint,
				                       clamp(weight, 0.0, 1.0));
			}
		}
		#endif

		//Viewer helpers (locator crosses) take their colour from the vertices
		#ifdef __D_VERTEX_COLOUR
			lColourVec4 *= vertColor;
		#endif

		//_F21_VERTEXCUSTOM (formerly VERTEXCOLOUR) marks custom per-vertex data, not a colour: the bytes are
		//small raw values (normalised they turn everything black, raw they blow colours out and make
		//translucent materials opaque). Until its meaning is known it is not applied to the colour
		
		//TRANSPARENCY
		// Transparency scalar comes from float in Material
		lColourVec4.a *= mpCustomPerMaterial.gMaterialColourVec4.a;

		//Decals historically relied on texture coverage even without the ordinary transparent
		//or cutout flags. Discard empty coverage instead of replacing the G-buffer with a box.
		#if defined(__F51_DECAL_DIFFUSE) || defined(__F52_DECAL_NORMAL)
			if (lColourVec4.a < 0.001)
				discard;
		#endif
	    
		// Discard fully transparent pixels
	    #if defined( __F09_TRANSPARENT ) || defined( __F11_ALPHACUTOUT )
	    {
	        if ( lColourVec4.a < kfAlphaThreshold )
	        	discard;
	        
	        #ifdef __F11_ALPHACUTOUT
	        	lColourVec4.a = smoothstep( kfAlphaThreshold, kfAlphaThresholdMax, lColourVec4.a );
	    	#endif
	    }
	    #endif

		#ifdef D_DETAIL
	    	//TODO: ADD shit for detail maps
	    #endif

	    //NORMALS

		#ifdef __F03_NORMALMAP
			
			#if defined( __F44_IMPOSTER )
	            vec4 lTexColour = texture(mpCustomPerMaterial.gNormalMap, vec3(lTexCoordsVec4.xy, 0.0));
	            vec3 lNormalTexVec3 = normalize(lTexColour.xyz * 2.0 - 1.0);
	        #else
	            #ifdef __F55_MULTITEXTURE
	                vec4 lTexColour = texture(mpCustomPerMaterial.gNormalMap, vec3(lTexCoordsVec4.xy, lfMultiTextureIndex));
	            #else
					vec4 lTexColour = texture(mpCustomPerMaterial.gNormalMap, vec3(lTexCoordsVec4.xy, 0.0));
	            #endif 
	        
	        	vec3 lNormalTexVec3 = DecodeNormalMap( lTexColour );
	        #endif
	            
	         lNormalVec3 = lNormalTexVec3;
		#elif defined( D_USES_VERTEX_NORMAL )
	    	lNormalVec3 = mTangentSpaceNormalVec3;
		#endif

		#if defined( __F03_NORMALMAP ) || defined( __F42_DETAIL_NORMAL )
    	{
        	mat3 lTangentSpaceMat;

			#ifdef __F52_DECAL_NORMAL
	        {
	        	//The projected UV axes are the projector's local X and Y axes, not the tangent
	        	//frame of whichever cube face happened to rasterize this pixel. Transform those
	        	//axes into the rotated render space and orthonormalize them for non-uniform scale.
	        	mat3 projectorMat = mat3(mpCommonPerFrame.rotMat) *
	        	                    mat3(instanceData[instanceId].worldMat);
	        	vec3 decalTangent = normalize(projectorMat * vec3(1.0, 0.0, 0.0));
	        	vec3 decalBitangent = projectorMat * vec3(0.0, 1.0, 0.0);
	        	decalBitangent -= dot(decalBitangent, decalTangent) * decalTangent;
	        	if (dot(decalBitangent, decalBitangent) < 1e-8)
	        		decalBitangent = cross(projectorMat * vec3(0.0, 0.0, 1.0), decalTangent);
	        	decalBitangent = normalize(decalBitangent);
	        	vec3 decalNormal = normalize(cross(decalTangent, decalBitangent));
	        	if (dot(decalNormal, projectorMat * vec3(0.0, 0.0, 1.0)) < 0.0)
	        		decalNormal = -decalNormal;
	        	decalBitangent = normalize(cross(decalNormal, decalTangent));
	        	lTangentSpaceMat = mat3(decalTangent, decalBitangent, decalNormal);
	        }
	        #else
	        {
	            lTangentSpaceMat = TBN;
	        }
	        #endif
        
	        /* TODO ENABLE TAHT SHIT AT SOME POINT
	        #if defined( _F36_DOUBLESIDED ) && !defined( D_IMPOSTER_NORMAL)

	            lTangentSpaceMat[ 1 ] = cross( lFaceNormalVec3, lTangentSpaceMat[ 0 ] ) * IN( mWorldPositionVec3_mfSpare ).w;
	            lTangentSpaceMat[ 2 ] = lFaceNormalVec3;

	        #endif
	        */

			lNormalVec3 = normalize(lTangentSpaceMat * lNormalVec3);
    	}
	    #endif

	    #ifdef __F42_DETAIL_NORMAL
	    	//TODO ADD SOME SHIT HERE
	    #endif

		//Double-sided: back faces use the flipped normal
		#ifdef __F36_DOUBLESIDED
			if (!gl_FrontFacing)
				lNormalVec3 = -lNormalVec3;
		#endif

		//LIGHTING
		
		#ifndef __F07_UNLIT
	    {
	        #ifdef __F25_MASKS_MAP
	        {
	            lfRoughness = 1.0 - lMasks.r; //Glossiness

	            #ifdef D_DETAIL
	            {
	                //TODO: CHECK THAT SHIT OUT
	                //lfRoughness  = mix( lfRoughness, lDetailNormalVec4.b,               lfIsBlendImage    * smoothstep( lfBlendHeightMin, lfBlendHeightMax, lMasks.b ) );
	                //lfRoughness  = mix( lfRoughness, lDetailNormalVec4.b * lfRoughness, lfIsMultiplyImage * smoothstep( lfBlendHeightMin, lfBlendHeightMax, lMasks.b ) );
	            }
	            #endif
				
	        }
			#endif

			lfRoughness *= mpCustomPerMaterial.gMaterialParamsVec4.x;

	        #ifdef __F39_METALLIC_MASK
	        {
	            lfMetallic = lMasks.g;

	            #ifdef D_DETAIL
	            {
	                //TODO: CHECK THAT SHIT OUT
	                //lfMetallic = mix( lfMetallic, lDetailNormalVec4.b,               lfIsBlendImage    * smoothstep( lfBlendHeightMin, lfBlendHeightMax, lMasks.b ) );
	                //lfMetallic = mix( lfMetallic, lDetailNormalVec4.b * lfRoughness, lfIsMultiplyImage * smoothstep( lfBlendHeightMin, lfBlendHeightMax, lMasks.b ) );
	            }
	            #endif        
	        }
	        #else 
	        	lfMetallic = mpCustomPerMaterial.gMaterialParamsVec4.z;
        	#endif

			#ifdef __F22_OCCLUSION_MAP
	        {
				#ifdef __F55_MULTITEXTURE
	            	vec4 ao_color = texture(mpCustomPerMaterial.gOcclusionMap, vec3(lTexCoordsVec4.xy, lfMultiTextureIndex));
	        	#else
	            	vec4 ao_color = texture(mpCustomPerMaterial.gOcclusionMap, vec3(lTexCoordsVec4.xy, 0.0));
	        	#endif
	            ao = ao_color.g; //It seems that the red channel is always 1
				
			}
	        #endif
		}
	    #endif

	} else {
		lColourVec4 = vec4(color, 1.0);
		lNormalVec3 = mTangentSpaceNormalVec3;
	}

	#ifdef __F34_GLOW
    {
        #if defined(__F35_GLOW_MASK) && !defined(__F09_TRANSPARENT)
			lfGlow = mpCustomPerMaterial.gCustomParams01Vec4.y * lLowAlpha;
		#else
			lfGlow = mpCustomPerMaterial.gCustomParams01Vec4.y;
		#endif
    }
    #endif

	//Glow material class (current game versions): the surface emits its own colour
	#ifdef __D_CLASS_GLOW
		lfGlow = max(lfGlow, 1.0);
	#endif


	lColourVec4 = ApplySelectedColor(lColourVec4);

	//WRITE OUTPUT


	#ifdef _D_DEFERRED_RENDERING
		//Save Info to GBuffer
	    //Albedo
		outcolors[0] = lColourVec4;
		//Normals
		outcolors[1].rgb = lNormalVec3;
		outcolors[1].a = isLit; //TODO: Use the alpha channel of that attachment to upload any extra material flags

		//Export Frag Params
		outcolors[2].x = ao;
		outcolors[2].y = saturate(lfMetallic);
		outcolors[2].z = lfRoughness;
		outcolors[2].a = max(lfGlow, 0.0); //Glow intensity (RGBA16F, may exceed 1)

	#else
		
		//FORWARD LIGHTING
		vec4 finalColor = lColourVec4;

		#ifndef __F07_UNLIT
		if (mpCommonPerFrame.use_lighting > 0.0) {
			//Lights and the camera live in the rotated scene space, like the deferred pass
			vec4 lRotatedFragPos = mpCommonPerFrame.rotMat * fragPos;
			lNormalVec3 = dot(lNormalVec3, lNormalVec3) > 1e-12 ? normalize(lNormalVec3) : vec3(0.0, 0.0, 1.0);

			//Start from the environment lighting instead of the unlit colour
			//Transparent meshes do not receive key light shadows
			finalColor.rgb = calcEnvironmentLighting(mpCommonPerFrame.lights[0], lRotatedFragPos, lNormalVec3,
				mpCommonPerFrame.cameraPosition.xyz, lColourVec4.rgb, lfMetallic, lfRoughness, ao, 1.0);

			//Scene lights, from the light clusters
			finalColor.rgb += calcSceneLighting(gl_FragCoord.xy, viewDepthFromWindowZ(gl_FragCoord.z, mpCommonPerFrame.projMatInv),
				lRotatedFragPos, lNormalVec3, mpCommonPerFrame.cameraPosition.xyz, mpCommonPerFrame.cameraDirection.xyz,
				lColourVec4.rgb, lfMetallic, lfRoughness, ao);
		}
		#else
			finalColor = lColourVec4;
		#endif

		//Emissive
		finalColor.rgb += lColourVec4.rgb * max(lfGlow, 0.0);

		//Keep the RGBA16F accumulation finite (max 65504) when bright glow layers overlap
		finalColor.rgb = min(finalColor.rgb, vec3(64.0));

		//Weighted Blended order independent transparency (McGuire & Bavoil, depth weight from window z).
		//The old weight ran pow() on NDC z, which is negative for the nearer half of the depth range and gives NaN
		float d = 1.0 - gl_FragCoord.z;
		float weight = max(min(1.0, max3(finalColor.rgb) * finalColor.a), finalColor.a) *
						clamp(100.0 * d * d * d, 1e-2, 1e2);
		
		outcolors[0] = vec4(finalColor.rgb * finalColor.a, finalColor.a) * weight;
		outcolors[1] = vec4(finalColor.a);

	#endif
}


void main(){

	pbr_lighting();
}
