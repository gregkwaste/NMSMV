/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */


//Extra preprocessor directives
#if defined( __F25_ROUGHNESS_MASK ) || defined( __F41_DETAIL_DIFFUSE ) || defined( __F42_DETAIL_NORMAL ) || defined( __F24_AOMAP ) || defined( __F40_SUBSURFACE_MASK ) || defined( __F54_COLOURMASK )
    #define D_MASKS
#endif

#if defined( __F01_DIFFUSEMAP ) || defined( __F03_NORMALMAP ) || defined( D_IMPOSTERMASKS ) || defined(D_MASKS)
    #define D_TEXCOORDS
#endif

#if defined( __F42_DETAIL_NORMAL ) || defined( __F41_DETAIL_DIFFUSE )
    //#define D_DETAIL
#endif

 
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
	const float kfAlphaThreshold = 0.5;
	const float kfAlphaThresholdMax = 0.9;
#else
	const float kfAlphaThreshold = 0.0001;
#endif


//New Decoding function - RGTC
vec3 DecodeNormalMap(vec4 lNormalTexVec4 ){
    lNormalTexVec4 = normalize(2.0 * lNormalTexVec4 - 1.0);
    return ( vec3( lNormalTexVec4.r, lNormalTexVec4.g, sqrt( max( 1.0 - lNormalTexVec4.r*lNormalTexVec4.r - lNormalTexVec4.g*lNormalTexVec4.g, 0.0 ) ) ) );
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
	float lLowAlpha = 1.0; //TODO : Find out what exactly is that shit
	float lHighAlpha = 1.0; //TODO : Find out what exactly is that shit
	vec4 world = fragPos;
	float isLit = 1.0;
	float lfRoughness = 1.0;
	float lfMetallic = 0.0;
	float lfSubsurface = 0.0; //Not used atm
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

			vec4 localPos = instanceData[instanceId].worldMatInv * world;
			localPos /= localPos.w;

			//Clip
			float decalBias = 0.01;
			clip(0.5 - decalBias - abs(localPos.x));
			clip(0.5 - decalBias- abs(localPos.y));
			clip(0.5 - decalBias- abs(localPos.z));


			lTexCoordsVec4.xy = localPos.xy + 0.5;
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

			#if !defined(__F07_UNLIT) && defined(__F39_METALLIC_MASK)
				#if defined(__F34_GLOW) && defined(__F35_GLOW_MASK) && !defined(__F09_TRANSPARENT)
					lHighAlpha = GetUpperValue(lColourVec4.a);
				#else
					lHighAlpha = lColourVec4.a;
				#endif
			#endif
			
			#if defined(__F34_GLOW) && defined (__F35_GLOW_MASK) && !defined (__F09_TRANSPARENT)
				lLowAlpha = GetLowerValue(lColourVec4.a);
			#endif
			
			#if !defined(__F09_TRANSPARENT) && !defined(__F11_ALPHACUTOUT)
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

			#ifndef __F17_MULTIPLYDIFFUSE2MAP
				lColourVec4.rgb = mix( lColourVec4.rgb, lDiffuse2Vec4.rgb, lDiffuse2Vec4.a );
			#endif
		#endif

		#ifdef __F21_VERTEXCOLOUR
			lColourVec4 *= vertColor;
		#endif
		
		//TRANSPARENCY

		//Mask Checks
		#ifdef __F22_TRANSPARENT_SCALAR
			// Transparency scalar comes from float in Material
	        lColourVec4.a *= mpCustomPerMaterial.gMaterialColourVec4.a;
	    #endif

		// Discard fully transparent pixels
	    #if defined( __F09_TRANSPARENT ) || defined( __F22_TRANSPARENT_SCALAR ) || defined( __F11_ALPHACUTOUT )
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

	    #ifdef __F24_AOMAP
			lColourVec4.rgb *= lMasks.r;
		#endif

		#ifdef __F17_MULTIPLYDIFFUSE2MAP
			lColourVec4.rgb *= diffTex2Factor;
		#endif

		//NORMALS

		#ifdef __F03_NORMALMAP
			#ifdef __F43_NORMAL_TILING
	        	lTexCoordsVec4.xy *= mpCustomPerMaterial.gCustomParams01Vec4.z;
	        #endif
	        
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
	        	/* TODO ENABLE THAT SHIT AT SOME POINT
	           		// lTangentSpaceMat = GetDecalTangentSpaceMatrix( lWorldPositionVec3, lUniforms.mpPerFrame->gViewMat4 );
	            	lTangentSpaceMat = GetCotangentFrame( lWorldPositionVec3, lUniforms.mpPerFrame->gViewPositionVec3, lTexCoordsVec4.xy );
	            */
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

			lNormalVec3 = normalize(TBN * lNormalVec3);        
    	}
	    #endif

	    #ifdef __F42_DETAIL_NORMAL
	    	//TODO ADD SOME SHIT HERE
	    #endif

		//LIGHTING

		#ifndef __F07_UNLIT
	    {
	        #ifdef __F25_ROUGHNESS_MASK
	        {
	            lfRoughness = lMasks.g;

	            #ifdef D_DETAIL
	            {
	                //TODO: CHECK THAT SHIT OUT
	                //lfRoughness  = mix( lfRoughness, lDetailNormalVec4.b,               lfIsBlendImage    * smoothstep( lfBlendHeightMin, lfBlendHeightMax, lMasks.b ) );
	                //lfRoughness  = mix( lfRoughness, lDetailNormalVec4.b * lfRoughness, lfIsMultiplyImage * smoothstep( lfBlendHeightMin, lfBlendHeightMax, lMasks.b ) );
	            }
	            #endif

	            lfRoughness = 1.0 - lfRoughness;
	        }
	        #endif

			lfRoughness *= mpCustomPerMaterial.gMaterialParamsVec4.x;

	        #ifdef __F39_METALLIC_MASK
	        {
	            lfMetallic = lHighAlpha;

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

	        #ifdef __F40_SUBSURFACE_MASK
	        	lfSubsurface = lMasks.r;
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


    #ifdef __F34_GLOW
		lColourVec4.rgb = mix( lColourVec4.rgb, lColourVec4.rgb, lfGlow );
		#ifdef __F35_GLOW_MASK
			lColourVec4.a = lfGlow;
		#endif
	#endif


	//WRITE OUTPUT


	#ifdef _D_DEFERRED_RENDERING
		//Save Info to GBuffer
	    //Albedo
		outcolors[0] = lColourVec4;
		//Normals
		outcolors[1].rgb = lNormalVec3;
		outcolors[1].a = isLit; //TODO: Use the alpha channel of that attachment to upload any extra material flags

		//Export Frag Params
		outcolors[2].x = ao; //ao is already multiplied to the color
		outcolors[2].y = saturate(lfMetallic);
		outcolors[2].z = lfRoughness;
		outcolors[2].a = saturate(lfSubsurface);

		//TODO SEND GLOW TO NEXT STAGES

	#else
		
		//FORWARD LIGHTING
		vec4 finalColor = lColourVec4;

		#ifndef __F07_UNLIT
		if (mpCommonPerFrame.use_lighting > 0.0) {
			for(int i = 0; i < mpCommonPerFrame.light_count; ++i) 
		    {
		    	// calculate per-light radiance
		        Light light = mpCommonPerFrame.lights[i]; 

				//Pos.w is the renderable status of the light
				if (light.position.w < 1.0)
		        	continue;
	    		
	    		finalColor.rgb += calcLighting(light, fragPos, lNormalVec3, 
				mpCommonPerFrame.cameraPosition.xyz, mpCommonPerFrame.cameraDirection.xyz,
		            lColourVec4.rgb, lfMetallic, lfRoughness, ao);
			} 
		}
		#else
			finalColor = lColourVec4;
		#endif

		//Weighted Blended order independent transparency
		float z = screenPos.z / screenPos.w;
		float weight = max(min(1.0, max3(finalColor.rgb) * finalColor.a), finalColor.a) *
						clamp(0.03 / (1e-5 + pow(z/200, 4.0)), 1e-2, 3e3);
		
		outcolors[0] = vec4(finalColor.rgb * finalColor.a, finalColor.a) * weight;
		outcolors[1] = vec4(finalColor.a);

	#endif
}


void main(){

	pbr_lighting();
}