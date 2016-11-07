#extension GL_ARB_shading_language_include : require
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

varying vec3 E,N;
varying float l_distance;
varying mat3 TBN;
varying vec3 nvectors[3];
varying vec2 uv0;
varying float bColor;

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
	if (matflags[0])
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
 	if (matflags[23])
 		diffTexColor.rgb *= texture2D(maskTex, uv0).r;
	
	shininess = pow(max (dot (E, N), 0.0), 4.0);
	
	//Check _F03_NORMALMAP
	// if (matflags[2]) {
	// 	//Normal Checks
	//  	vec3 normal;
	 	
 // 		normal = DecodeNormalMap(texture2D(normalTex, uv0));
 // 		bshininess = pow(max (dot (E, normalize(TBN * normal)), 0.0), 2.0);
 // 	} else {
	// 	bshininess = pow(max (dot (E, N), 0.0), 2.0);	
	// }
	bshininess = pow(max (dot (E, N), 0.0), 2.0);

	ambient = 0.8 * ambient;
	vec3 diff;
	
	diff = intensity * lightColor * shininess * diffTexColor.rgb; //(l_distance*l_distance);

    //gl_FragColor = vec4(ambient + (intense + 1.0) * diff.xyz, 1.0);	
    gl_FragColor = vec4(ambient + diff.xyz, 1.0);	
    //gl_FragColor = vec4(nvectors[1], 1.0);
    
}