#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable

//Imports
#include "/common.glsl"

/* Copies incoming fragment color without change. */
uniform vec3 color;
uniform float intensity;
//Diffuse Textures
uniform int diffTexCount;
uniform sampler2D diffuseTex;
uniform sampler2D maskTex;
uniform sampler2D normalTex;
uniform bool matflags[64];

//Normal Texture
uniform float diffuseFlag;
uniform bool procFlag;
uniform bool useLighting;

in vec3 E;
in vec3 N;
in vec2 uv0;
in float l_distance;
in mat3 TBN;
in float bColor;

//Selected
uniform int selected;

//Normal Decode Function
vec3 DecodeNormalMap(vec4 lNormalTexVec4){
	lNormalTexVec4 = (lNormalTexVec4 * 2.0) - 1.0;

	return normalize(vec3(lNormalTexVec4.a, lNormalTexVec4.g, 
					 (1.0 - lNormalTexVec4.a * lNormalTexVec4.a) * \
					 (1.0 - lNormalTexVec4.g * lNormalTexVec4.g) ));
}


void main()
{	
	//Final Light/Normal vector calculations
	
	
	vec4 diffTexColor = vec4(color, 1.0); 
	//Colors
	//Check _F01_DIFFUSEMAP
	if (matflags[0] && (diffuseFlag > 0.0))
		diffTexColor = texture2D(diffuseTex, uv0);
	
	vec3 lightColor = vec3(0.8, 0.8, 0.8);
	vec3 ambient;
	ambient = diffTexColor.rgb;

	float shininess;
	float bshininess;
		
	float alpha;
	alpha = diffTexColor.a;
	//Mask Checks
	//Check _F24_AOMAP
 	if (matflags[23] && (diffuseFlag > 0.0))
 	 	diffTexColor.rgb *= texture2D(maskTex, uv0).r;
	
	bshininess = pow(max (dot (N, E), 0.0), 2.0);	
	//Check _F03_NORMALMAP 63
	if (matflags[2] && (diffuseFlag > 0.0)) {
		//Normal Checks
	  	vec3 normal;
	 	
  		normal = DecodeNormalMap(texture2D(normalTex, uv0));
  		bshininess = pow(max (dot (E, normalize(TBN * normal)), 0.0), 2.0);
  	}

	ambient = 0.5 * ambient;
	vec3 diff;
	
	diff = intensity * lightColor * bshininess * diffTexColor.rgb; //(l_distance*l_distance);

    //gl_FragColor = vec4(ambient + (intense + 1.0) * diff.xyz, 1.0);	
    //if ((diffTexColor.r <0.0001) && (diffTexColor.g <0.0001) && (diffTexColor.b < 0.0001)) discard;
    

    gl_FragColor = vec4(ambient + diff.xyz, 1.0);	
    if (selected>0.0) gl_FragColor *= vec4(0.0, 2.0, 0.0, 1.0);
    //gl_FragColor = vec4(N, 1.0);
    


    
}