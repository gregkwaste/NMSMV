#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_texture_query_lod : enable

//Imports
#include "/common.glsl"

/* Copies incoming fragment color without change. */
uniform vec3 color;
uniform float intensity;
//Diffuse Textures
uniform int diffTexCount;
uniform bool matflags[64];

layout(location=75) uniform sampler2D diffuseTex;
layout(location=76) uniform sampler2D maskTex;
layout(location=77) uniform sampler2D normalTex;

//Normal Texture
uniform float diffuseFlag;
uniform bool procFlag;
// uniform bool useLighting; Unused

in vec3 E;
in vec3 N;
in vec2 uv0;
in float l_distance;
in mat3 TBN;
in float bColor;
in vec4 finalPos;

//Selected
uniform int selected;

//Deferred Shading outputs
out vec4 outcolors[3];


//Old Decoding function 
//Normal Decode Function - DTX5
// vec3 DecodeNormalMap(vec4 lNormalTexVec4){
// 	lNormalTexVec4 = (lNormalTexVec4 * 2.0) - 1.0;

// 	return normalize(vec3(lNormalTexVec4.a, lNormalTexVec4.g, 
// 					 (1.0 - lNormalTexVec4.a * lNormalTexVec4.a) * \
// 					 (1.0 - lNormalTexVec4.g * lNormalTexVec4.g) ));
// }

//New Decoding function - RGTC
vec3 DecodeNormalMap(vec4 lNormalTexVec4 ){
    lNormalTexVec4 = ( lNormalTexVec4 * ( 2.0 * 255.0 / 256.0 ) ) - 1.0;
    return ( vec3( lNormalTexVec4.r, lNormalTexVec4.g, sqrt( max( 1.0 - lNormalTexVec4.r*lNormalTexVec4.r - lNormalTexVec4.g*lNormalTexVec4.g, 0.0 ) ) ) );
}

void main()
{	
	//Final Light/Normal vector calculations
	
	float mipmaplevel;
	mipmaplevel = textureQueryLOD(diffuseTex, uv0).x;
	vec4 diffTexColor = vec4(color, 1.0); 
	//Colors
	//Check _F01_DIFFUSEMAP
	if (matflags[0] && (diffuseFlag > 0.0))
		diffTexColor = textureLod(diffuseTex, uv0, mipmaplevel);
	
	vec3 lightColor = vec3(0.8, 0.8, 0.8);
	vec3 ambient;
	ambient = diffTexColor.rgb;

	float shininess;
	float bshininess;
		
	float alpha;
	alpha = diffTexColor.a;
	
	//Mask Checks
	
	//Check _F11_ALPHACUTOUT
	if (matflags[10]) {
		float maskalpha =  textureLod(diffuseTex, uv0, mipmaplevel).a;
		if (maskalpha <= 0.05) discard;
	}

	//Check _F9_TRANSPARENT
	if (matflags[8]) {
		if (alpha <= 0.05) discard;
	}
	
	//Check _F24_AOMAP
 	if ((matflags[23])  && (diffuseFlag > 0.0)){
 		mipmaplevel = textureQueryLOD(maskTex, uv0).x;
 		float maskalpha =  textureLod(maskTex, uv0, mipmaplevel).r;
 		diffTexColor.rgb *= maskalpha; //Is the r channel the ambient occlusion map?
 	}
	
	vec3 normal = N;
	bshininess = pow(max (dot (E, N), 0.0), 2.0);	
	//Check _F03_NORMALMAP 63
	if (matflags[2] && (diffuseFlag > 0.0)) {
		//Normal Checks
	  	
	 	mipmaplevel = textureQueryLOD(normalTex, uv0).x;
  		normal = DecodeNormalMap(textureLod(normalTex, uv0, mipmaplevel));
  		bshininess = pow(max (dot (E, normalize(TBN * normal)), 0.0), 2.0);
  	}

	ambient = 0.85 * ambient;
	vec3 diff;
	
	diff = intensity * lightColor * bshininess * diffTexColor.rgb; //(l_distance*l_distance);

    //gl_FragColor = vec4(ambient + (intense + 1.0) * diff.xyz, 1.0);	
    //if ((diffTexColor.r <0.0001) && (diffTexColor.g <0.0001) && (diffTexColor.b < 0.0001)) discard;
    


    //outcolors[0] = vec4(ambient + diff.xyz, 1.0);
	outcolors[0] = vec4(diffTexColor.rgb, 1.0);
    //outcolors[0] = vec4(1.0, 1.0, 1.0, 1.0);
	if (selected>0.0) outcolors[0] *= vec4(0.0, 1.5, 0.0, 1.0);
    //gl_FragColor = vec4(N, 1.0);

    outcolors[1] = finalPos;
    //outcolors[1] = vec4(N, 1.0);
    outcolors[2] = vec4(0.0, 1.0, 0.0, 1.0);
}