#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_texture_query_lod : enable

//Includes
#include "/common.glsl"

/* Copies incoming fragment color without change. */
uniform vec3 color;
uniform float intensity;

layout(location=11) uniform bool matflags[64];
layout(location=75) uniform sampler2D diffuseTex;
layout(location=76) uniform sampler2D maskTex;
layout(location=77) uniform sampler2D normalTex;


//Material Options
uniform vec4 gMaterialColourVec4;
uniform vec4 gMaterialParamsVec4;
uniform vec4 gMaterialSFXVec4;
uniform vec4 gMaterialSFXColVec4;
uniform vec4 gDissolveDataVec4;


//Rendering Options
uniform float diffuseFlag; //Enable Textures
uniform float use_lighting; //Enable lighting
uniform int selected; //Selected

in vec3 E;
in vec3 N;
in vec2 uv0;
in float l_distance;
in mat3 TBN;
in float bColor;
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
	vec4 diffTexColor = vec4(color, 1.0); 
	//Colors
	//Check _F01_DIFFUSEMAP
	if (diffuseFlag > 0.0){
		if (matflags[_F01_DIFFUSEMAP]) {
			mipmaplevel = textureQueryLOD(diffuseTex, uv0).x;
			diffTexColor = textureLod(diffuseTex, uv0, mipmaplevel);
			if (!matflags[_F09_TRANSPARENT]){
				diffTexColor.a = 1.0f;
			}
		} else{
			diffTexColor = gMaterialColourVec4;
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
	if (matflags[_F11_ALPHACUTOUT]) {
		float maskalpha =  textureLod(diffuseTex, uv0, mipmaplevel).a;
		if (maskalpha <= 0.05) discard;
	}

	//Check _F9_TRANSPARENT
	if (matflags[_F09_TRANSPARENT]) {
		if (alpha <= 0.05) discard;
	}
	
	//Check _F24_AOMAP
 	if ((matflags[_F24_AOMAP])  && (diffuseFlag > 0.0)){
 		float maskalpha =  textureLod(maskTex, uv0, mipmaplevel).r;
 		diffTexColor.rgb *= maskalpha; //Is the r channel the ambient occlusion map?
 	}
	
	vec3 normal = N;
	bshininess = pow(max (dot (E, N), 0.0), 2.0);	
	
	ambient = vec3(0.3, 0.3, 0.3);
	vec3 diff = vec3(0.0, 0.0, 0.0);

	if (diffuseFlag > 0.0){
		//Check _F03_NORMALMAP 63
		if (matflags[_F03_NORMALMAP]) {
			//Normal Checks
	  		normal = DecodeNormalMap(textureLod(normalTex, uv0, mipmaplevel));
	  		normal = normalize(TBN * normal);
	  		bshininess = pow(max (dot (E, normal), 0.0), 2.0);
	  	}

		if (matflags[_F25_ROUGHNESS_MASK]) {
			lfRoughness = textureLod(maskTex, uv0, mipmaplevel).g;
			lfRoughness = 1.0 - lfRoughness;
			lfRoughness *= gMaterialParamsVec4.x;
		}  

		if (matflags[_F39_METALLIC_MASK]) {
			lfMetallic = GetUpperValue(alpha);
		} else{
			lfMetallic = gMaterialParamsVec4.z;
		}
	}
	
	diff += ambient * (0.5 + ((lfRoughness) * 0.5));
    diff *= (intensity * lightColor * bshininess + 1.0) * diffTexColor.rgb; //+ lSpecularColourVec3 * PhongApprox( lfRoughness, lfRoL );
    //diff += lfShadow * lFresnelColVec3;
    
    if (use_lighting > 0.0){
		outcolors[0] = vec4(diff.rgb, 1.0);
	}
	else {
		outcolors[0] = vec4(diffTexColor.rgb, 1.0);
	}

    //outcolors[0] = vec4(1.0, 1.0, 1.0, 1.0);
	if (selected>0.0) outcolors[0] *= vec4(0.0, 1.5, 0.0, 1.0);
    //gl_FragColor = vec4(N, 1.0);
    outcolors[1] = finalPos;
    //outcolors[1] = vec4(N, 1.0);
    outcolors[2] = vec4(0.0, 1.0, 0.0, 1.0);
}