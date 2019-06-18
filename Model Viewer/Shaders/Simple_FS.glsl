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
//uniform Light lights[4]; //Support up to 4 lights for now

//Uniform Blocks
layout (std140) uniform Uniforms
{
    CommonPerFrameUniforms mpCommonPerFrame;
    CommonPerMeshUniforms mpCommonPerMesh;
};


in vec3 E;
in vec3 N;
in vec2 uv0;
in float l_distance;
in mat3 TBN;
in vec3 default_color;
in vec4 finalPos;

//Deferred Shading outputs
out vec4 outcolors[3];


//New Decoding function - RGTC
vec3 DecodeNormalMap(vec4 lNormalTexVec4 ){
    lNormalTexVec4 = ( lNormalTexVec4 * ( 2.0 * 255.0 / 256.0 ) ) - 1.0;
    return ( vec3( lNormalTexVec4.r, lNormalTexVec4.g, sqrt( max( 1.0 - lNormalTexVec4.r*lNormalTexVec4.r - lNormalTexVec4.g*lNormalTexVec4.g, 0.0 ) ) ) );
}


void main()
{	
	//Final Light/Normal vector calculations
	
	float mipmaplevel = 0.0;
	vec4 diffTexColor = vec4(mpCommonPerMesh.color, 1.0); 
	//Colors
	//Check _F01_DIFFUSEMAP
	if (mpCommonPerFrame.diffuseFlag > 0.0){
		if (mpCustomPerMaterial.matflags[_F01_DIFFUSEMAP]) {
			if (mpCustomPerMaterial.matflags[_F55_MULTITEXTURE]){	
				mipmaplevel = textureQueryLOD(mpCustomPerMaterial.gDiffuseMap, uv0).x;
				diffTexColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(uv0, mpCustomPerMaterial.gUserDataVec4.w), mipmaplevel);
				//diffTexColor = texture(gDiffuseMap, vec3(uv0, mpCustomPerMaterial.gUserDataVec4.w));
			}
			else {
				mipmaplevel = textureQueryLOD(mpCustomPerMaterial.gDiffuseMap, uv0).x;
				diffTexColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(uv0, 0.0), mipmaplevel);
				//diffTexColor = texture(gDiffuseMap, vec3(uv0, 0.0));
			}

			if ((!mpCustomPerMaterial.matflags[_F09_TRANSPARENT]) || (!mpCustomPerMaterial.matflags[_F22_TRANSPARENT_SCALAR])){
					diffTexColor.a = 1.0f;
			}
		} else {
			diffTexColor = mpCustomPerMaterial.gMaterialColourVec4;
		}
	}
	
	vec3 lightColor = vec3(0.8, 0.8, 0.8);
	vec3 ambient;
	ambient = diffTexColor.rgb;

	float shininess;
	float bshininess;
	float lfRoughness = 1.0f;
	float lfMetallic = 0.0f;
		
	float alpha;
	alpha = diffTexColor.a;
	
	//Mask Checks
	
	//Check _F11_ALPHACUTOUT
	if (mpCustomPerMaterial.matflags[_F11_ALPHACUTOUT]) {
		if (alpha <= 0.05) discard;
	}

	if (mpCustomPerMaterial.matflags[_F22_TRANSPARENT_SCALAR]){
		// Transparency scalar comes from float in Material
        alpha *= mpCustomPerMaterial.gMaterialColourVec4.a;	
	}

	//Check _F9_TRANSPARENT
	if (mpCustomPerMaterial.matflags[_F09_TRANSPARENT] || mpCustomPerMaterial.matflags[_F22_TRANSPARENT_SCALAR]) {
		if (alpha <= 0.05) discard;
	}
	
	//Check _F24_AOMAP
 	if ((mpCustomPerMaterial.matflags[_F24_AOMAP])  && (mpCommonPerFrame.diffuseFlag > 0.0)){
 		float maskalpha =  textureLod(mpCustomPerMaterial.gMasksMap, vec3(uv0, 0.0), mipmaplevel).r;
 		diffTexColor.rgb *= maskalpha; //Is the r channel the ambient occlusion map?
 	}
	
	vec3 normal = N;
	bshininess = pow(max (dot (E, N), 0.0), 2.0);	
	
	ambient = vec3(0.3, 0.3, 0.3);
	vec3 diff = vec3(0.0, 0.0, 0.0);

	if (mpCommonPerFrame.diffuseFlag > 0.0){
		//Check _F03_NORMALMAP 63
		if (mpCustomPerMaterial.matflags[_F03_NORMALMAP]) {
			//Normal Checks
	  		normal = DecodeNormalMap(textureLod(mpCustomPerMaterial.gNormalMap, vec3(uv0,0.0), mipmaplevel));
	  		normal = normalize(TBN * normal);
	  		bshininess = pow(max (dot (E, normal), 0.0), 2.0);
	  	}

		if (mpCustomPerMaterial.matflags[_F25_ROUGHNESS_MASK]) {
			lfRoughness = textureLod(mpCustomPerMaterial.gMasksMap, vec3(uv0, 0.0), mipmaplevel).g;
			lfRoughness = 1.0 - lfRoughness;
			lfRoughness *= mpCustomPerMaterial.gMaterialParamsVec4.x;
		}  

		if (mpCustomPerMaterial.matflags[_F39_METALLIC_MASK]) {
			lfMetallic = GetUpperValue(alpha);
		} else{
			lfMetallic = mpCustomPerMaterial.gMaterialParamsVec4.z;
		}
	}
	
	diff += ambient * (0.5 + ((lfRoughness) * 0.5));
    diff *= (1.0f * lightColor * bshininess + 1.0) * diffTexColor.rgb; //+ lSpecularColourVec3 * PhongApprox( lfRoughness, lfRoL );
    
    //This is what I used
    //diff *= (1.0f * lightColor * bshininess + 1.0) * diffTexColor.rgb; //+ lSpecularColourVec3 * PhongApprox( lfRoughness, lfRoL );
    
    //diff += lfShadow * lFresnelColVec3;
    
    if (mpCommonPerFrame.use_lighting > 0.0){
		outcolors[0] = vec4(diff.rgb, alpha);
	}
	else {
		outcolors[0] = vec4(diffTexColor.rgb, alpha);
	}

    //outcolors[0] = vec4(1.0, 1.0, 1.0, 1.0);
	if (mpCommonPerMesh.selected > 0.0) outcolors[0] *= vec4(0.0, 1.5, 0.0, 1.0);
    //gl_FragColor = vec4(N, 1.0);
    outcolors[1] = finalPos;
    //outcolors[1] = vec4(N, 1.0);
    outcolors[2] = vec4(0.0, 1.0, 0.0, 1.0);


    //Apply gamma correction
    float gamma = 2.2;
    outcolors[0].rgb = pow(outcolors[0].rgb, vec3(1.0/gamma));
}
